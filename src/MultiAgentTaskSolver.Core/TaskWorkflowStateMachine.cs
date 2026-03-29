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

    public static TaskLifecycleState ApproveReviewedTask(TaskLifecycleState currentState)
    {
        return currentState switch
        {
            TaskLifecycleState.ReviewReady => TaskLifecycleState.WorkApproved,
            _ => throw new InvalidOperationException($"Task approval cannot start from '{currentState.GetDisplayName()}'."),
        };
    }

    public static TaskLifecycleState ReviseReviewedTask(TaskLifecycleState currentState)
    {
        return currentState switch
        {
            TaskLifecycleState.ReviewReady => TaskLifecycleState.Draft,
            _ => throw new InvalidOperationException($"Task revision cannot start from '{currentState.GetDisplayName()}'."),
        };
    }

    public static TaskLifecycleState StartWorker(TaskLifecycleState currentState)
    {
        return currentState switch
        {
            TaskLifecycleState.WorkApproved or TaskLifecycleState.Working or TaskLifecycleState.NeedsRework => TaskLifecycleState.Working,
            _ => throw new InvalidOperationException($"Worker execution cannot start from '{currentState.GetDisplayName()}'."),
        };
    }

    public static TaskLifecycleState CompleteWorker(TaskLifecycleState currentState)
    {
        return currentState switch
        {
            TaskLifecycleState.Working => TaskLifecycleState.Working,
            _ => throw new InvalidOperationException($"Worker execution cannot complete from '{currentState.GetDisplayName()}'."),
        };
    }

    public static TaskLifecycleState FailWorker(TaskLifecycleState currentState, TaskLifecycleState previousState)
    {
        return currentState switch
        {
            TaskLifecycleState.Working => previousState,
            _ => currentState,
        };
    }
}
