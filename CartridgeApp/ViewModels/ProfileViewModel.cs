using System.Collections.ObjectModel;
using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;
using Microsoft.Maui.Media;

namespace CartridgeApp.ViewModels;

/// <summary>One of the four showcase slots ("Favorite Games").</summary>
public class ShowcaseSlot
{
    public ShowcaseSlot(int index, Game? game, bool isCrowned)
    {
        Index = index;
        Game = game;
        IsCrowned = isCrowned;
    }

    public int Index { get; }
    public Game? Game { get; }
    public bool IsCrowned { get; }

    public bool HasGame => Game != null;
    public bool IsEmpty => Game == null;
    public bool HasGameWithoutCover => HasGame && !HasCover;
    public string Title => Game?.Title ?? "Empty slot";
    public string Platform => Game?.Platform ?? string.Empty;
    public string? CoverUrl => Game?.CoverUrl;
    public bool HasCover => Game?.HasCover ?? false;
    public double Rating => Game?.Rating ?? 0;
}

public sealed class RecentPlay
{
    public RecentPlay(int gridColumn, PlayLog log)
    {
        GridColumn = gridColumn;
        Log = log;
    }

    public int GridColumn { get; }
    public PlayLog Log { get; }
    public Game Game => Log.Game;
    public DateTime LoggedDate => Log.LoggedDate;
    public double Rating => Log.Game.Rating;
}

/// <summary>One bar of the rating histogram.</summary>
public class RatingBucket
{
    public RatingBucket(int gridColumn, string label, int count, double barHeight)
    {
        GridColumn = gridColumn;
        Label = label;
        Count = count;
        BarHeight = barHeight;
    }

    public int GridColumn { get; }
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
    private const double MaxBarHeight = 64;
    private const double MinBarHeight = 4;

    private const string CancelText = "Скасувати";
    private const string RemoveText = "Очистити слот";

    private readonly IGameRepository _gameRepository;
    private UserProfile _profile = new();
    private List<Game> _games = new();
    private bool _isBusy;
    private bool _isEditing;
    private int[] _draftShowcaseIds = new int[SlotCount];
    private int? _draftCrownedGameId;

    private string _editDisplayName = string.Empty;
    private string _editHandle = string.Empty;
    private string _editBio = string.Empty;
    private string _editAvatarUrl = string.Empty;
    private string _editFavoritePlatform = string.Empty;

    private string _rankTitle = "ROOKIE";
    private string _rankDetails = string.Empty;
    private string _totalLibraryText = "0";
    private string _playedText = "0";
    private string _completedText = "0";
    private string _abandonedText = "0";
    private string _masteredText = "0";
    private string _hoursLoggedText = "0";
    private string _backlogClearText = "0%";
    private string _averageRatingText = "AVG ★ —";
    private string _ratedGamesText = string.Empty;

    public ProfileViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;

        EditProfileCommand = new Command(StartEditing);
        ToggleEditProfileCommand = new Command(() =>
        {
            if (IsEditing)
            {
                CancelEditing();
            }
            else
            {
                StartEditing();
            }
        });
        SaveProfileCommand = new Command(async () => await SaveProfileAsync());
        CancelEditCommand = new Command(CancelEditing);
        SelectShowcaseGameCommand = new Command<int>(async index => await SelectShowcaseGameAsync(index));
        CrownShowcaseGameCommand = new Command<ShowcaseSlot>(CrownShowcaseGame);
        ChooseAvatarCommand = new Command(async () => await ChooseAvatarAsync());
        ShowKpiInfoCommand = new Command(async () => await ShowKpiInfoAsync());
        OpenGameCommand = new Command<Game>(async game => await OpenGameAsync(game));
        OpenShowcaseGameCommand = new Command<ShowcaseSlot>(async slot => await OpenShowcaseSlotAsync(slot));
        OpenJournalCommand = new Command(async () => await Shell.Current.GoToAsync("//JournalPage"));
    }

    // ----- profile header
    public string DisplayName => _profile.DisplayName;
    public string Handle => _profile.Handle;
    public string Bio => _profile.Bio;
    public bool HasBio => !string.IsNullOrWhiteSpace(_profile.Bio);
    public bool HasNoBio => !HasBio;
    public string? AvatarUrl => string.IsNullOrWhiteSpace(_profile.AvatarUrl) ? null : _profile.AvatarUrl;
    public ImageSource? AvatarSource => CreateAvatarSource(AvatarUrl);
    public bool HasAvatar => AvatarSource != null;
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
    public string PlayedText { get => _playedText; private set => SetProperty(ref _playedText, value); }
    public string CompletedText { get => _completedText; private set => SetProperty(ref _completedText, value); }
    public string AbandonedText { get => _abandonedText; private set => SetProperty(ref _abandonedText, value); }
    public string MasteredText { get => _masteredText; private set => SetProperty(ref _masteredText, value); }
    public string HoursLoggedText { get => _hoursLoggedText; private set => SetProperty(ref _hoursLoggedText, value); }
    public string BacklogClearText { get => _backlogClearText; private set => SetProperty(ref _backlogClearText, value); }

    // ----- histogram
    public string AverageRatingText { get => _averageRatingText; private set => SetProperty(ref _averageRatingText, value); }
    public string RatedGamesText { get => _ratedGamesText; private set => SetProperty(ref _ratedGamesText, value); }

    public ObservableCollection<ShowcaseSlot> Showcase { get; } = new();
    public ObservableCollection<AchievementBadge> Badges { get; } = new();
    public ObservableCollection<RatingBucket> RatingBuckets { get; } = new();
    public ObservableCollection<RecentPlay> RecentLogs { get; } = new();
    public ObservableCollection<PlayLog> RecentReviews { get; } = new();
    public bool HasRecentLogs => RecentLogs.Count > 0;
    public bool HasNoRecentLogs => RecentLogs.Count == 0;
    public bool HasNoRecentReviews => RecentReviews.Count == 0;

    // ----- inline edit panel
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(IsProfileView));
            }
        }
    }
    public bool IsProfileView => !IsEditing;

    public string EditDisplayName
    {
        get => _editDisplayName;
        set
        {
            if (SetProperty(ref _editDisplayName, value))
            {
                OnPropertyChanged(nameof(EditInitials));
            }
        }
    }
    public string EditHandle { get => _editHandle; set => SetProperty(ref _editHandle, value); }
    public string EditBio { get => _editBio; set => SetProperty(ref _editBio, value); }
    public string EditAvatarUrl
    {
        get => _editAvatarUrl;
        set
        {
            if (SetProperty(ref _editAvatarUrl, value))
            {
                OnPropertyChanged(nameof(EditAvatarSource));
                OnPropertyChanged(nameof(HasEditAvatar));
            }
        }
    }
    public string EditInitials => BuildInitials(EditDisplayName);
    public ImageSource? EditAvatarSource => CreateAvatarSource(EditAvatarUrl);
    public bool HasEditAvatar => EditAvatarSource != null;
    public string EditFavoritePlatform { get => _editFavoritePlatform; set => SetProperty(ref _editFavoritePlatform, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand EditProfileCommand { get; }
    public ICommand ToggleEditProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SelectShowcaseGameCommand { get; }
    public ICommand CrownShowcaseGameCommand { get; }
    public ICommand ChooseAvatarCommand { get; }
    public ICommand ShowKpiInfoCommand { get; }
    public ICommand OpenGameCommand { get; }
    public ICommand OpenShowcaseGameCommand { get; }
    public ICommand OpenJournalCommand { get; }

    // ------------------------------------------------------------------ loading

    public async Task RefreshAsync()
    {
        if (IsBusy || IsEditing)
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
            BuildBadges(logs);

            RecentLogs.Clear();
            PlayLog[] latestGames = logs
                .Where(log => log.Game != null)
                .GroupBy(log => log.GameId)
                .Select(group => group.OrderByDescending(log => log.LoggedDate).ThenByDescending(log => log.Id).First())
                .OrderByDescending(log => log.LoggedDate)
                .ThenByDescending(log => log.Id)
                .Take(4)
                .ToArray();
            for (int index = 0; index < latestGames.Length; index++)
            {
                RecentLogs.Add(new RecentPlay(index, latestGames[index]));
            }

            RecentReviews.Clear();
            foreach (PlayLog log in logs.Where(log => !string.IsNullOrWhiteSpace(log.ReviewText)).Take(4))
            {
                RecentReviews.Add(log);
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
        int played = _games.Count(game => game.Status == GameStatus.Played);
        int completed = _games.Count(game => game.Status == GameStatus.Completed);
        int abandoned = _games.Count(game => game.Status == GameStatus.Abandoned);
        int mastered = _games.Count(game => game.IsMastered);
        int backlog = _games.Count(game => game.Status == GameStatus.Backlog);
        double hoursLogged = logs.Sum(log => log.HoursSession);

        TotalLibraryText = total.ToString();
        PlayedText = played.ToString();
        CompletedText = completed.ToString();
        AbandonedText = abandoned.ToString();
        MasteredText = mastered.ToString();
        HoursLoggedText = $"{hoursLogged:0.#}";
        BacklogClearText = total == 0 ? "0%" : $"{(total - backlog) * 100d / total:0}%";

        (string rank, string details) = PlayerRank.Resolve(completed);
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
            RatingBuckets.Add(new RatingBucket(i, ((i + 1) / 2d).ToString("0.0"), counts[i], height));
        }

        AverageRatingText = ratings.Length == 0 ? "AVG ★ —" : $"AVG ★ {ratings.Average():0.0}";
        RatedGamesText = $"{ratings.Length} rated";
    }

    private void BuildShowcase()
    {
        int[] ids = IsEditing
            ? _draftShowcaseIds
            : ParseShowcaseIds(_profile.ShowcaseGameIdsCsv);
        int? crownedGameId = IsEditing ? _draftCrownedGameId : _profile.CrownedGameId;

        Showcase.Clear();
        for (int slot = 0; slot < SlotCount; slot++)
        {
            Game? game = ids[slot] > 0 ? _games.FirstOrDefault(item => item.Id == ids[slot]) : null;
            Showcase.Add(new ShowcaseSlot(slot, game, game != null && game.Id == crownedGameId));
        }
    }

    private void BuildBadges(IReadOnlyList<PlayLog> logs)
    {
        int completed = _games.Count(game => game.Status == GameStatus.Completed);
        int reviews = logs.Count(log => !string.IsNullOrWhiteSpace(log.ReviewText));
        double hours = logs.Sum(log => log.HoursSession);
        int platforms = _games.Select(game => game.Platform)
            .Where(platform => !string.IsNullOrWhiteSpace(platform))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Badges.Clear();
        AddBadge("🎮", "First Cartridge", "Add your first game", _games.Count >= 1, "#00B4FF");
        AddBadge("⚔️", "Backlog Slayer", "Complete 3 games", completed >= 3, "#FF4081");
        AddBadge("🏆", "Platinum Hunter", "Master a game to 100%", _games.Any(game => game.IsMastered), "#FFC83D");
        AddBadge("✍️", "Game Critic", "Write 2 diary reviews", reviews >= 2, "#7C4DFF");
        AddBadge("⏱️", "Marathoner", "Log 25+ hours", hours >= 25, "#38BDF8");
        AddBadge("🕹️", "Multi-Platform", "Play on 3 platforms", platforms >= 3, "#00E676");
    }

    private void AddBadge(string icon, string title, string description, bool unlocked, string color)
    {
        Badges.Add(new AchievementBadge
        {
            Icon = icon,
            Title = title,
            Description = description,
            IsUnlocked = unlocked,
            AccentColor = Color.FromArgb(unlocked ? color : "#5E6B83"),
            GridColumn = Badges.Count % 2,
            GridRow = Badges.Count / 2
        });
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

        int[] ids = (int[])_draftShowcaseIds.Clone();
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

        int? crownedGameId = _draftCrownedGameId;
        if (choice == RemoveText)
        {
            if (ids[index] == crownedGameId)
            {
                crownedGameId = null;
            }

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
            else if (ids[index] == crownedGameId)
            {
                crownedGameId = null;
            }

            ids[index] = newId;
        }

        _draftShowcaseIds = ids;
        _draftCrownedGameId = crownedGameId;
        BuildShowcase();
    }

    private void CrownShowcaseGame(ShowcaseSlot? slot)
    {
        if (slot?.Game == null)
        {
            return;
        }

        _draftCrownedGameId = _draftCrownedGameId == slot.Game.Id ? null : slot.Game.Id;
        BuildShowcase();
    }

    private async Task OpenShowcaseSlotAsync(ShowcaseSlot? slot)
    {
        if (slot?.Game == null)
        {
            StartEditing();
            return;
        }

        await OpenGameAsync(slot.Game);
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
        if (IsEditing)
        {
            return;
        }

        EditDisplayName = _profile.DisplayName;
        EditHandle = _profile.Handle;
        EditBio = _profile.Bio;
        EditAvatarUrl = _profile.AvatarUrl ?? string.Empty;
        EditFavoritePlatform = _profile.FavoritePlatform;
        _draftShowcaseIds = ParseShowcaseIds(_profile.ShowcaseGameIdsCsv);
        _draftCrownedGameId = _profile.CrownedGameId;
        IsEditing = true;
        BuildShowcase();
    }

    private void CancelEditing()
    {
        _draftShowcaseIds = ParseShowcaseIds(_profile.ShowcaseGameIdsCsv);
        _draftCrownedGameId = _profile.CrownedGameId;
        IsEditing = false;
        BuildShowcase();
    }

    private async Task SaveProfileAsync()
    {
        if (IsBusy)
        {
            return;
        }

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

        string? avatarUrl = string.IsNullOrWhiteSpace(EditAvatarUrl) ? null : EditAvatarUrl.Trim();
        if (avatarUrl != null &&
            !(Uri.TryCreate(avatarUrl, UriKind.Absolute, out Uri? avatarUri) &&
              (avatarUri.Scheme == Uri.UriSchemeHttp || avatarUri.Scheme == Uri.UriSchemeHttps)) &&
            !File.Exists(avatarUrl))
        {
            await ShowAlertAsync("Помилка", "Avatar URL має бути прямим посиланням http(s)://… або виберіть локальне фото.");
            return;
        }

        IsBusy = true;
        try
        {
            int? crownedGameId = _draftCrownedGameId.HasValue && _draftShowcaseIds.Contains(_draftCrownedGameId.Value)
                ? _draftCrownedGameId
                : null;
            var updatedProfile = new UserProfile
            {
                Id = _profile.Id,
                DisplayName = name,
                Handle = "@" + handle,
                Bio = EditBio.Trim(),
                AvatarUrl = avatarUrl,
                FavoritePlatform = string.IsNullOrWhiteSpace(EditFavoritePlatform) ? "PC" : EditFavoritePlatform.Trim(),
                ShowcaseGameIdsCsv = string.Join(",", _draftShowcaseIds),
                CrownedGameId = crownedGameId
            };

            await _gameRepository.UpdateUserProfileAsync(updatedProfile);
            _profile = updatedProfile;
            _draftCrownedGameId = crownedGameId;
            IsEditing = false;
            BuildShowcase();
            NotifyProfileChanged();
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка PostgreSQL", $"Не вдалося зберегти профіль: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ChooseAvatarAsync()
    {
        try
        {
            IReadOnlyList<FileResult>? photos = await MediaPicker.Default.PickPhotosAsync();
            FileResult? photo = photos?.FirstOrDefault();
            if (photo == null)
            {
                return;
            }

            string extension = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            string path = Path.Combine(FileSystem.AppDataDirectory, $"avatar-{Guid.NewGuid():N}{extension}");
            await using Stream input = await photo.OpenReadAsync();
            await using FileStream output = File.Create(path);
            await input.CopyToAsync(output);

            EditAvatarUrl = path;
        }
        catch (Exception exception)
        {
            await ShowAlertAsync("Помилка", $"Не вдалося відкрити зображення: {exception.Message}");
        }
    }

    // ------------------------------------------------------------------ misc

    private async Task ShowKpiInfoAsync()
    {
        await ShowAlertAsync(
            "Як рахується статистика",
            "Total — усі ігри в бібліотеці.\n" +
            "Played, Completed and Abandoned — ігри у відповідних статусах.\n" +
            "100% — ігри, позначені як mastered / platinum.\n" +
            "Hours — сума годин усіх записів щоденника.\n\n" +
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

    private static ImageSource? CreateAvatarSource(string? avatar)
    {
        if (string.IsNullOrWhiteSpace(avatar))
        {
            return null;
        }

        return Uri.TryCreate(avatar, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? ImageSource.FromUri(uri)
            : File.Exists(avatar) ? ImageSource.FromFile(avatar) : null;
    }

    private void NotifyProfileChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Handle));
        OnPropertyChanged(nameof(Bio));
        OnPropertyChanged(nameof(HasBio));
        OnPropertyChanged(nameof(HasNoBio));
        OnPropertyChanged(nameof(AvatarUrl));
        OnPropertyChanged(nameof(AvatarSource));
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(FavoritePlatform));
        OnPropertyChanged(nameof(HasRecentLogs));
        OnPropertyChanged(nameof(HasNoRecentLogs));
        OnPropertyChanged(nameof(HasNoRecentReviews));
    }
}