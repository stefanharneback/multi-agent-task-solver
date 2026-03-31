using MultiAgentTaskSolver.App.Services;

namespace MultiAgentTaskSolver.App.Tests;

public sealed class UiStateStoreTests
{
    [Theory]
    [InlineData(96d, -400d, 96d, 72d)]
    [InlineData(96d, 24d, 96d, 120d)]
    [InlineData(960d, 400d, 96d, 960d)]
    [InlineData(0d, 40d, 120d, 160d)]
    public void ResizeEditorHeightClampsAndFallsBackToDefault(double baselineHeight, double deltaY, double fallbackHeight, double expectedHeight)
    {
        var nextHeight = UiStateStore.ResizeEditorHeight(baselineHeight, deltaY, fallbackHeight);

        Assert.Equal(expectedHeight, nextHeight);
    }
}
