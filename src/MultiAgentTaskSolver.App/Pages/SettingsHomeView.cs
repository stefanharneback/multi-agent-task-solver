using MultiAgentTaskSolver.App.ViewModels;

namespace MultiAgentTaskSolver.App.Pages;

public sealed class SettingsHomeView : ContentView
{
    private readonly SettingsViewModel _viewModel;
    private readonly VerticalStackLayout _modelsHost;

    public SettingsHomeView(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.OpenAiModels.CollectionChanged += (_, _) => RenderModels();

        var workspaceEntry = new Entry
        {
            AutomationId = "WorkspaceRootEntry",
            Placeholder = "Folder where task workspaces are stored."
        };
        ApplyStyle(workspaceEntry, "FormEntryStyle");
        workspaceEntry.SetBinding(Entry.TextProperty, nameof(SettingsViewModel.WorkspaceRootPath));

        var browseWorkspaceButton = new Button
        {
            AutomationId = "BrowseWorkspaceButton",
            HorizontalOptions = LayoutOptions.Start,
            Text = "Browse Workspace Folder"
        };
        ApplyStyle(browseWorkspaceButton, "SecondaryActionButtonStyle");
        browseWorkspaceButton.SetBinding(Button.CommandProperty, nameof(SettingsViewModel.BrowseWorkspaceFolderCommand));

        var gatewayEntry = new Entry
        {
            AutomationId = "OpenAiGatewayBaseUrlEntry",
            Keyboard = Keyboard.Url,
            Placeholder = "https://your-gateway-host"
        };
        ApplyStyle(gatewayEntry, "FormEntryStyle");
        gatewayEntry.SetBinding(Entry.TextProperty, nameof(SettingsViewModel.OpenAiGatewayBaseUrl));

        var bearerTokenEntry = new Entry
        {
            AutomationId = "OpenAiBearerTokenEntry",
            IsPassword = true,
            Placeholder = "Paste the client bearer token"
        };
        ApplyStyle(bearerTokenEntry, "FormEntryStyle");
        bearerTokenEntry.SetBinding(Entry.TextProperty, nameof(SettingsViewModel.OpenAiBearerToken));

        var saveButton = new Button
        {
            AutomationId = "SaveSettingsButton",
            HorizontalOptions = LayoutOptions.Start,
            Text = "Save Settings"
        };
        ApplyStyle(saveButton, "PrimaryActionButtonStyle");
        saveButton.SetBinding(Button.CommandProperty, nameof(SettingsViewModel.SaveCommand));

        var errorLabel = new Label
        {
            AutomationId = "SettingsErrorLabel",
            Style = TryGetStyle("ErrorLabelStyle")
        };
        errorLabel.SetBinding(Label.TextProperty, nameof(SettingsViewModel.ErrorMessage));

        _modelsHost = new VerticalStackLayout
        {
            AutomationId = "OpenAiModelsHost",
            Spacing = 8
        };

        var settingsCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    CreateSectionHeading("Workspace and gateway"),
                    CreateFieldHeader("Workspace root", "workspace-root"),
                    workspaceEntry,
                    CreateHintLabel("This should point to the base folder where task manifests and artifacts are saved."),
                    browseWorkspaceButton,
                    CreateFieldHeader("OpenAI gateway base URL", "gateway-base-url"),
                    gatewayEntry,
                    CreateHintLabel("Base URL for the gateway service that forwards requests to OpenAI."),
                    CreateFieldHeader("OpenAI client bearer token", "bearer-token"),
                    bearerTokenEntry,
                    CreateHintLabel("Stored locally and used for authenticated calls from the app to the gateway."),
                    saveButton,
                    errorLabel
                }
            }
        };
        ApplyStyle(settingsCard, "SurfaceCardStyle");

        var modelsCard = new Border
        {
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    CreateFieldHeader("Seeded OpenAI models", "seeded-models", automationId: "OpenAiModelsHeadingLabel", useSectionHeading: true),
                    _modelsHost
                }
            }
        };
        ApplyStyle(modelsCard, "SurfaceCardStyle");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        AutomationId = "SettingsHeadingLabel",
                        Text = "Workspace settings",
                        Style = TryGetStyle("PageHeadingStyle")
                    },
                    CreateHintLabel("Configure the local workspace and the OpenAI gateway connection used by the app."),
                    settingsCard,
                    modelsCard
                }
            }
        };
    }

    public async Task LoadAsync()
    {
        await _viewModel.LoadAsync();
        RenderModels();
    }

    private static async Task ShowHelpAsync(string helpKey)
    {
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

        if (Shell.Current?.CurrentPage is Page currentPage)
        {
            await currentPage.DisplayAlertAsync(title, message, "Close");
        }
    }

    private void RenderModels()
    {
        _modelsHost.Children.Clear();

        if (_viewModel.OpenAiModels.Count == 0)
        {
            _modelsHost.Children.Add(CreateHintLabel("No models are loaded yet."));
            return;
        }

        foreach (var model in _viewModel.OpenAiModels)
        {
            var card = new Border
            {
                Padding = 12,
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label
                        {
                            Text = model.DisplayName,
                            Style = TryGetStyle("BodyStrongStyle")
                        },
                        new Label
                        {
                            Text = model.ModelId,
                            Style = TryGetStyle("FormFieldHint")
                        },
                        new Label
                        {
                            Text = model.Description
                        }
                    }
                }
            };
            ApplyStyle(card, "SurfaceCardStyle");
            _modelsHost.Children.Add(card);
        }
    }

    private static Grid CreateFieldHeader(string title, string helpKey, string? automationId = null, bool useSectionHeading = false)
    {
        var label = new Label
        {
            Text = title,
            AutomationId = automationId
        };
        label.Style = TryGetStyle(useSectionHeading ? "SectionHeadingStyle" : "FormFieldTitle");

        var button = new Button
        {
            Command = new Command(async () => await ShowHelpAsync(helpKey)),
            Style = TryGetStyle("InfoButtonStyle"),
            Text = "?"
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(button, 1);
        grid.Children.Add(label);
        grid.Children.Add(button);
        return grid;
    }

    private static Label CreateSectionHeading(string text)
    {
        return new Label
        {
            Text = text,
            Style = TryGetStyle("SectionHeadingStyle")
        };
    }

    private static Label CreateHintLabel(string text)
    {
        return new Label
        {
            Text = text,
            Style = TryGetStyle("FormFieldHint")
        };
    }

    private static void ApplyStyle(VisualElement element, string resourceKey)
    {
        if (TryGetStyle(resourceKey) is { } style)
        {
            element.Style = style;
        }
    }

    private static Style? TryGetStyle(string resourceKey)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var style) == true
            && style is Style typedStyle)
        {
            return typedStyle;
        }

        return null;
    }
}
