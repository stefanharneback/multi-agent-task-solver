namespace MultiAgentTaskSolver.App.Tests;

public sealed class UiSurfaceContractTests
{
    [Fact]
    public void CreateTaskPageUsesResizeHandlesAndNoLongerInjectsOutputBrowseShortcut()
    {
        var xaml = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "CreateTaskPage.xaml");

        Assert.DoesNotContain("Text=\"A+\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"A-\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTaskAddOutputFolderButton", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateTaskSummaryResizeHandle", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateTaskInputPathsResizeHandle", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateTaskOutputPathsResizeHandle", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateTaskMarkdownResizeHandle", xaml, StringComparison.Ordinal);
        Assert.Contains("Leave this blank if you only want run-history copies", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskDetailsPageUsesResizeHandlesInsteadOfFontButtons()
    {
        var xaml = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "TaskDetailsPage.xaml");

        Assert.DoesNotContain("Text=\"A+\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"A-\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TaskSummaryResizeHandle", xaml, StringComparison.Ordinal);
        Assert.Contains("TaskInputPathsResizeHandle", xaml, StringComparison.Ordinal);
        Assert.Contains("TaskOutputPathsResizeHandle", xaml, StringComparison.Ordinal);
        Assert.Contains("TaskMarkdownResizeHandle", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedStylesDefineUnifiedPagePaletteAndResizeHandleTokens()
    {
        var styles = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Resources", "Styles", "Styles.xaml");
        var colors = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Resources", "Styles", "Colors.xaml");

        Assert.Contains("Style TargetType=\"Page\"", styles, StringComparison.Ordinal);
        Assert.Contains("EditorPanelStyle", styles, StringComparison.Ordinal);
        Assert.Contains("EditorResizeHandleStyle", styles, StringComparison.Ordinal);
        Assert.Contains("EditorResizeGripStyle", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("#512BD4", colors, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Canvas", colors, StringComparison.Ordinal);
        Assert.Contains("Surface", colors, StringComparison.Ordinal);
        Assert.Contains("FieldBackground", colors, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MultiAgentTaskSolver.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Repository root could not be resolved from the test output directory.");
        }

        var path = Path.Combine([directory.FullName, .. relativeSegments]);
        return File.ReadAllText(path);
    }
}
