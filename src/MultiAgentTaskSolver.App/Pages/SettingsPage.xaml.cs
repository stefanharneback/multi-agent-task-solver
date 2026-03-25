using CommunityToolkit.Maui.Storage;
using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnBrowseWorkspaceClicked(object? sender, EventArgs e)
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (result.IsSuccessful && !string.IsNullOrWhiteSpace(result.Folder?.Path))
        {
            _viewModel.WorkspaceRootPath = result.Folder.Path;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await _viewModel.SaveAsync();
    }
}
