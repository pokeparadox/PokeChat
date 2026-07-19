using System;
using System.Collections.Generic;

namespace PokeChat.Data.Entities
{
    public class TaskList
    {
        public int Id { get; set; }
        public string GoalDescription { get; set; } = string.Empty;
        public string? ContextTags { get; set; }
        public double SuccessRating { get; set; }
        public int Version { get; set; }
        public bool IsTemplate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        public List<ExecutionTask> Tasks { get; set; } = new();
    }

    public class ExecutionTask
    {
        public int Id { get; set; }
        public int TaskListId { get; set; }
        public int SequenceOrder { get; set; }
        public TaskType Type { get; set; }
        public string? Payload { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public TaskList? TaskList { get; set; }
    }

    public enum TaskType
    {
        ToolCall,
        SubPlan,
        Reasoning
    }
}