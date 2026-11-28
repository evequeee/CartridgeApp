using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>
/// Top bar shown on the four tab pages: tabs with an underline on the left, profile widget on the right.
/// </summary>
public class AppHeaderViewModel : ViewModelBase
{
    private static readonly Dictionary<string, string> RoutesByTab = new()
    {
        ["Library"] = "MainPage",
        ["Journal"] = "JournalPage",
        ["Profile"] = "ProfilePage",
        ["Analytics"] = "StatsPage"
    };

    private readonly IGameRepository _gameRepository;
    private string _activeTab = string.Empty;
    private string _displayName = "Player One";
    private string _handle = "@cartridge_hunter";
    private string _initials = "PO";
    private string? _avatarUrl;
    private string _rankText = "🏆 RANK: ROOKIE";
    private bool _isRefreshing;

    public AppHeaderViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        NavigateCommand = new Command<string>(async route => await NavigateAsync(route));
    }

    public string DisplayName { get => _displayName; private set => SetProperty(ref _displayName, value); }
    public string Handle { get => _handle; private set => SetProperty(ref _handle, value); }
    public string Initials { get => _initials; private set => SetProperty(ref _initials, value); }
    public string RankText { get => _rankText; private set => SetProperty(ref _rankText, value); }

    public string? AvatarUrl
    {
        get => _avatarUrl;
        private set
        {
            if (SetProperty(ref _avatarUrl, value))
            {
                OnPropertyChanged(nameof(HasAvatar));
                OnPropertyChanged(nameof(AvatarSource));
            }
        }
    }

    public ImageSource? AvatarSource
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_avatarUrl))
            {
                return null;
            }

            return Uri.TryCreate(_avatarUrl, UriKind.Absolute, out Uri? uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? ImageSource.FromUri(uri)
                : ImageSource.FromFile(_avatarUrl);
        }
    }

    public bool HasAvatar => !string.IsNullOrWhiteSpace(_avatarUrl);

    public bool IsLibraryActive => _activeTab == "Library";
    public bool IsJournalActive => _activeTab == "Journal";
    public bool IsProfileActive => _activeTab == "Profile";
    public bool IsAnalyticsActive => _activeTab == "Analytics";

    public ICommand NavigateCommand { get; }

    public void SetActiveTab(string? tab)
    {
        _activeTab = tab ?? string.Empty;
        OnPropertyChanged(nameof(IsLibraryActive));
        OnPropertyChanged(nameof(IsJournalActive));
        OnPropertyChanged(nameof(IsProfileActive));
        OnPropertyChanged(nameof(IsAnalyticsActive));
    }

    /// <summary>Reloads profile data and rank. Cheap enough to run every time a tab page appears.</summary>
    public async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            await _gameRepository.EnsureDatabaseAsync();
            UserProfile profile = await _gameRepository.GetUserProfileAsync();
            IReadOnlyList<Game> games = await _gameRepository.GetGamesAsync();

            int completed = games.Count(game => game.Status == GameStatus.Completed);
            (string rank, _) = PlayerRank.Resolve(completed);

            DisplayName = profile.DisplayName;
            Handle = profile.Handle;
            Initials = BuildInitials(profile.DisplayName);
            AvatarUrl = string.IsNullOrWhiteSpace(profile.AvatarUrl) ? null : profile.AvatarUrl;
            RankText = $"🏆 RANK: {rank}";
        }
        catch
        {
            // The top bar is decorative; page view models already report database errors to the user.
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task NavigateAsync(string? tab)
    {
        if (string.IsNullOrEmpty(tab) || tab == _activeTab || !RoutesByTab.TryGetValue(tab, out string? route))
        {
            return;
        }

        await Shell.Current.GoToAsync("//" + route);
    }

    private static string BuildInitials(string name)
    {
        string[] words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        string initials = words.Length == 1 ? words[0][..1] : words[0][..1] + words[1][..1];
        return initials.ToUpperInvariant();
    }
}