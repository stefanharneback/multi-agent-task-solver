using MultiAgentTaskSolver.App.Pages;
using MultiAgentTaskSolver.App.Services;
#if WINDOWS
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
#endif

namespace MultiAgentTaskSolver.App;

public partial class App : Application
{
    private readonly AppShell _appShell;

    public App(AppShell appShell)
    {
        // Loading the merged dictionaries through App.xaml caused a WinUI startup crash.
        // Instantiate the compiled dictionaries directly instead.
        UserAppTheme = AppTheme.Dark;
        Resources = new ResourceDictionary();
        Resources.MergedDictionaries.Add(new MultiAgentTaskSolver.App.Resources.Styles.Colors());
        Resources.MergedDictionaries.Add(new MultiAgentTaskSolver.App.Resources.Styles.AppStyles());
        _appShell = appShell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if WINDOWS
        var window = new Window(_appShell);
        window.Created += OnWindowCreated;
#else
        var window = new Window(_appShell);
#endif
        return window;
    }

#if WINDOWS
    private void OnWindowCreated(object? sender, EventArgs e)
    {
        if (sender is not Window mauiWindow || mauiWindow.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        RestoreWindowLayout(appWindow);
        appWindow.Changed -= OnAppWindowChanged;
        appWindow.Changed += OnAppWindowChanged;
    }

    private static void RestoreWindowLayout(AppWindow appWindow)
    {
        var layout = UiStateStore.GetWindowLayout();
        appWindow.MoveAndResize(new RectInt32(layout.X, layout.Y, layout.Width, layout.Height));
    }

    private static void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPositionChange && !args.DidSizeChange)
        {
            return;
        }

        if (sender.Presenter is OverlappedPresenter presenter
            && presenter.State is OverlappedPresenterState.Maximized or OverlappedPresenterState.Minimized)
        {
            return;
        }

        UiStateStore.SaveWindowLayout(
            sender.Position.X,
            sender.Position.Y,
            sender.Size.Width,
            sender.Size.Height);
    }
#endif
}
