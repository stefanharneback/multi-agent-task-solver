using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Core;

public static class WorkflowNames
{
    public static string GetStorageName(this TaskRunKind kind)
    {
        return kind switch
        {
            TaskRunKind.TaskReview => "task-review",
            TaskRunKind.Worker => "worker",
            TaskRunKind.Critic => "critic",
            TaskRunKind.UserDecision => "user-decision",
            TaskRunKind.Evaluation => "evaluation",
            _ => "run",
        };
    }

    public static string GetStorageName(this TaskStepType stepType)
    {
        return stepType switch
        {
            TaskStepType.TaskReview => "task-review",
            TaskStepType.Worker => "worker",
            TaskStepType.Critic => "critic",
            TaskStepType.UserDecision => "user-decision",
            TaskStepType.Evaluation => "evaluation",
            _ => "step",
        };
    }

    public static string GetDisplayName(this TaskRunKind kind)
    {
        return kind switch
        {
            TaskRunKind.TaskReview => "Task review",
            TaskRunKind.Worker => "Worker",
            TaskRunKind.Critic => "Critic",
            TaskRunKind.UserDecision => "User decision",
            TaskRunKind.Evaluation => "Evaluation",
            _ => "Run",
        };
    }

    public static string GetDisplayName(this TaskRunStatus status)
    {
        return status switch
        {
            TaskRunStatus.Planned => "Planned",
            TaskRunStatus.Running => "Running",
            TaskRunStatus.Completed => "Completed",
            TaskRunStatus.Failed => "Failed",
            TaskRunStatus.Cancelled => "Cancelled",
            TaskRunStatus.Superseded => "Superseded",
            _ => "Unknown",
        };
    }

    public static string GetDisplayName(this TaskStepStatus status)
    {
        return status switch
        {
            TaskStepStatus.Planned => "Planned",
            TaskStepStatus.Running => "Running",
            TaskStepStatus.Completed => "Completed",
            TaskStepStatus.Failed => "Failed",
            TaskStepStatus.Cancelled => "Cancelled",
            _ => "Unknown",
        };
    }

    public static string GetDisplayName(this TaskLifecycleState state)
    {
        return state switch
        {
            TaskLifecycleState.Draft => "Draft",
            TaskLifecycleState.ReviewReady => "Review Ready",
            TaskLifecycleState.UnderReview => "Under Review",
            TaskLifecycleState.WorkApproved => "Work Approved",
            TaskLifecycleState.Working => "Working",
            TaskLifecycleState.UnderCritique => "Under Critique",
            TaskLifecycleState.NeedsRework => "Needs Rework",
            TaskLifecycleState.Done => "Done",
            TaskLifecycleState.Archived => "Archived",
            _ => "Unknown",
        };
    }
}
