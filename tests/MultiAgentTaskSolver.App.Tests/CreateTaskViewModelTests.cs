using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class CreateTaskViewModelTests
{
    [Fact]
    public async Task CreateAsyncCreatesTaskAndNavigatesToDetails()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator
        {
            CreateTaskHandler = request => TestData.CreateSnapshot("task-new", request.Title, request.Summary, TaskLifecycleState.Draft),
        };
        var navigation = new FakeNavigationService();
        var viewModel = new CreateTaskViewModel(coordinator, navigation)
        {
            Title = "  New task  ",
            Summary = "Summary",
        };

        await viewModel.CreateAsync();

        var navigationCall = Assert.Single(navigation.TaskDetailsNavigations);
        Assert.Equal("task-new", navigationCall.TaskId);
        Assert.True(navigationCall.ReplaceCurrentPage);
    }

    [Fact]
    public async Task CancelAsyncGoesBack()
    {
        var navigation = new FakeNavigationService();
        var viewModel = new CreateTaskViewModel(new FakeTaskWorkspaceCoordinator(), navigation);

        await viewModel.CancelAsync();

        Assert.Equal(1, navigation.GoBackCount);
    }
}
