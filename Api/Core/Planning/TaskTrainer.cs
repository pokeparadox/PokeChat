using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PokeChat.Data.Entities;
using PokeChat.LLM;

namespace PokeChat.Api.Core.Planning
{
    public class TaskTrainer : ITaskTrainer
    {
        private readonly LLMOrchestrator? _llmOrchestrator;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public TaskTrainer(LLMOrchestrator? llmOrchestrator = null)
        {
            _llmOrchestrator = llmOrchestrator;
        }

        public async Task<TaskList> DecomposeGoalAsync(string goal)
        {
            var plan = new TaskList
            {
                GoalDescription = goal,
                IsTemplate = false,
                IsTrained = true,
                SuccessRating = 0.0,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                Tasks = new List<ExecutionTask>()
            };

            if (_llmOrchestrator == null || !_llmOrchestrator.IsAvailable)
                return BuildHeuristicPlan(goal);

            var prompt = BuildDecompositionPrompt(goal);
            var response = await Task.Run(() => _llmOrchestrator.GenerateResponse(prompt));

            if (string.IsNullOrWhiteSpace(response))
                return BuildHeuristicPlan(goal);

            plan.Tasks = ParseDecomposition(response);

            if (plan.Tasks.Count == 0)
                plan.Tasks = BuildHeuristicPlan(goal).Tasks;

            return plan;
        }

        public List<ExecutionTask> ParseDecomposition(string llmResponse)
        {
            var tasks = new List<ExecutionTask>();

            // Strip markdown fences that LLMs often wrap around JSON
            var cleaned = System.Text.RegularExpressions.Regex.Replace(llmResponse, @"```(?:json)?\s*\n?", "");

            var lines = cleaned.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int order = 1;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip markdown fences or non-JSON lines
                if (trimmed.StartsWith("```") || !trimmed.StartsWith("{"))
                    continue;

                try
                {
                    var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("type", out var typeProp))
                        continue;

                    var typeStr = typeProp.GetString();
                    if (string.IsNullOrEmpty(typeStr))
                        continue;

                    if (string.Equals(typeStr, "ToolCall", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!root.TryGetProperty("toolName", out var toolNameProp))
                            continue;

                        var toolName = toolNameProp.GetString() ?? "";
                        var args = new List<string>();

                        if (root.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var arg in argsProp.EnumerateArray())
                            {
                                var val = arg.ValueKind == JsonValueKind.String
                                    ? arg.GetString() ?? ""
                                    : arg.GetRawText();
                                args.Add(val);
                            }
                        }

                        var payload = JsonSerializer.Serialize(new { toolName, args }, JsonOptions);

                        tasks.Add(new ExecutionTask
                        {
                            SequenceOrder = order++,
                            Type = TaskType.ToolCall,
                            Payload = payload,
                            Status = "Pending"
                        });
                    }
                    else if (string.Equals(typeStr, "Reasoning", StringComparison.OrdinalIgnoreCase))
                    {
                        var payload = root.TryGetProperty("payload", out var payloadProp)
                            ? payloadProp.GetString() ?? ""
                            : "";

                        tasks.Add(new ExecutionTask
                        {
                            SequenceOrder = order++,
                            Type = TaskType.Reasoning,
                            Payload = payload,
                            Status = "Pending"
                        });
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed JSON lines
                }
            }

            return tasks;
        }

        private static string BuildDecompositionPrompt(string goal)
        {
            return $@"You are a task planning assistant. Given a goal, break it into concrete steps.
For each step, output ONE JSON object on its own line:
{{""type"":""ToolCall"",""toolName"":""<tool>"",""args"":[""<arg1>"",""<arg2>""]}}
{{""type"":""Reasoning"",""payload"":""<description>""}}
Available tools: read, write, bash, grep, glob
Each ToolCall toolName must match one of these tool names exactly.

When the goal involves updating or editing a file, the plan should typically include:
1. A Reasoning step describing the overall approach
2. A ToolCall with read to examine the current content
3. A Reasoning step about what changes to make
4. A ToolCall with write to produce the updated file

Goal: {goal}
Output ONLY the JSON lines, nothing else.";
        }

        private static TaskList BuildHeuristicPlan(string goal)
        {
            var plan = new TaskList
            {
                GoalDescription = goal,
                IsTemplate = false,
                IsTrained = true,
                SuccessRating = 0.0,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                Tasks = new List<ExecutionTask>()
            };

            var lower = goal.ToLowerInvariant();
            int order = 1;

            bool mentionsFile = System.Text.RegularExpressions.Regex.IsMatch(lower,
                @"\b\./?\S+\.\w+\b|file|document|read|write|update|edit|modify|change");
            bool mentionsRead = lower.Contains("read") || lower.Contains("understand") || lower.Contains("examine") || lower.Contains("explore");
            bool mentionsWrite = lower.Contains("write") || lower.Contains("update") || lower.Contains("edit") || lower.Contains("modify") || lower.Contains("add") || lower.Contains("change") || lower.Contains("improve");

            if (mentionsFile)
            {
                // Extract file path from goal
                var fileMatch = System.Text.RegularExpressions.Regex.Match(goal, @"(\./?[\w./\\-]+\.\w+)");
                var filePath = fileMatch.Success ? fileMatch.Groups[1].Value : null;

                if (mentionsWrite && filePath != null)
                {
                    plan.Tasks.Add(new ExecutionTask
                    {
                        SequenceOrder = order++,
                        Type = TaskType.Reasoning,
                        Payload = $"Read and understand the current content of {filePath}",
                        Status = "Pending"
                    });
                    plan.Tasks.Add(new ExecutionTask
                    {
                        SequenceOrder = order++,
                        Type = TaskType.ToolCall,
                        Payload = JsonSerializer.Serialize(new { toolName = "read", args = new[] { filePath } }, JsonOptions),
                        Status = "Pending"
                    });
                    plan.Tasks.Add(new ExecutionTask
                    {
                        SequenceOrder = order++,
                        Type = TaskType.Reasoning,
                        Payload = $"Analyse the content and determine what changes are needed based on: {goal}",
                        Status = "Pending"
                    });
                    plan.Tasks.Add(new ExecutionTask
                    {
                        SequenceOrder = order++,
                        Type = TaskType.ToolCall,
                        Payload = JsonSerializer.Serialize(new { toolName = "write", args = new[] { filePath, "<updated content>" } }, JsonOptions),
                        Status = "Pending"
                    });
                }
                else if (filePath != null)
                {
                    plan.Tasks.Add(new ExecutionTask
                    {
                        SequenceOrder = order++,
                        Type = TaskType.ToolCall,
                        Payload = JsonSerializer.Serialize(new { toolName = "read", args = new[] { filePath } }, JsonOptions),
                        Status = "Pending"
                    });
                    plan.Tasks.Add(new ExecutionTask
                    {
                        SequenceOrder = order++,
                        Type = TaskType.Reasoning,
                        Payload = $"Understand the content and: {goal}",
                        Status = "Pending"
                    });
                }
                else
                {
                    plan.Tasks.Add(new ExecutionTask
                    {
                        SequenceOrder = order++,
                        Type = TaskType.Reasoning,
                        Payload = goal,
                        Status = "Pending"
                    });
                }
            }
            else
            {
                plan.Tasks.Add(new ExecutionTask
                {
                    SequenceOrder = order++,
                    Type = TaskType.Reasoning,
                    Payload = goal,
                    Status = "Pending"
                });
            }

            return plan;
        }
    }
}
