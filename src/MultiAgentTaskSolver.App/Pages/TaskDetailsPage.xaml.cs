using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class TaskDetailsPage : ContentPage
{
    private const string SummaryHeightKey = "page.details.summary.height";
    private const string InputPathsHeightKey = "page.details.inputs.height";
    private const string OutputPathsHeightKey = "page.details.outputs.height";
    private const string TaskMarkdownHeightKey = "page.details.task.height";

    private readonly TaskDetailsViewModel _viewModel;

    public TaskDetailsPage(TaskDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        ApplySavedEditorHeights();
    }

    public Task LoadAsync(string taskId) => _viewModel.LoadAsync(taskId);

    private void ApplySavedEditorHeights()
    {
        TaskSummaryEditor.HeightRequest = UiStateStore.GetEditorHeight(SummaryHeightKey, TaskSummaryEditor.HeightRequest);
        TaskInputPathsEditor.HeightRequest = UiStateStore.GetEditorHeight(InputPathsHeightKey, TaskInputPathsEditor.HeightRequest);
        TaskOutputPathsEditor.HeightRequest = UiStateStore.GetEditorHeight(OutputPathsHeightKey, TaskOutputPathsEditor.HeightRequest);
        TaskMarkdownEditor.HeightRequest = UiStateStore.GetEditorHeight(TaskMarkdownHeightKey, TaskMarkdownEditor.HeightRequest);
    }

    private async void OnHelpButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string helpKey)
        {
            return;
        }

        var (title, message) = helpKey switch
        {
            "title" => (
                "Task title",
                "Keep the title short and specific. It should describe the task clearly in the list view."),
            "summary" => (
                "Short summary",
                "Use this as the quick overview. Keep it compact and practical so the current state of the task is easy to understand."),
            "instructions" => (
                "Task instructions",
                "This is the full task definition. Explain the expected work, what inputs matter, what to ignore, and how the outputs should be structured."),
            "input-paths" => (
                "Input folders",
                "Type one folder per line. These are task-local paths under inputs/. You can edit them directly as plain text."),
            "output-paths" => (
                "Output files",
                "Type one file per line. These are task-local paths under outputs/. The worker uses these declared targets when it writes results."),
            "review-model" => (
                "Review model",
                "Choose the model used by the review step. Use a stronger model when the task is ambiguous or high risk."),
            "worker-model" => (
                "Worker model",
                "Choose the model used to produce the actual output. Match this to the task complexity and cost you want."),
            "import-destination" => (
                "Import destination",
                "Choose which declared input folder should receive imported files or folders. Imports are copied into the task workspace."),
            _ => ("Field help", "No additional help is available for this field yet."),
        };

        await DisplayAlertAsync(title, message, "Close");
    }

    private void OnEditorHeightButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string parameter)
        {
            return;
        }

        var parts = parameter.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var direction))
        {
            return;
        }

        switch (parts[0])
        {
            case "summary":
                TaskSummaryEditor.HeightRequest = UiStateStore.ChangeEditorHeight(SummaryHeightKey, TaskSummaryEditor.HeightRequest, direction, 96d);
                break;
            case "input-paths":
                TaskInputPathsEditor.HeightRequest = UiStateStore.ChangeEditorHeight(InputPathsHeightKey, TaskInputPathsEditor.HeightRequest, direction, 120d);
                break;
            case "output-paths":
                TaskOutputPathsEditor.HeightRequest = UiStateStore.ChangeEditorHeight(OutputPathsHeightKey, TaskOutputPathsEditor.HeightRequest, direction, 90d);
                break;
            case "instructions":
                TaskMarkdownEditor.HeightRequest = UiStateStore.ChangeEditorHeight(TaskMarkdownHeightKey, TaskMarkdownEditor.HeightRequest, direction, 300d);
                break;
        }
    }
}
