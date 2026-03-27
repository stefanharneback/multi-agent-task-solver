using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Core;

public static class TaskWorkflowStateMachine
{
    public static TaskLifecycleState StartTaskReview(TaskLifecycleState currentState)
    {
        return currentState switch
        {
            TaskLifecycleState.Draft or TaskLifecycleState.ReviewReady or TaskLifecycleState.NeedsRework => TaskLifecycleState.UnderReview,
            _ => throw new InvalidOperationException($"Task review cannot start from '{currentState.GetDisplayName()}'."),
        };
    }

    public static TaskLifecycleState CompleteTaskReview(TaskLifecycleState currentState)
    {
        return currentState switch
        {
            TaskLifecycleState.UnderReview => TaskLifecycleState.ReviewReady,
            _ => throw new InvalidOperationException($"Task review cannot complete from '{currentState.GetDisplayName()}'."),
        };
    }

    public static TaskLifecycleState FailTaskReview(TaskLifecycleState currentState)
    {
        return currentState switch
        {
            TaskLifecycleState.UnderReview => TaskLifecycleState.Draft,
            _ => currentState,
        };
    }

    public static TaskLifecycleState FailTaskReview(TaskLifecycleState currentState, TaskLifecycleState previousState)
    {
        return currentState switch
        {
            TaskLifecycleState.UnderReview => previousState,
            _ => currentState,
        };
    }
}
