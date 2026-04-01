using MultiAgentTaskSolver.App.Services;

namespace MultiAgentTaskSolver.App.Pages;

/// <summary>
/// A reusable collapsible section with a tappable header that toggles body content visibility.
/// Expand/collapse state is persisted via <see cref="UiStateStore"/> when <see cref="StateKey"/> is set.
/// </summary>
public sealed class CollapsibleSection : ContentView
{
    private readonly Label _chevronLabel;
    private readonly VerticalStackLayout _bodyHost;
    private bool _isExpanded;

    public CollapsibleSection()
    {
        _chevronLabel = new Label
        {
            Text = ChevronExpanded,
            VerticalOptions = LayoutOptions.Center,
            Style = TryGetStyle("FormFieldTitle")
        };

        var titleLabel = new Label
        {
            VerticalOptions = LayoutOptions.Center,
            Style = TryGetStyle("SectionHeadingStyle")
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8,
            Style = TryGetStyle("CollapsibleSectionHeaderStyle")
        };

        Grid.SetColumn(_chevronLabel, 0);
        Grid.SetColumn(titleLabel, 1);
        headerGrid.Children.Add(_chevronLabel);
        headerGrid.Children.Add(titleLabel);

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (_, _) => SetExpanded(!_isExpanded, persist: true);
        headerGrid.GestureRecognizers.Add(tapGesture);

        _bodyHost = new VerticalStackLayout
        {
            Spacing = 14
        };

        var rootLayout = new VerticalStackLayout
        {
            Spacing = 6,
            Children = { headerGrid, _bodyHost }
        };

        Content = rootLayout;

        // Bind title label to the Title property via a simple PropertyChanged hook.
        // We avoid full BindableProperty boilerplate by using a backing field approach.
        titleLabel.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
    }

    private const string ChevronExpanded = "▾";
    private const string ChevronCollapsed = "▸";

    private string _title = string.Empty;
    private string _stateKey = string.Empty;
    private View? _sectionBody;

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// A unique key used to persist expand/collapse state. When empty, state is not persisted.
    /// </summary>
    public string StateKey
    {
        get => _stateKey;
        set => _stateKey = value;
    }

    /// <summary>
    /// The view content inside the collapsible body.
    /// </summary>
    public View? SectionBody
    {
        get => _sectionBody;
        set
        {
            _sectionBody = value;
            _bodyHost.Children.Clear();
            if (value is not null)
            {
                _bodyHost.Children.Add(value);
            }
        }
    }

    /// <summary>
    /// Sets the initial expanded state and loads persisted preference if a <see cref="StateKey"/> is set.
    /// Call this after setting <see cref="SectionBody"/> and <see cref="StateKey"/>.
    /// </summary>
    public void Initialize(bool defaultExpanded)
    {
        var expanded = string.IsNullOrWhiteSpace(_stateKey)
            ? defaultExpanded
            : UiStateStore.GetSectionExpanded(_stateKey, defaultExpanded);

        SetExpanded(expanded, persist: false);
    }

    /// <summary>
    /// Gets or sets the current expanded state. Use <see cref="Initialize"/> for initial setup.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetExpanded(value, persist: true);
    }

    private void SetExpanded(bool expanded, bool persist)
    {
        _isExpanded = expanded;
        _chevronLabel.Text = expanded ? ChevronExpanded : ChevronCollapsed;
        _bodyHost.IsVisible = expanded;

        if (persist && !string.IsNullOrWhiteSpace(_stateKey))
        {
            UiStateStore.SaveSectionExpanded(_stateKey, expanded);
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
