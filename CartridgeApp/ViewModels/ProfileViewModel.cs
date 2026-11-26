using System.Collections.ObjectModel;
using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>One of the four showcase slots ("Favorite Games").</summary>
public class ShowcaseSlot
{
    public ShowcaseSlot(int index, Game? game)
    {
        Index = index;
        Game = game;
    }

    public int Index { get; }
    public Game? Game { get; }

    public bool HasGame => Game != null;
    public bool IsEmpty => Game == null;
    public string Title => Game?.Title ?? "Empty slot";
    public string Platform => Game?.Platform ?? string.Empty;
    public string? CoverUrl => Game?.CoverUrl;
    public bool HasCover => Game?.HasCover ?? false;
    public double Rating => Game?.Rating ?? 0;
    public string ActionText => Game == null ? "+ Pin Game" : "⇄ Change";
}

/// <summary>One bar of the rating histogram.</summary>
public class RatingBucket
{
    public RatingBucket(string label, int count, double barHeight)
    {
        Label = label;
        Count = count;
        BarHeight = barHeight;
    }

    public string Label { get; }
    public int Count { get; }
    public string CountText => Count == 0 ? string.Empty : Count.ToString();
    public double BarHeight { get; }
}

/// <summary>
/// Player profile page: banner, favorite games showcase, KPI strip, rating histogram and recent diary.
/// </summary>
public class ProfileViewModel : ViewModelBase
{
    private const int SlotCount = 4;
    private const double MaxBarHeight = 110;
    private const double MinBarHeight = 4;

    private const string CancelText = "Скасувати";
    private const string RemoveText = "Прибрати з вітрини";

    private readonly IGameRepository _gameRepository;
    private UserProfile _profile = new();
    private List<Game> _games = new();
    private bool _isBusy;
    private bool _isEditing;

    private string _editDisplayName = string.Empty;
    private string _editHandle = string.Empty;
    private string _editBio = string.Empty;
    private string _editAvatarUrl = string.Empty;
    private string _editFavoritePlatform = string.Empty;

    private string _rankTitle = "ROOKIE";
    private string _rankDetails = string.Empty;
    private string _totalLibraryText = "0";
    private string _completedText = "0";
    private string _masteredText = "0";
    private string _hoursLoggedText = "0";
    private string _backlogClearText = "0%";
    private string _averageRatingText = "AVG ★ —";
    private string _ratedGamesText = string.Empty;

    public ProfileViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;

        EditProfileCommand = new Command(StartEditing);
        SaveProfileCommand = new Command(async () => await SaveProfileAsync());
        CancelEditCommand = new Command(() => IsEditing = false);
        SelectShowcaseGameCommand = new Command<int>(async index => await SelectShowcaseGameAsync(index));
        ShowKpiInfoCommand = new Command(async () => await ShowKpiInfoAsync());
        OpenGameCommand = new Command<Game>(async game => await OpenGameAsync(game));
        OpenJournalCommand = new Command(async () => await Shell.Current.GoToAsync("//JournalPage"));
    }

    // ----- profile header
    public string DisplayName => _profile.DisplayName;
    public string Handle => _profile.Handle;
    public string Bio => _profile.Bio;
    public bool HasBio => !string.IsNullOrWhiteSpace(_profile.Bio);
    public string? AvatarUrl => string.IsNullOrWhiteSpace(_profile.AvatarUrl) ? null : _profile.AvatarUrl;
    public bool HasAvatar => AvatarUrl != null;
    public string Initials => BuildInitials(_profile.DisplayName);
    public string FavoritePlatform => _profile.FavoritePlatform;

    public string RankTitle
    {
        get => _rankTitle;
        private set => SetProperty(ref _rankTitle, value);
    }

    public string RankDetails
    {
        get => _rankDetails;
        private set => SetProperty(ref _rankDetails, value);
    }

    // ----- KPI strip
    public string TotalLibraryText { get => _totalLibraryText; private set => SetProperty(ref _totalLibraryText, value); }
    public string CompletedText { get => _completedText; private set => SetProperty(ref _completedText, value); }
    public string MasteredText { get => _masteredText; private set => SetProperty(ref _masteredText, value); }
    public string HoursLoggedText { get => _hoursLoggedText; private set => SetProperty(ref _hoursLoggedText, value); }
    public string BacklogClearText { get => _backlogClearText; private set => SetProperty(ref _backlogClearText, value); }

    // ----- histogram
    public string AverageRatingText { get => _averageRatingText; private set => SetProperty(ref _averageRatingText, value); }
    public string RatedGamesText { get => _ratedGamesText; private set => SetProperty(ref _ratedGamesText, value); }

    public ObservableCollection<ShowcaseSlot> Showcase { get; } = new();
    public ObservableCollection<RatingBucket> RatingBuckets { get; } = new();
    public ObservableCollection<PlayLog> RecentLogs { get; } = new();
    public bool HasRecentLogs => RecentLogs.Count > 0;
    public bool HasNoRecentLogs => RecentLogs.Count == 0;

    // ----- inline edit panel
    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    public string EditDisplayName { get => _editDisplayName; set => SetProperty(ref _editDisplayName, value); }
    public string EditHandle { get => _editHandle; set => SetProperty(ref _editHandle, value); }
    public string EditBio { get => _editBio; set => SetProperty(ref _editBio, value); }
    public string EditAvatarUrl { get => _editAvatarUrl; set => SetProperty(ref _editAvatarUrl, value); }
    public string EditFavoritePlatform { get => _editFavoritePlatform; set => SetProperty(ref _editFavoritePlatform, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand EditProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SelectShowcaseGameCommand { get; }
    public ICommand ShowKpiInfoCommand { get; }
    public ICommand OpenGameCommand { get; }
    public ICommand OpenJournalCommand { get; }

    // ------------------------------------------------------------------ loading

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _gameRepository.EnsureDatabaseAsync();

            _profile = await _gameRepository.GetUserProfileAsync();
            _games = (await _gameRepository.GetGamesAsync()).ToList();
            IReadOnlyList<PlayLog> logs = await _gameRepository.GetRecentLogsAsync(int.MaxValue);

            BuildStatistics(logs);
            BuildHistogram();
            BuildShowcase();

            RecentLogs.Clear();
            foreach (PlayLog log in logs.Take(3))
            {
                RecentLogs.Add(log);
            }

            NotifyProfileChanged();
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося завантажити профіль: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildStatistics(IReadOnlyList<PlayLog> logs)
    {
        int total = _games.Count;
        int completed = _games.Count(game => game.Status == GameStatus.Completed);
        int mastered = _games.Count(game => game.IsMastered);
        int backlog = _games.Count(game => game.Status == GameStatus.Backlog);
        double hoursLogged = logs.Sum(log => log.HoursSession);

        TotalLibraryText = total.ToString();
        CompletedText = completed.ToString();
        MasteredText = mastered.ToString();
        HoursLoggedText = $"{hoursLogged:0.#}";
        BacklogClearText = total == 0 ? "0%" : $"{(total - backlog) * 100d / total:0}%";

        (string rank, string details) = ResolveRank(completed);
        RankTitle = rank;
        RankDetails = details;
    }

    private void BuildHistogram()
    {
        double[] ratings = _games.Where(game => game.Rating > 0).Select(game => game.Rating).ToArray();
        var counts = new int[10];
        foreach (double rating in ratings)
        {
            int bucket = (int)Math.Round(Math.Clamp(rating, 0.5, 5.0) * 2d, MidpointRounding.AwayFromZero) - 1;
            counts[Math.Clamp(bucket, 0, 9)]++;
        }

        int max = counts.Max();

        RatingBuckets.Clear();
        for (int i = 0; i < counts.Length; i++)
        {
            double height = counts[i] == 0 ? MinBarHeight : 10 + (MaxBarHeight - 10) * counts[i] / max;
            RatingBuckets.Add(new RatingBucket(((i + 1) / 2d).ToString("0.0"), counts[i], height));
        }

        AverageRatingText = ratings.Length == 0 ? "AVG ★ —" : $"AVG ★ {ratings.Average():0.0}";
        RatedGamesText = $"{ratings.Length} rated";
    }

    private void BuildShowcase()
    {
        int[] ids = ParseShowcaseIds(_profile.ShowcaseGameIdsCsv);

        Showcase.Clear();
        for (int slot = 0; slot < SlotCount; slot++)
        {
            Game? game = ids[slot] > 0 ? _games.FirstOrDefault(item => item.Id == ids[slot]) : null;
            Showcase.Add(new ShowcaseSlot(slot, game));
        }
    }

    /// <summary>Rank grows with the number of completed games.</summary>
    private static (string Rank, string Details) ResolveRank(int completed)
    {
        (int Threshold, string Name)[] tiers =
        [
            (0, "ROOKIE"),
            (2, "CHALLENGER"),
            (5, "VETERAN"),
            (10, "ELITE"),
            (20, "MASTER"),
            (40, "LEGEND")
        ];

        int current = 0;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (completed >= tiers[i].Threshold)
            {
                current = i;
            }
        }

        string details = current == tiers.Length - 1
            ? $"{completed} completed · max rank"
            : $"{completed} completed · {tiers[current + 1].Threshold - completed} to {tiers[current + 1].Name}";

        return (tiers[current].Name, details);
    }

    // ------------------------------------------------------------------ showcase

    private async Task SelectShowcaseGameAsync(int index)
    {
        if (index < 0 || index >= SlotCount)
        {
            return;
        }

        Page? page = CurrentPage;
        if (page == null)
        {
            return;
        }

        if (_games.Count == 0)
        {
            await ShowAlertAsync("Вітрина", "Бібліотека порожня — спершу додайте гру.");
            return;
        }

        int[] ids = ParseShowcaseIds(_profile.ShowcaseGameIdsCsv);
        bool slotHasGame = ids[index] > 0 && _games.Any(game => game.Id == ids[index]);

        List<Game> options = _games.OrderBy(game => game.Title).ToList();
        string[] labels = options.Select(game => $"{game.Title} ({game.Platform})").ToArray();

        string choice = await page.DisplayActionSheetAsync(
            $"Слот {index + 1}: вибрати гру",
            CancelText,
            slotHasGame ? RemoveText : null,
            labels);

        if (string.IsNullOrEmpty(choice) || choice == CancelText)
        {
            return;
        }

        if (choice == RemoveText)
        {
            ids[index] = 0;
        }
        else
        {
            int selected = Array.IndexOf(labels, choice);
            if (selected < 0)
            {
                return;
            }

            int newId = options[selected].Id;
            int existingSlot = Array.IndexOf(ids, newId);
            if (existingSlot >= 0 && existingSlot != index)
            {
                // The game is already pinned elsewhere: swap the two slots instead of duplicating it.
                ids[existingSlot] = ids[index];
            }

            ids[index] = newId;
        }

        try
        {
            _profile.ShowcaseGameIdsCsv = string.Join(",", ids);
            await _gameRepository.UpdateUserProfileAsync(_profile);
            BuildShowcase();
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося зберегти вітрину: {exception.Message}");
        }
    }

    private static int[] ParseShowcaseIds(string? csv)
    {
        string[] parts = (csv ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        var ids = new int[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            ids[i] = i < parts.Length && int.TryParse(parts[i], out int id) && id > 0 ? id : 0;
        }

        return ids;
    }

    // ------------------------------------------------------------------ edit profile

    private void StartEditing()
    {
        EditDisplayName = _profile.DisplayName;
        EditHandle = _profile.Handle;
        EditBio = _profile.Bio;
        EditAvatarUrl = _profile.AvatarUrl ?? string.Empty;
        EditFavoritePlatform = _profile.FavoritePlatform;
        IsEditing = true;
    }

    private async Task SaveProfileAsync()
    {
        string name = EditDisplayName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await ShowAlertAsync("Помилка", "Ім'я профілю є обов'язковим.");
            return;
        }

        string handle = new string(EditHandle.Where(character => !char.IsWhiteSpace(character)).ToArray()).TrimStart('@');
        if (string.IsNullOrEmpty(handle))
        {
            await ShowAlertAsync("Помилка", "Вкажіть @handle (латиницею, без пробілів).");
            return;
        }

        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(EditAvatarUrl))
        {
            string trimmed = EditAvatarUrl.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                await ShowAlertAsync("Помилка", "Avatar URL має бути повним посиланням http(s)://…");
                return;
            }

            avatarUrl = trimmed;
        }

        try
        {
            _profile.DisplayName = name;
            _profile.Handle = "@" + handle;
            _profile.Bio = EditBio.Trim();
            _profile.AvatarUrl = avatarUrl;
            _profile.FavoritePlatform = string.IsNullOrWhiteSpace(EditFavoritePlatform) ? "PC" : EditFavoritePlatform.Trim();

            await _gameRepository.UpdateUserProfileAsync(_profile);
            IsEditing = false;
            NotifyProfileChanged();
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося зберегти профіль: {exception.Message}");
        }
    }

    // ------------------------------------------------------------------ misc

    private async Task ShowKpiInfoAsync()
    {
        await ShowAlertAsync(
            "Як рахується статистика",
            "Total Library — усі ігри в бібліотеці.\n" +
            "Completed — ігри зі статусом Completed.\n" +
            "100% Mastered — ігри, позначені як 100% / Platinum.\n" +
            "Hours Logged — сума годин усіх записів щоденника (не поле «години» гри).\n\n" +
            "Backlog Clear % = (усі ігри − ігри в Backlog) ÷ усі ігри × 100. " +
            "Показує, яку частку бібліотеки ви вже почали, пройшли або закинули.\n\n" +
            "Ранг залежить від кількості пройдених ігор:\n" +
            "ROOKIE 0–1 · CHALLENGER 2–4 · VETERAN 5–9 · ELITE 10–19 · MASTER 20–39 · LEGEND 40+\n\n" +
            "AVG ★ — середня оцінка лише серед ігор, які ви оцінили (0 = без оцінки не враховується).");
    }

    private static async Task OpenGameAsync(Game game)
    {
        if (game == null || game.Id == 0)
        {
            return;
        }

        await Shell.Current.GoToAsync("GameDetailPage", new Dictionary<string, object>
        {
            ["Game"] = game
        });
    }

    private static string BuildInitials(string name)
    {
        string[] words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        string initials = words.Length == 1
            ? words[0][..1]
            : words[0][..1] + words[1][..1];

        return initials.ToUpperInvariant();
    }

    private void NotifyProfileChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Handle));
        OnPropertyChanged(nameof(Bio));
        OnPropertyChanged(nameof(HasBio));
        OnPropertyChanged(nameof(AvatarUrl));
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(FavoritePlatform));
        OnPropertyChanged(nameof(HasRecentLogs));
        OnPropertyChanged(nameof(HasNoRecentLogs));
    }
}