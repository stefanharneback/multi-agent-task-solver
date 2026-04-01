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
