using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class TaskListViewModelTests
{
    [Fact]
    public async Task LoadAsyncPopulatesWorkspaceAndTasks()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        coordinator.Tasks.AddRange(
        [
            new TaskManifest
            {
                Id = "task-001",
                Title = "First task",
                Summary = "Summary",
                UpdatedAtUtc = new DateTimeOffset(2026, 3, 26, 10, 15, 0, TimeSpan.Zero),
            },
        ]);

        var viewModel = new TaskListViewModel(coordinator, new FakeNavigationService());

        await viewModel.LoadAsync();

        Assert.Equal("C:\\workspace", viewModel.WorkspaceRootPath);
        Assert.Single(viewModel.Tasks);
        Assert.Equal("task-001", viewModel.Tasks[0].TaskId);
    }

    [Fact]
    public async Task OpenCreateTaskAsyncUsesNavigationService()
    {
        var navigation = new FakeNavigationService();
        var viewModel = new TaskListViewModel(new FakeTaskWorkspaceCoordinator(), navigation);

        await viewModel.OpenCreateTaskAsync();

        Assert.Equal(1, navigation.CreateTaskNavigationCount);
    }

    [Fact]
    public async Task OpenTaskAsyncUsesNavigationService()
    {
        var navigation = new FakeNavigationService();
        var viewModel = new TaskListViewModel(new FakeTaskWorkspaceCoordinator(), navigation);

        await viewModel.OpenTaskAsync("task-123");

        var navigationCall = Assert.Single(navigation.TaskDetailsNavigations);
        Assert.Equal("task-123", navigationCall.TaskId);
        Assert.False(navigationCall.ReplaceCurrentPage);
    }
}
