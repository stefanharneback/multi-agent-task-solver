using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class RunHistoryPage : ContentPage
{
    private readonly RunHistoryViewModel _viewModel;

    public RunHistoryPage(RunHistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public Task LoadAsync(string taskId) => _viewModel.LoadAsync(taskId);
}
