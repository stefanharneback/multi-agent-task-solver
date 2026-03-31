using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class CreateTaskViewModelTests
{
    [Fact]
    public async Task CreateAsyncCreatesTaskAndNavigatesToDetails()
    {
        CreateTaskRequest? capturedRequest = null;
        var coordinator = new FakeTaskWorkspaceCoordinator
        {
            CreateTaskHandler = request =>
            {
                capturedRequest = request;
                return TestData.CreateSnapshot("task-new", request.Title, request.Summary, TaskLifecycleState.Draft);
            },
        };
        var navigation = new FakeNavigationService();
        var viewModel = new CreateTaskViewModel(coordinator, navigation)
        {
            Title = "  New task  ",
            Summary = "Summary",
            InputPathsText = "research/articles\ninputs/contracts",
            OutputPathsText = "deliverables/final-report.md",
        };

        await viewModel.CreateAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(["inputs/research/articles", "inputs/contracts"], capturedRequest!.InputPaths);
        Assert.Equal(["outputs/deliverables/final-report.md"], capturedRequest.OutputPaths);
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

    [Fact]
    public async Task AddInputFolderAsyncAppendsNormalizedTaskRelativeFolder()
    {
        var folderPicker = new FakeFolderPickerService
        {
            SelectedPath = "C:\\temp\\Contracts",
        };
        var viewModel = new CreateTaskViewModel(new FakeTaskWorkspaceCoordinator(), new FakeNavigationService(), folderPicker);

        await viewModel.AddInputFolderAsync();

        Assert.Equal("inputs/Contracts", viewModel.InputPathsText);
    }

    [Fact]
    public async Task AddOutputFolderAsyncAppendsDefaultWorkerOutputFileUnderSelectedFolder()
    {
        var folderPicker = new FakeFolderPickerService
        {
            SelectedPath = "C:\\temp\\Deliverables",
        };
        var viewModel = new CreateTaskViewModel(new FakeTaskWorkspaceCoordinator(), new FakeNavigationService(), folderPicker);

        await viewModel.AddOutputFolderAsync();

        Assert.Equal("outputs/Deliverables/worker-output.md", viewModel.OutputPathsText);
    }
}
