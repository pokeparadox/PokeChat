using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PokeChat.Data;
using PokeChat.Data.Entities;
using PokeChat.LLM;
using PokeChat.Tools;

namespace PokeChat.Api.Core.Planning
{
    public class PlannerService : IPlannerService
    {
        private readonly IDbContextFactory<PokeChatDbContext> _contextFactory;
        private readonly ToolRegistry _toolRegistry;
        private readonly LLMOrchestrator? _llmOrchestrator;

        public PlannerService(IDbContextFactory<PokeChatDbContext> contextFactory, ToolRegistry toolRegistry, LLMOrchestrator? llmOrchestrator = null)
        {
            _contextFactory = contextFactory;
            _toolRegistry = toolRegistry;
            _llmOrchestrator = llmOrchestrator;
        }

        public async Task<TaskList?> FindRelevantPlanAsync(string goal, string contextTags)
        {
            using var context = _contextFactory.CreateDbContext();
            var exactMatches = await context.TaskLists
                .Where(tl => tl.ContextTags != null && tl.ContextTags.Contains(contextTags))
                .ToListAsync();

            if (exactMatches.Any())
            {
                return exactMatches.OrderByDescending(tl => tl.SuccessRating).First();
            }

            var tags = contextTags.Split(',').Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            var partialMatches = await context.TaskLists
                .Where(tl => tags.All(tag => tl.ContextTags != null && tl.ContextTags.Contains(tag)))
                .ToListAsync();

            if (partialMatches.Any())
            {
                return partialMatches.OrderByDescending(tl => tl.SuccessRating).First();
            }

            return await context.TaskLists
                .OrderByDescending(tl => tl.SuccessRating)
                .FirstOrDefaultAsync();
        }

        public async Task<TaskList> PlanAsync(string goal)
        {
            string contextTags = ExtractContextTags(goal);
            var existingPlan = await FindRelevantPlanAsync(goal, contextTags);
            TaskList newPlan;

            if (existingPlan != null)
            {
                newPlan = CloneTaskList(existingPlan);
                await AdaptPlanAsync(newPlan, goal, contextTags);
            }
            else
            {
                newPlan = CreateNewPlan(goal, contextTags);
                await AdaptPlanAsync(newPlan, goal, contextTags);
            }

            using var context = _contextFactory.CreateDbContext();
            context.TaskLists.Add(newPlan);
            await context.SaveChangesAsync();
            return newPlan;
        }

        public async Task AdaptPlanAsync(TaskList plan, string goal, string contextTags)
        {
            if (plan.Tasks != null && plan.Tasks.Any())
            {
                foreach (var task in plan.Tasks)
                {
                    if (task.Type == TaskType.Reasoning && string.IsNullOrWhiteSpace(task.Payload))
                    {
                        task.Payload = $"Goal: {goal}. Context: {contextTags}";
                    }
                }
                return;
            }

            if (_llmOrchestrator == null || !_llmOrchestrator.IsAvailable)
                return;

            var prompt = $@"Break this goal into 3-5 concrete steps. For each step, output ONE line in this exact format:
TYPE|PAYLOAD
where TYPE is one of: Reasoning, ToolCall
and PAYLOAD is the step description.

Goal: {goal}
Context tags: {contextTags}

Output only the steps, nothing else.";

            var response = _llmOrchestrator.GenerateResponse(prompt);
            if (string.IsNullOrWhiteSpace(response))
                return;

            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int order = 1;
            foreach (var line in lines)
            {
                var trimmed = line.Trim().TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' ');
                var pipeIndex = trimmed.IndexOf('|');
                if (pipeIndex < 0) continue;

                var typeStr = trimmed[..pipeIndex].Trim();
                var payload = trimmed[(pipeIndex + 1)..].Trim();

                if (!Enum.TryParse<TaskType>(typeStr, true, out var taskType))
                    taskType = TaskType.Reasoning;

                plan.Tasks.Add(new ExecutionTask
                {
                    SequenceOrder = order++,
                    Type = taskType,
                    Payload = payload,
                    Status = "Pending"
                });
            }
        }

        public async Task<string> ExecuteAsync(TaskList taskList)
        {
            if (taskList == null || !taskList.Tasks.Any())
                return "No tasks found in the plan.";

            var executionLog = new List<string>();
            int completedCount = 0;
            int failedCount = 0;

            using var context = _contextFactory.CreateDbContext();

            foreach (var task in taskList.Tasks.OrderBy(t => t.SequenceOrder))
            {
                executionLog.Add($"Executing {task.SequenceOrder}: {task.Type} - Payload: {task.Payload}");
                task.Status = "Running";
                context.Update(task);
                await context.SaveChangesAsync();

                try
                {
                    string result = await ExecuteTask(task);
                    task.Status = "Completed";
                    task.Result = result;
                    completedCount++;
                    executionLog.Add($"Result: {result}");
                }
                catch (Exception ex)
                {
                    task.Status = "Failed";
                    task.ErrorMessage = ex.Message;
                    failedCount++;
                    executionLog.Add($"Failed: {ex.Message}");
                }

                context.Update(task);
            }

            await context.SaveChangesAsync();

            double successRating = taskList.Tasks.Count > 0
                ? (double)completedCount / taskList.Tasks.Count
                : 0;
            taskList.SuccessRating = successRating;
            taskList.LastUsedAt = DateTime.UtcNow;
            context.Update(taskList);
            await context.SaveChangesAsync();

            executionLog.Add($"Summary: {completedCount} completed, {failedCount} failed, rating: {successRating:P0}");
            return string.Join("\n", executionLog);
        }

        private async Task<string> ExecuteTask(ExecutionTask task)
        {
            return task.Type switch
            {
                TaskType.ToolCall => await ExecuteToolCall(task),
                TaskType.SubPlan => await ExecuteSubPlan(task),
                TaskType.Reasoning => ExecuteReasoning(task),
                _ => throw new InvalidOperationException($"Unknown task type: {task.Type}")
            };
        }

        private async Task<string> ExecuteToolCall(ExecutionTask task)
        {
            if (string.IsNullOrWhiteSpace(task.Payload))
                throw new InvalidOperationException("ToolCall task requires Payload");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payload = JsonSerializer.Deserialize<ToolCallPayload>(task.Payload, options);
            if (payload == null || string.IsNullOrWhiteSpace(payload.ToolName))
                throw new InvalidOperationException("Invalid ToolCall payload: missing toolName");

            var result = _toolRegistry.TryExecute(payload.ToolName, payload.Args ?? Array.Empty<string>());
            if (result == null)
                throw new InvalidOperationException($"Tool '{payload.ToolName}' not found or disabled");

            if (!result.Success)
                throw new InvalidOperationException($"Tool '{payload.ToolName}' failed: {result.ErrorMessage}");

            return result.Output;
        }

        private async Task<string> ExecuteSubPlan(ExecutionTask task)
        {
            if (string.IsNullOrWhiteSpace(task.Payload))
                throw new InvalidOperationException("SubPlan task requires Payload");

            var subPlanId = JsonSerializer.Deserialize<int>(task.Payload);
            using var context = _contextFactory.CreateDbContext();
            var subPlan = await context.TaskLists
                .Include(tl => tl.Tasks)
                .FirstOrDefaultAsync(tl => tl.Id == subPlanId);

            if (subPlan == null)
                throw new InvalidOperationException($"SubPlan with ID {subPlanId} not found");

            return await ExecuteAsync(subPlan);
        }

        private string ExecuteReasoning(ExecutionTask task)
        {
            return task.Payload ?? "No reasoning payload provided";
        }

        private class ToolCallPayload
        {
            public string ToolName { get; set; } = string.Empty;
            public string[]? Args { get; set; }
        }

        private TaskList CloneTaskList(TaskList source)
        {
            return new TaskList
            {
                GoalDescription = source.GoalDescription,
                ContextTags = source.ContextTags,
                SuccessRating = source.SuccessRating,
                Version = source.Version + 1,
                IsTemplate = source.IsTemplate,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                Tasks = source.Tasks.Select(t => new ExecutionTask
                {
                    Type = t.Type,
                    Payload = t.Payload,
                    Status = "Cloned"
                }).ToList()
            };
        }

        private TaskList CreateNewPlan(string goal, string contextTags)
        {
            return new TaskList
            {
                GoalDescription = goal,
                ContextTags = contextTags,
                SuccessRating = 0.0,
                Version = 1,
                IsTemplate = true,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                Tasks = new List<ExecutionTask>()
            };
        }

        public async Task<List<TaskList>> GetAllPlansAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.TaskLists
                .Include(tl => tl.Tasks)
                .OrderByDescending(tl => tl.CreatedAt)
                .ToListAsync();
        }

        public string ExtractContextTags(string goal)
        {
            string[] commonTags = { "refactor", "bugfix", "data-migration", "testing", "ui", "api", "nlp" };
            var tags = new List<string>();

            foreach (var tag in commonTags)
            {
                if (goal.Contains(tag, StringComparison.OrdinalIgnoreCase))
                    tags.Add(tag);
            }

            return string.Join(",", tags);
        }
    }
}
