using Microsoft.Maui.Storage;

namespace MultiAgentTaskSolver.App.Services;

internal static class UiStateStore
{
    private const double MinimumEditorHeight = 72d;
    private const double MaximumEditorHeight = 960d;

    public static double GetEditorHeight(string key, double fallbackHeight)
    {
        return ClampEditorHeight(Preferences.Default.Get(key, fallbackHeight));
    }

    public static double ResizeEditorHeight(double baselineHeight, double deltaY, double fallbackHeight)
    {
        var baseline = baselineHeight > 0 ? baselineHeight : fallbackHeight;
        return ClampEditorHeight(baseline + deltaY);
    }

    public static double SaveEditorHeight(string key, double currentHeight, double fallbackHeight)
    {
        var height = ClampEditorHeight(currentHeight > 0 ? currentHeight : fallbackHeight);
        Preferences.Default.Set(key, height);
        return height;
    }

    public static WindowLayout GetWindowLayout()
    {
        return new WindowLayout(
            Preferences.Default.Get("ui.window.x", 80),
            Preferences.Default.Get("ui.window.y", 40),
            Preferences.Default.Get("ui.window.width", 1440),
            Preferences.Default.Get("ui.window.height", 960));
    }

    public static void SaveWindowLayout(int x, int y, int width, int height)
    {
        if (width < 640 || height < 480)
        {
            return;
        }

        Preferences.Default.Set("ui.window.x", x);
        Preferences.Default.Set("ui.window.y", y);
        Preferences.Default.Set("ui.window.width", width);
        Preferences.Default.Set("ui.window.height", height);
    }

    public static bool GetSectionExpanded(string key, bool fallback)
    {
        return Preferences.Default.Get(key, fallback);
    }

    public static void SaveSectionExpanded(string key, bool value)
    {
        Preferences.Default.Set(key, value);
    }

    internal static double ClampEditorHeight(double height)
    {
        return Math.Clamp(height, MinimumEditorHeight, MaximumEditorHeight);
    }

    internal readonly record struct WindowLayout(int X, int Y, int Width, int Height);
}
