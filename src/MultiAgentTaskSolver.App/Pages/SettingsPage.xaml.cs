using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnHelpButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string helpKey)
        {
            return;
        }

        var (title, message) = helpKey switch
        {
            "workspace-root" => (
                "Workspace root",
                "Choose the base folder where task folders, manifests, run artifacts, and copied imports are stored. Keep this as a stable local path that the app can write to."),
            "gateway-base-url" => (
                "OpenAI gateway base URL",
                "Enter the base URL for the gateway service that this app calls. Include the scheme, for example https://your-service-host, but do not add endpoint-specific paths."),
            "bearer-token" => (
                "OpenAI client bearer token",
                "Paste the client token used to authenticate from this app to the gateway. It is stored locally on this machine and sent with gateway requests."),
            "seeded-models" => (
                "Seeded OpenAI models",
                "This list shows the models currently seeded in the app for review and worker selection. It is informational here so you can verify what the app expects to be available through the gateway."),
            _ => ("Field help", "No additional help is available for this field yet."),
        };

        await DisplayAlertAsync(title, message, "Close");
    }
}
