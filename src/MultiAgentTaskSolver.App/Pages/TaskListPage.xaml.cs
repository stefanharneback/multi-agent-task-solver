using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class TaskListPage : ContentPage
{
    private readonly TaskListViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public TaskListPage(TaskListViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _serviceProvider = serviceProvider;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnCreateTaskClicked(object? sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<CreateTaskPage>();
        await Shell.Current.Navigation.PushAsync(page);
    }

    private async void OnTaskSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not TaskListItemViewModel selectedTask)
        {
            return;
        }

        TasksCollectionView.SelectedItem = null;

        var page = _serviceProvider.GetRequiredService<TaskDetailsPage>();
        await page.LoadAsync(selectedTask.TaskId);
        await Shell.Current.Navigation.PushAsync(page);
    }
}
