using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class CreateTaskPage : ContentPage
{
    private const string SummaryHeightKey = "page.create.summary.height";
    private const string InputPathsHeightKey = "page.create.inputs.height";
    private const string OutputPathsHeightKey = "page.create.outputs.height";
    private const string TaskMarkdownHeightKey = "page.create.task.height";

    public CreateTaskPage(CreateTaskViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        ConfigureResizableEditor(SummaryEditor, SummaryResizeHandle, SummaryHeightKey, 96d);
        ConfigureResizableEditor(InputPathsEditor, InputPathsResizeHandle, InputPathsHeightKey, 120d);
        ConfigureResizableEditor(OutputPathsEditor, OutputPathsResizeHandle, OutputPathsHeightKey, 90d);
        ConfigureResizableEditor(TaskMarkdownEditor, TaskMarkdownResizeHandle, TaskMarkdownHeightKey, 320d);
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
                "Use a short, specific title. Aim for 3-10 words. It should make sense in the task list without opening the task."),
            "summary" => (
                "Short summary",
                "Write a compact description of the task. State the goal, main constraints, and expected result. One to four short paragraphs is usually enough."),
            "input-paths" => (
                "Input folders",
                "Type one folder per line. These are task-local paths under inputs/. Example: research/articles or inputs/contracts. You can edit them manually as plain text."),
            "output-paths" => (
                "Output files",
                "Type one file path per line when you want a stable top-level deliverable under outputs/. Leave it blank if the run-scoped worker history copy is enough."),
            "instructions" => (
                "Task instructions",
                "This is the main task prompt. Describe the work to do, how imported material should be used, and what the final output should contain. Markdown works best for longer instructions."),
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
