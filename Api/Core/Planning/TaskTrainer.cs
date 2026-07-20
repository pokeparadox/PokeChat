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
                return plan;

            var prompt = BuildDecompositionPrompt(goal);
            var response = await Task.Run(() => _llmOrchestrator.GenerateResponse(prompt));

            if (string.IsNullOrWhiteSpace(response))
                return plan;

            plan.Tasks = ParseDecomposition(response);
            return plan;
        }

        public List<ExecutionTask> ParseDecomposition(string llmResponse)
        {
            var tasks = new List<ExecutionTask>();
            var lines = llmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
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
Available tools: shell_command, file_ops, web_search, read_url, mempalace_drawer
Each ToolCall payload must match one of these tool names exactly.
Goal: {goal}
Output ONLY the JSON lines, nothing else.";
        }
    }
}
