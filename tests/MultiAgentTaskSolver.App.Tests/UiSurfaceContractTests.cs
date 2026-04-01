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
        Assert.Contains("Leave blank to only keep run-scoped history copies", xaml, StringComparison.Ordinal);
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
        var appCode = ReadRepoFile("src", "MultiAgentTaskSolver.App", "App.xaml.cs");

        Assert.Contains("Style TargetType=\"Page\"", styles, StringComparison.Ordinal);
        Assert.Contains("EditorPanelStyle", styles, StringComparison.Ordinal);
        Assert.Contains("EditorResizeHandleStyle", styles, StringComparison.Ordinal);
        Assert.Contains("EditorResizeGripStyle", styles, StringComparison.Ordinal);
        Assert.Contains("BodyStrongStyle", styles, StringComparison.Ordinal);
        Assert.Contains("ItalicBodyStyle", styles, StringComparison.Ordinal);
        Assert.Contains("CaptionLabelStyle", styles, StringComparison.Ordinal);
        Assert.Contains("StatusLabelStyle", styles, StringComparison.Ordinal);
        Assert.Contains("ErrorLabelStyle", styles, StringComparison.Ordinal);
        Assert.Contains("#512BD4", colors, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<Color x:Key=\"Canvas\">#0F0B17</Color>", colors, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<Color x:Key=\"Surface\">#171222</Color>", colors, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<Color x:Key=\"FieldBackground\">#1D182B</Color>", colors, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StageCurrentBackground", colors, StringComparison.Ordinal);
        Assert.Contains("StageCompletedBackground", colors, StringComparison.Ordinal);
        Assert.Contains("UserAppTheme = AppTheme.Dark;", appCode, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedPagesNoLongerCarryInlineThemeOverrides()
    {
        var settingsPage = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "SettingsPage.xaml");
        var taskDetailsPage = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "TaskDetailsPage.xaml");
        var runHistoryPage = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "RunHistoryPage.xaml");

        Assert.DoesNotContain("FontAttributes=", settingsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=", settingsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("TextColor=", settingsPage, StringComparison.Ordinal);

        Assert.DoesNotContain("FontAttributes=", taskDetailsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=", taskDetailsPage, StringComparison.Ordinal);
        Assert.DoesNotContain("TextColor=", taskDetailsPage, StringComparison.Ordinal);

        Assert.DoesNotContain("FontAttributes=", runHistoryPage, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize=", runHistoryPage, StringComparison.Ordinal);
        Assert.DoesNotContain("TextColor=", runHistoryPage, StringComparison.Ordinal);
        Assert.DoesNotContain("AppThemeBinding", runHistoryPage, StringComparison.Ordinal);
    }

    [Fact]
    public void CodeBuiltViewsUseSharedStylesInsteadOfHardcodedThemeValues()
    {
        var settingsHomeView = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "SettingsHomeView.cs");
        var taskWorkspaceHomePage = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "TaskWorkspaceHomePage.cs");
        var taskWorkspaceHomeView = ReadRepoFile("src", "MultiAgentTaskSolver.App", "Pages", "TaskWorkspaceHomeView.cs");

        Assert.DoesNotContain("Color.FromArgb", settingsHomeView, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb", taskWorkspaceHomePage, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb", taskWorkspaceHomeView, StringComparison.Ordinal);
        Assert.Contains("ItalicBodyStyle", settingsHomeView + taskWorkspaceHomePage + taskWorkspaceHomeView, StringComparison.Ordinal);
        Assert.Contains("ErrorLabelStyle", settingsHomeView + taskWorkspaceHomePage + taskWorkspaceHomeView, StringComparison.Ordinal);
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
