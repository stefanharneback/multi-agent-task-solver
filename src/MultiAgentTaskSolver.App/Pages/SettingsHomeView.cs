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
                    CreateFieldHeaderWithSubtitle("Workspace root", "Base folder where task folders, manifests, run artifacts, and imported files are stored."),
                    workspaceEntry,
                    browseWorkspaceButton,
                    CreateFieldHeaderWithSubtitle("OpenAI gateway base URL", "Include the scheme (e.g. https://your-service-host). Do not add endpoint-specific paths."),
                    gatewayEntry,
                    CreateFieldHeaderWithSubtitle("OpenAI client bearer token", "Stored locally on this machine. Used for authenticated calls from the app to the gateway."),
                    bearerTokenEntry,
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
                    CreateFieldHeaderWithSubtitle(
                        "Seeded OpenAI models",
                        "Models available through the gateway for review and worker selection. Informational only.",
                        automationId: "OpenAiModelsHeadingLabel",
                        useSectionHeading: true),
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

    private static VerticalStackLayout CreateFieldHeaderWithSubtitle(
        string title,
        string subtitle,
        string? automationId = null,
        bool useSectionHeading = false)
    {
        var titleLabel = new Label
        {
            Text = title,
            AutomationId = automationId,
            Style = TryGetStyle(useSectionHeading ? "SectionHeadingStyle" : "FormFieldTitle")
        };

        var subtitleLabel = new Label
        {
            Text = subtitle,
            Style = TryGetStyle("FormFieldSubtitle")
        };

        return new VerticalStackLayout
        {
            Spacing = 2,
            Children = { titleLabel, subtitleLabel }
        };
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
