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
        ConfigureResizableEditor(TaskSummaryEditor, TaskSummaryResizeHandle, SummaryHeightKey, 96d);
        ConfigureResizableEditor(TaskInputPathsEditor, TaskInputPathsResizeHandle, InputPathsHeightKey, 120d);
        ConfigureResizableEditor(TaskOutputPathsEditor, TaskOutputPathsResizeHandle, OutputPathsHeightKey, 90d);
        ConfigureResizableEditor(TaskMarkdownEditor, TaskMarkdownResizeHandle, TaskMarkdownHeightKey, 300d);
    }

    public Task LoadAsync(string taskId) => _viewModel.LoadAsync(taskId);

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
                "Type one file per line when you want a stable top-level deliverable under outputs/. Leave it blank if the run-scoped worker history copy is enough."),
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

    private static void ConfigureResizableEditor(Editor editor, View handle, string key, double fallbackHeight)
    {
        editor.HeightRequest = UiStateStore.GetEditorHeight(key, fallbackHeight);

        var session = new EditorResizeSession(editor, key, fallbackHeight);
        var gesture = new PanGestureRecognizer();
        gesture.PanUpdated += (_, args) => HandleEditorResize(session, args);
        handle.GestureRecognizers.Add(gesture);
    }

    private static void HandleEditorResize(EditorResizeSession session, PanUpdatedEventArgs args)
    {
        switch (args.StatusType)
        {
            case GestureStatus.Started:
                session.StartHeight = session.Editor.HeightRequest > 0 ? session.Editor.HeightRequest : session.FallbackHeight;
                break;
            case GestureStatus.Running:
                session.Editor.HeightRequest = UiStateStore.ResizeEditorHeight(
                    session.StartHeight,
                    args.TotalY,
                    session.FallbackHeight);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                session.Editor.HeightRequest = UiStateStore.SaveEditorHeight(
                    session.Key,
                    session.Editor.HeightRequest,
                    session.FallbackHeight);
                break;
        }
    }

    private sealed class EditorResizeSession(Editor editor, string key, double fallbackHeight)
    {
        public Editor Editor { get; } = editor;

        public string Key { get; } = key;

        public double FallbackHeight { get; } = fallbackHeight;

        public double StartHeight { get; set; }
    }
}
