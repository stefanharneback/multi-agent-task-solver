using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task BrowseWorkspaceFolderAsyncUsesPickedPath()
    {
        var folderPicker = new FakeFolderPickerService
        {
            SelectedPath = "D:\\Tasks",
        };
        var viewModel = new SettingsViewModel(new FakeTaskWorkspaceCoordinator(), folderPicker);

        await viewModel.BrowseWorkspaceFolderAsync();

        Assert.Equal("D:\\Tasks", viewModel.WorkspaceRootPath);
    }

    [Fact]
    public async Task SaveAsyncPersistsTrimmedSettingsAndToken()
    {
        var coordinator = new FakeTaskWorkspaceCoordinator();
        var viewModel = new SettingsViewModel(coordinator, new FakeFolderPickerService())
        {
            WorkspaceRootPath = " C:\\Tasks ",
            OpenAiGatewayBaseUrl = " https://gateway.example.test ",
            OpenAiBearerToken = "secret-token",
        };

        await viewModel.SaveAsync();

        var saved = Assert.Single(coordinator.SavedSettings);
        Assert.Equal("C:\\Tasks", saved.Settings.WorkspaceRootPath);
        Assert.Equal("https://gateway.example.test", saved.Settings.OpenAiGatewayBaseUrl);
        Assert.Equal("secret-token", saved.BearerToken);
    }
}
