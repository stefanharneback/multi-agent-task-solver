using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public sealed class TaskWorkspaceHomePage : ContentPage
{
    private readonly TaskListViewModel _viewModel;
    private readonly Label _workspaceRootLabel;
    private readonly Label _errorLabel;
    private readonly VerticalStackLayout _tasksHost;

    public TaskWorkspaceHomePage(TaskListViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        Title = "Tasks";

        _workspaceRootLabel = new Label
        {
            AutomationId = "WorkspaceRootLabel",
            Style = TryGetStyle("ItalicBodyStyle")
        };
        _workspaceRootLabel.SetBinding(Label.TextProperty, nameof(TaskListViewModel.WorkspaceRootPath));

        _errorLabel = new Label
        {
            AutomationId = "TaskListErrorLabel",
            Style = TryGetStyle("ErrorLabelStyle")
        };
        _errorLabel.SetBinding(Label.TextProperty, nameof(TaskListViewModel.ErrorMessage));

        var createTaskButton = new Button
        {
            AutomationId = "CreateTaskButton",
            Text = "Create Task",
            Command = _viewModel.OpenCreateTaskCommand,
            HorizontalOptions = LayoutOptions.Start
        };
        ApplyStyle(createTaskButton, "PrimaryActionButtonStyle");

        _tasksHost = new VerticalStackLayout
        {
            AutomationId = "TasksCollectionView",
            Spacing = 10
        };

        var workspaceCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = "Workspace root",
                        Style = TryGetStyle("FormFieldTitle")
                    },
                    _workspaceRootLabel,
                    createTaskButton,
                    _errorLabel
                }
            }
        };
        ApplyStyle(workspaceCard, "SurfaceCardStyle");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 16,
                Children =
                {
                    new Label
                    {
                        AutomationId = "TaskListHeadingLabel",
                        Text = "Task workspace",
                        Style = TryGetStyle("PageHeadingStyle")
                    },
                    new Label
                    {
                        Text = "Create tasks, review the workspace root, and open existing task folders.",
                        Style = TryGetStyle("FormFieldHint")
                    },
                    workspaceCard,
                    new Label
                    {
                        Text = "Tasks",
                        Style = TryGetStyle("SectionHeadingStyle")
                    },
                    _tasksHost
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        RenderTasks();
    }

    private void RenderTasks()
    {
        _tasksHost.Children.Clear();

        if (_viewModel.Tasks.Count == 0)
        {
            _tasksHost.Children.Add(new Label
            {
                Text = "No tasks yet. Create the first task to initialize the workspace.",
                Style = TryGetStyle("FormFieldHint")
            });

            return;
        }

        foreach (var task in _viewModel.Tasks)
        {
            var openButton = new Button
            {
                Text = "Open",
                HorizontalOptions = LayoutOptions.Start
            };
            ApplyStyle(openButton, "SecondaryActionButtonStyle");
            openButton.Clicked += async (_, _) => await _viewModel.OpenTaskAsync(task.TaskId);

            var card = new Border
            {
                Padding = 12,
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Label
                        {
                            Text = task.Title,
                            Style = TryGetStyle("CardTitleStyle")
                        },
                        new Label
                        {
                            Text = task.Summary
                        },
                        new Label
                        {
                            Text = task.UpdatedAtText,
                            Style = TryGetStyle("FormFieldHint")
                        },
                        openButton
                    }
                }
            };
            ApplyStyle(card, "SurfaceCardStyle");

            _tasksHost.Children.Add(card);
        }
    }

    private static void ApplyStyle(VisualElement element, string resourceKey)
    {
        if (TryGetStyle(resourceKey) is { } style)
        {
            element.Style = style;
        }
    }

    private static Style? TryGetStyle(string resourceKey)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var style) == true
            && style is Style typedStyle)
        {
            return typedStyle;
        }

        return null;
    }
}
