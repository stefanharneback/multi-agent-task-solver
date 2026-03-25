using MultiAgentTaskSolver.App.Pages;

namespace MultiAgentTaskSolver.App;

public partial class AppShell : Shell
{
    public AppShell(TaskListPage taskListPage, SettingsPage settingsPage)
    {
        InitializeComponent();

        Items.Add(CreateFlyoutItem("Tasks", "tasks", taskListPage));
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
