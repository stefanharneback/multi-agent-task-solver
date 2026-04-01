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

        TaskDefinitionSection.Initialize(defaultExpanded: true);
        InputsOutputsSection.Initialize(defaultExpanded: true);
        ReviewAgentSection.Initialize(defaultExpanded: false);
        WorkerAgentSection.Initialize(defaultExpanded: false);
        FileImportSection.Initialize(defaultExpanded: false);
        ArtifactsFilesSection.Initialize(defaultExpanded: false);
    }

    public Task LoadAsync(string taskId) => _viewModel.LoadAsync(taskId);

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
