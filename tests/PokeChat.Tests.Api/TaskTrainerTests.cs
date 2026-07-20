using Microsoft.EntityFrameworkCore;
using PokeChat.Api.Core.Planning;
using PokeChat.Data.Entities;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;

namespace PokeChat.Tests.Api;

public class TaskTrainerTests
{
    [Fact]
    public void ParseDecomposition_ParsesToolCallLines()
    {
        var trainer = new TaskTrainer();
        var input = @"{""type"":""ToolCall"",""toolName"":""file_ops"",""args"":[""read"",""AGENTS.md""]}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(1);
        tasks[0].Type.ShouldBe(TaskType.ToolCall);
        tasks[0].Payload.ShouldContain("file_ops");
        tasks[0].Payload.ShouldContain("AGENTS.md");
        tasks[0].SequenceOrder.ShouldBe(1);
    }

    [Fact]
    public void ParseDecomposition_ParsesReasoningLines()
    {
        var trainer = new TaskTrainer();
        var input = @"{""type"":""Reasoning"",""payload"":""Analyze the existing architecture""}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(1);
        tasks[0].Type.ShouldBe(TaskType.Reasoning);
        tasks[0].Payload.ShouldBe("Analyze the existing architecture");
    }

    [Fact]
    public void ParseDecomposition_ParsesMultipleLines()
    {
        var trainer = new TaskTrainer();
        var input = @"{""type"":""ToolCall"",""toolName"":""file_ops"",""args"":[""read"",""file.cs""]}
{""type"":""Reasoning"",""payload"":""Understand the code structure""}
{""type"":""ToolCall"",""toolName"":""file_ops"",""args"":[""update"",""file.cs"",""new content""]}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(3);
        tasks[0].Type.ShouldBe(TaskType.ToolCall);
        tasks[1].Type.ShouldBe(TaskType.Reasoning);
        tasks[2].Type.ShouldBe(TaskType.ToolCall);
        tasks[0].SequenceOrder.ShouldBe(1);
        tasks[1].SequenceOrder.ShouldBe(2);
        tasks[2].SequenceOrder.ShouldBe(3);
    }

    [Fact]
    public void ParseDecomposition_SkipsNonJsonLines()
    {
        var trainer = new TaskTrainer();
        var input = @"Here is the plan:
{""type"":""Reasoning"",""payload"":""Do something""}
And some more text
{""type"":""ToolCall"",""toolName"":""shell_command"",""args"":[""echo"",""hello""]}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(2);
        tasks[0].Type.ShouldBe(TaskType.Reasoning);
        tasks[1].Type.ShouldBe(TaskType.ToolCall);
    }

    [Fact]
    public void ParseDecomposition_SkipsMalformedJson()
    {
        var trainer = new TaskTrainer();
        var input = @"{""type"":""Reasoning"",""payload"":""valid""}
{not valid json}
{""type"":""ToolCall"",""toolName"":""file_ops"",""args"":[""read""]}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseDecomposition_SkipsLinesWithoutType()
    {
        var trainer = new TaskTrainer();
        var input = @"{""toolName"":""file_ops"",""args"":[""read""]}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.ShouldBeEmpty();
    }

    [Fact]
    public void ParseDecomposition_SkipsMarkdownFences()
    {
        var trainer = new TaskTrainer();
        var input = @"```json
{""type"":""Reasoning"",""payload"":""do stuff""}
```";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(1);
        tasks[0].Type.ShouldBe(TaskType.Reasoning);
    }

    [Fact]
    public void ParseDecomposition_HandlesEmptyInput()
    {
        var trainer = new TaskTrainer();
        var tasks = trainer.ParseDecomposition("");
        tasks.ShouldBeEmpty();
    }

    [Fact]
    public void ParseDecomposition_SetsCorrectStatus()
    {
        var trainer = new TaskTrainer();
        var input = @"{""type"":""Reasoning"",""payload"":""step one""}";
        var tasks = trainer.ParseDecomposition(input);
        tasks[0].Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task DecomposeGoalAsync_ReturnsEmptyTasks_WhenNoLlm()
    {
        var trainer = new TaskTrainer(null);
        var plan = await trainer.DecomposeGoalAsync("test goal");
        plan.ShouldNotBeNull();
        plan.GoalDescription.ShouldBe("test goal");
        plan.IsTrained.ShouldBeTrue();
        plan.Tasks.ShouldBeEmpty();
    }

    [Fact]
    public void ParseDecomposition_CaseInsensitiveType()
    {
        var trainer = new TaskTrainer();
        var input = @"{""type"":""reasoning"",""payload"":""lowercase type""}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(1);
        tasks[0].Type.ShouldBe(TaskType.Reasoning);
    }

    [Fact]
    public void ParseDecomposition_ToolCallWithEmptyArgs()
    {
        var trainer = new TaskTrainer();
        var input = @"{""type"":""ToolCall"",""toolName"":""web_search""}";
        var tasks = trainer.ParseDecomposition(input);
        tasks.Count.ShouldBe(1);
        tasks[0].Type.ShouldBe(TaskType.ToolCall);
        tasks[0].Payload.ShouldContain("web_search");
    }

    [Fact]
    public async Task TrainFromLLMAsync_CreatesTrainedPlan()
    {
        using var factory = new TestDbContextFactory();
        var toolRegistry = CreateToolRegistry();
        var trainer = new TaskTrainer();
        var service = new PlannerService(factory, toolRegistry, taskTrainer: trainer);
        var plan = await service.TrainFromLLMAsync("update documentation");
        plan.ShouldNotBeNull();
        plan.GoalDescription.ShouldBe("update documentation");
        plan.IsTrained.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveTrainedTaskAsync_SavesToDatabase()
    {
        using var factory = new TestDbContextFactory();
        var toolRegistry = CreateToolRegistry();
        var trainer = new TaskTrainer();
        var service = new PlannerService(factory, toolRegistry, taskTrainer: trainer);
        var plan = new TaskList
        {
            GoalDescription = "test training",
            IsTrained = true,
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "step one" }
            }
        };
        await service.SaveTrainedTaskAsync(plan);
        plan.Id.ShouldBeGreaterThan(0);
        using var context = factory.CreateDbContext();
        var saved = context.TaskLists.Include(t => t.Tasks).First(t => t.Id == plan.Id);
        saved.IsTrained.ShouldBeTrue();
        saved.Tasks.Count.ShouldBe(1);
    }

    private static PokeChat.Tools.ToolRegistry CreateToolRegistry()
    {
        var configs = new Dictionary<string, PokeChat.Tools.ToolConfig>
        {
            ["shell_command"] = new() { Enabled = true, TimeoutMs = 5000 },
            ["file_ops"] = new() { Enabled = true, TimeoutMs = 5000 }
        };
        return new PokeChat.Tools.ToolRegistry(configs);
    }
}
