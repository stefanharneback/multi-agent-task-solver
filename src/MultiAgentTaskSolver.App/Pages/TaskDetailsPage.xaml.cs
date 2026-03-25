using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class TaskDetailsPage : ContentPage
{
    private readonly TaskDetailsViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public TaskDetailsPage(TaskDetailsViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _serviceProvider = serviceProvider;
    }

    public Task LoadAsync(string taskId) => _viewModel.LoadAsync(taskId);

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await _viewModel.SaveAsync();
    }

    private async void OnImportFilesClicked(object? sender, EventArgs e)
    {
        var files = await FilePicker.Default.PickMultipleAsync();
        if (files is null)
        {
            return;
        }

        var paths = new List<string>();
        foreach (var file in files)
        {
            if (!string.IsNullOrWhiteSpace(file?.FullPath))
            {
                paths.Add(file.FullPath);
            }
        }

        await _viewModel.ImportFilesAsync(paths);
    }

    private async void OnRunHistoryClicked(object? sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<RunHistoryPage>();
        await page.LoadAsync(_viewModel.TaskId);
        await Shell.Current.Navigation.PushAsync(page);
    }
}
