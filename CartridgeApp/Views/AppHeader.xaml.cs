using Microsoft.Extensions.DependencyInjection;

using CartridgeApp.ViewModels;

namespace CartridgeApp.Views;

/// <summary>
/// Shared top bar of the LIBRARY / JOURNAL / PROFILE / ANALYTICS pages.
/// Usage: &lt;views:AppHeader ActiveTab="Library" /&gt;
/// </summary>
public partial class AppHeader : ContentView
{
    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab),
        typeof(string),
        typeof(AppHeader),
        string.Empty,
        propertyChanged: (bindable, _, newValue) => ((AppHeader)bindable)._viewModel?.SetActiveTab(newValue as string));

    private readonly AppHeaderViewModel? _viewModel;
    private Page? _hostPage;

    public AppHeader()
    {
        InitializeComponent();

        _viewModel = IPlatformApplication.Current?.Services.GetService<AppHeaderViewModel>();
        BindingContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Library, Journal, Profile or Analytics.</summary>
    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        _viewModel?.SetActiveTab(ActiveTab);

        // Refresh the profile widget and rank every time the hosting page appears again.
        _hostPage ??= FindHostPage();
        if (_hostPage != null)
        {
            _hostPage.Appearing -= OnHostPageAppearing;
            _hostPage.Appearing += OnHostPageAppearing;
        }

        if (_viewModel != null)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_hostPage != null)
        {
            _hostPage.Appearing -= OnHostPageAppearing;
        }
    }

    private async void OnHostPageAppearing(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private Page? FindHostPage()
    {
        Element? element = Parent;
        while (element != null && element is not Page)
        {
            element = element.Parent;
        }

        return element as Page;
    }
}