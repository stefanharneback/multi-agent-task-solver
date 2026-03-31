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
        ApplySavedEditorHeights();
    }

    private void ApplySavedEditorHeights()
    {
        SummaryEditor.HeightRequest = UiStateStore.GetEditorHeight(SummaryHeightKey, SummaryEditor.HeightRequest);
        InputPathsEditor.HeightRequest = UiStateStore.GetEditorHeight(InputPathsHeightKey, InputPathsEditor.HeightRequest);
        OutputPathsEditor.HeightRequest = UiStateStore.GetEditorHeight(OutputPathsHeightKey, OutputPathsEditor.HeightRequest);
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
                "Use a short, specific title. Aim for 3-10 words. It should make sense in the task list without opening the task."),
            "summary" => (
                "Short summary",
                "Write a compact description of the task. State the goal, main constraints, and expected result. One to four short paragraphs is usually enough."),
            "input-paths" => (
                "Input folders",
                "Type one folder per line. These are task-local paths under inputs/. Example: research/articles or inputs/contracts. You can edit them manually as plain text."),
            "output-paths" => (
                "Output files",
                "Type one file path per line. These are task-local paths under outputs/. Example: deliverables/final-report.md or outputs/data/summary.json."),
            "instructions" => (
                "Task instructions",
                "This is the main task prompt. Describe the work to do, how imported material should be used, and what the final output should contain. Markdown works best for longer instructions."),
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
                SummaryEditor.HeightRequest = UiStateStore.ChangeEditorHeight(SummaryHeightKey, SummaryEditor.HeightRequest, direction, 96d);
                break;
            case "input-paths":
                InputPathsEditor.HeightRequest = UiStateStore.ChangeEditorHeight(InputPathsHeightKey, InputPathsEditor.HeightRequest, direction, 120d);
                break;
            case "output-paths":
                OutputPathsEditor.HeightRequest = UiStateStore.ChangeEditorHeight(OutputPathsHeightKey, OutputPathsEditor.HeightRequest, direction, 90d);
                break;
            case "instructions":
                TaskMarkdownEditor.HeightRequest = UiStateStore.ChangeEditorHeight(TaskMarkdownHeightKey, TaskMarkdownEditor.HeightRequest, direction, 320d);
                break;
        }
    }
}
