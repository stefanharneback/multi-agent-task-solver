using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class CreateTaskPage : ContentPage
{
    private readonly CreateTaskViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public CreateTaskPage(CreateTaskViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _serviceProvider = serviceProvider;
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        var createdTaskId = await _viewModel.CreateAsync();
        if (string.IsNullOrWhiteSpace(createdTaskId))
        {
            return;
        }

        var page = _serviceProvider.GetRequiredService<TaskDetailsPage>();
        await page.LoadAsync(createdTaskId);
        await Shell.Current.Navigation.PushAsync(page);
        Shell.Current.Navigation.RemovePage(this);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Shell.Current.Navigation.PopAsync();
    }
}
