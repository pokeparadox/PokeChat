using System.Collections.Generic;
using System.Threading.Tasks;
using PokeChat.Data.Entities;

namespace PokeChat.Api.Core.Planning
{
    public interface ITaskTrainer
    {
        Task<TaskList> DecomposeGoalAsync(string goal);
        List<ExecutionTask> ParseDecomposition(string llmResponse);
    }
}
