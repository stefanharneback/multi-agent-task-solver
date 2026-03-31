using MultiAgentTaskSolver.App.Pages;

namespace MultiAgentTaskSolver.App;

public partial class AppShell : Shell
{
    public AppShell(TaskWorkspaceHomeView taskWorkspaceHomeView, SettingsHomeView settingsHomeView)
    {
        InitializeComponent();

        var taskWorkspacePage = new ContentPage
        {
            Title = "Tasks",
            Content = taskWorkspaceHomeView
        };

        taskWorkspacePage.Appearing += async (_, _) => await taskWorkspaceHomeView.LoadAsync();

        var settingsPage = new ContentPage
        {
            Title = "Settings",
            Content = settingsHomeView
        };

        settingsPage.Appearing += async (_, _) => await settingsHomeView.LoadAsync();

        Items.Add(CreateFlyoutItem("Tasks", "tasks", taskWorkspacePage));
        Items.Add(CreateFlyoutItem("Settings", "settings", settingsPage));
    }

    private static FlyoutItem CreateFlyoutItem(string title, string route, Page content)
    {
        var flyoutItem = new FlyoutItem
        {
            Title = title,
            Route = route,
        };

        flyoutItem.Items.Add(new ShellContent
        {
            Title = title,
            Route = route,
            Content = content,
        });

        return flyoutItem;
    }
}
