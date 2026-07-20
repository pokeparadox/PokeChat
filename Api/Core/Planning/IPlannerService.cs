using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PokeChat.Data.Entities;

namespace PokeChat.Api.Core.Planning
{
    public interface IPlannerService
    {
        Task<TaskList> PlanAsync(string goal);
        Task<TaskList> TrainFromLLMAsync(string goal);
        Task SaveTrainedTaskAsync(TaskList plan);
        Task<string> ExecuteAsync(TaskList taskList);
        Task AdaptPlanAsync(TaskList plan, string goal, string contextTags);
        Task<List<TaskList>> GetAllPlansAsync();
    }
}