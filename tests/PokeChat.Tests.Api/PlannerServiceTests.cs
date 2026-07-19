using Microsoft.EntityFrameworkCore;
using PokeChat.Api.Core.Planning;
using PokeChat.Data;
using PokeChat.Data.Entities;
using PokeChat.Tests.Shared.Helpers;
using PokeChat.Tools;
using Shouldly;

namespace PokeChat.Tests.Api;

public class PlannerServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly ToolRegistry _toolRegistry;

    public PlannerServiceTests()
    {
        _factory = new TestDbContextFactory();
        _toolRegistry = CreateToolRegistry();
    }

    private static ToolRegistry CreateToolRegistry()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["shell_command"] = new ToolConfig { Enabled = true, TimeoutMs = 5000 },
            ["file_ops"] = new ToolConfig { Enabled = true, TimeoutMs = 5000 }
        };
        return new ToolRegistry(configs);
    }

    private PlannerService CreateService() => new(_factory, _toolRegistry);

    [Fact]
    public async Task PlanAsync_CreatesNewPlan_WhenNoExistingPlan()
    {
        var service = CreateService();
        var plan = await service.PlanAsync("test goal");
        plan.ShouldNotBeNull();
        plan.GoalDescription.ShouldBe("test goal");
        plan.Version.ShouldBe(1);
        plan.IsTemplate.ShouldBeTrue();
        plan.Tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlanAsync_CreatesPlanWithCorrectContextTags()
    {
        var service = CreateService();
        var plan = await service.PlanAsync("refactor the api code");
        plan.ContextTags!.ShouldContain("refactor");
        plan.ContextTags!.ShouldContain("api");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNoTasksMessage_WhenEmptyPlan()
    {
        var service = CreateService();
        var plan = new TaskList { Tasks = new List<ExecutionTask>() };
        var result = await service.ExecuteAsync(plan);
        result.ShouldBe("No tasks found in the plan.");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNoTasksMessage_WhenNullPlan()
    {
        var service = CreateService();
        var result = await service.ExecuteAsync(null!);
        result.ShouldBe("No tasks found in the plan.");
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesReasoningTask()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "reasoning content" }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        var result = await service.ExecuteAsync(plan);
        result.ShouldContain("reasoning content");
        result.ShouldContain("1 completed, 0 failed");
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesTaskStatusToCompleted()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "test" }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        await service.ExecuteAsync(plan);
        var task = await context.Tasks.FirstAsync(t => t.TaskListId == plan.Id);
        task.Status.ShouldBe("Completed");
        task.Result!.ShouldContain("test");
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesSuccessRating()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "test1" },
                new() { SequenceOrder = 2, Type = TaskType.Reasoning, Payload = "test2" }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        await service.ExecuteAsync(plan);
        plan.SuccessRating.ShouldBe(1.0);
        plan.LastUsedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_HandlesTaskFailure()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.ToolCall, Payload = null }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        var result = await service.ExecuteAsync(plan);
        result.ShouldContain("Failed");
        result.ShouldContain("0 completed, 1 failed");
        var task = await context.Tasks.FirstAsync(t => t.TaskListId == plan.Id);
        task.Status.ShouldBe("Failed");
        task.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesMultipleTasksInOrder()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 2, Type = TaskType.Reasoning, Payload = "second" },
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "first" }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        var result = await service.ExecuteAsync(plan);
        var lines = result.Split('\n');
        lines[0].ShouldContain("first");
        lines[1].ShouldContain("Result: first");
        lines[2].ShouldContain("second");
        lines[3].ShouldContain("Result: second");
    }

    [Fact]
    public async Task ExecuteAsync_CalculatesCorrectSuccessRating_WhenPartialFailure()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "success" },
                new() { SequenceOrder = 2, Type = TaskType.ToolCall, Payload = null }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        await service.ExecuteAsync(plan);
        plan.SuccessRating.ShouldBe(0.5);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesToolCallTask()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { toolName = "shell_command", args = new[] { "echo", "hello" } });
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.ToolCall, Payload = payload }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        var result = await service.ExecuteAsync(plan);
        result.ShouldContain("1 completed, 0 failed");
    }

    [Fact]
    public async Task ExecuteAsync_ToolCallWithInvalidToolName_Fails()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { toolName = "nonexistent_tool", args = Array.Empty<string>() });
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.ToolCall, Payload = payload }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        var result = await service.ExecuteAsync(plan);
        result.ShouldContain("0 completed, 1 failed");
        var task = await context.Tasks.FirstAsync(t => t.TaskListId == plan.Id);
        task.Status.ShouldBe("Failed");
    }

    [Fact]
    public async Task AdaptPlanAsync_FillsEmptyReasoningPayload()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = null }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        await service.AdaptPlanAsync(plan, "test goal", "testing");
        var task = await context.Tasks.FirstAsync(t => t.TaskListId == plan.Id);
        task.Payload!.ShouldContain("test goal");
        task.Payload!.ShouldContain("testing");
    }

    [Fact]
    public void ExtractContextTags_ExtractsMultipleTags()
    {
        var service = CreateService();
        var tags = service.ExtractContextTags("refactor the api for testing");
        tags.ShouldContain("refactor");
        tags.ShouldContain("api");
        tags.ShouldContain("testing");
    }

    [Fact]
    public void ExtractContextTags_ReturnsEmpty_WhenNoTagsFound()
    {
        var service = CreateService();
        var tags = service.ExtractContextTags("do something unrelated");
        tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllPlansAsync_ReturnsEmpty_WhenNoPlans()
    {
        var service = CreateService();
        var plans = await service.GetAllPlansAsync();
        plans.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllPlansAsync_ReturnsAllPlans()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        context.TaskLists.Add(new TaskList { GoalDescription = "plan A", Tasks = new List<ExecutionTask>() });
        context.TaskLists.Add(new TaskList { GoalDescription = "plan B", Tasks = new List<ExecutionTask> { new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "step1" } } });
        await context.SaveChangesAsync();
        var plans = await service.GetAllPlansAsync();
        plans.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllPlansAsync_IncludesTasks()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        var plan = new TaskList
        {
            GoalDescription = "test",
            Tasks = new List<ExecutionTask>
            {
                new() { SequenceOrder = 1, Type = TaskType.Reasoning, Payload = "a" },
                new() { SequenceOrder = 2, Type = TaskType.Reasoning, Payload = "b" }
            }
        };
        context.TaskLists.Add(plan);
        await context.SaveChangesAsync();
        var plans = await service.GetAllPlansAsync();
        var loaded = plans.First(p => p.Id == plan.Id);
        loaded.Tasks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllOrdersByCreatedAtDescending()
    {
        using var context = _factory.CreateDbContext();
        var service = CreateService();
        context.TaskLists.Add(new TaskList { GoalDescription = "old", CreatedAt = DateTime.UtcNow.AddDays(-1), Tasks = new List<ExecutionTask>() });
        context.TaskLists.Add(new TaskList { GoalDescription = "new", CreatedAt = DateTime.UtcNow, Tasks = new List<ExecutionTask>() });
        await context.SaveChangesAsync();
        var plans = await service.GetAllPlansAsync();
        plans[0].GoalDescription.ShouldBe("new");
        plans[1].GoalDescription.ShouldBe("old");
    }

    public void Dispose() => _factory?.Dispose();
}
