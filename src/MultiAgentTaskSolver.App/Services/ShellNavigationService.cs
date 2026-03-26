using MultiAgentTaskSolver.App.Pages;

namespace MultiAgentTaskSolver.App.Services;

public sealed class ShellNavigationService : IAppNavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public ShellNavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task GoToCreateTaskAsync()
    {
        var page = _serviceProvider.GetRequiredService<CreateTaskPage>();
        await Shell.Current.Navigation.PushAsync(page);
    }

    public async Task GoToTaskDetailsAsync(string taskId, bool replaceCurrentPage = false)
    {
        var page = _serviceProvider.GetRequiredService<TaskDetailsPage>();
        await page.LoadAsync(taskId);
        await Shell.Current.Navigation.PushAsync(page);

        if (replaceCurrentPage)
        {
            var navigation = Shell.Current.Navigation;
            if (navigation.NavigationStack.Count >= 2)
            {
                navigation.RemovePage(navigation.NavigationStack[^2]);
            }
        }
    }

    public async Task GoToRunHistoryAsync(string taskId)
    {
        var page = _serviceProvider.GetRequiredService<RunHistoryPage>();
        await page.LoadAsync(taskId);
        await Shell.Current.Navigation.PushAsync(page);
    }

    public async Task GoBackAsync()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}
