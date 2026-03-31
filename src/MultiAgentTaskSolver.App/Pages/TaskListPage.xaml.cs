namespace MultiAgentTaskSolver.App.Pages;

public sealed class TaskListPage : ContentPage
{
    public TaskListPage()
    {
        Title = "Tasks";
        Content = new VerticalStackLayout
        {
            Padding = 20,
            Children =
            {
                new Label
                {
                    AutomationId = "TaskListHeadingLabel",
                    Text = "Task workspace"
                }
            }
        };
    }
}
