namespace CartridgeApp.Services;

using CartridgeApp.Models;

public class MockGameService : IGameRepository
{
    private int _nextGameId = 7;
    private int _nextLogId = 6;

    private static string SteamCover(int appId) =>
        $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900.jpg";

    private readonly List<Game> _games =
    [
        new Game
        {
            Id = 1,
            Title = "Persona 4 Golden",
            Developer = "Atlus",
            Platform = "PC",
            Status = GameStatus.Completed,
            Rating = 5.0,
            HoursPlayed = 85,
            IsFavorite = true,
            IsLiked = true,
            IsMastered = true,
            CoverUrl = SteamCover(1113000)
        },
        new Game
        {
            Id = 2,
            Title = "Slay the Spire",
            Developer = "Mega Crit",
            Platform = "Steam Deck",
            Status = GameStatus.Playing,
            Rating = 4.5,
            HoursPlayed = 42,
            IsFavorite = true,
            IsLiked = true,
            CoverUrl = SteamCover(646570)
        },
        new Game
        {
            Id = 3,
            Title = "Cyberpunk 2077",
            Developer = "CD Projekt Red",
            Platform = "PC",
            Status = GameStatus.Backlog,
            Rating = 3.5,
            IsFavorite = true,
            CoverUrl = SteamCover(1091500)
        },
        new Game
        {
            Id = 4,
            Title = "Hades",
            Developer = "Supergiant Games",
            Platform = "PC",
            Status = GameStatus.Completed,
            Rating = 4.5,
            HoursPlayed = 30,
            IsLiked = true,
            CoverUrl = SteamCover(1145360)
        },
        new Game
        {
            Id = 5,
            Title = "Baldur's Gate 3",
            Developer = "Larian Studios",
            Platform = "PC",
            Status = GameStatus.Playing,
            Rating = 5.0,
            HoursPlayed = 60,
            IsFavorite = true,
            CoverUrl = SteamCover(1086940)
        },
        new Game
        {
            Id = 6,
            Title = "Elden Ring",
            Developer = "FromSoftware",
            Platform = "PlayStation",
            Status = GameStatus.Backlog,
            CoverUrl = SteamCover(1245620)
        }
    ];

    private readonly List<PlayLog> _logs;
    private UserProfile _profile = new()
    {
        Id = 1,
        DisplayName = "Player One",
        Handle = "@cartridge_hunter",
        Bio = "Збираю беклог, полюю на платини й намагаюсь пройти бодай половину того, що купив торік.",
        FavoritePlatform = "PC / PS5",
        ShowcaseGameIdsCsv = "1,5,4,2"
    };

    public MockGameService()
    {
        _logs =
        [
            new PlayLog
            {
                Id = 1,
                GameId = 1,
                LoggedDate = DateTime.Today.AddDays(-21),
                HoursSession = 12.5,
                StatusAfterSession = GameStatus.Completed,
                ReviewText = "Фінал і True Ending пройдені.",
                IsReplay = false
            },
            new PlayLog
            {
                Id = 2,
                GameId = 4,
                LoggedDate = DateTime.Today.AddDays(-9),
                HoursSession = 2.0,
                StatusAfterSession = GameStatus.Completed,
                ReviewText = "Ще один забіг на високому Heat.",
                IsReplay = true
            },
            new PlayLog
            {
                Id = 3,
                GameId = 2,
                LoggedDate = DateTime.Today.AddDays(-3),
                HoursSession = 3.0,
                StatusAfterSession = GameStatus.Playing,
                ReviewText = "Третій акт із Silent.",
                IsReplay = false
            },
            new PlayLog
            {
                Id = 4,
                GameId = 5,
                LoggedDate = DateTime.Today.AddDays(-2),
                HoursSession = 4.5,
                StatusAfterSession = GameStatus.Playing,
                ReviewText = "Другий акт, розмови з компаньйонами.",
                IsReplay = false
            },
            new PlayLog
            {
                Id = 5,
                GameId = 1,
                LoggedDate = DateTime.Today.AddDays(-1),
                HoursSession = 6.0,
                StatusAfterSession = GameStatus.Completed,
                ReviewText = "Другий прохід заради 100%.",
                IsReplay = true
            }
        ];
    }

    public List<Game> GetGames()
    {
        return _games.ToList();
    }

    public Task<IReadOnlyList<Game>> GetGamesAsync()
    {
        return Task.FromResult<IReadOnlyList<Game>>(_games.ToList());
    }

    public Task AddGameAsync(Game game)
    {
        if (game.Id == 0)
        {
            game.Id = _nextGameId++;
        }

        _games.Add(game);
        return Task.CompletedTask;
    }

    public Task UpdateGameAsync(Game game)
    {
        int index = _games.FindIndex(existing => existing.Id == game.Id);
        if (index >= 0)
        {
            _games[index] = game;
        }

        return Task.CompletedTask;
    }

    public Task DeleteGameAsync(Game game)
    {
        _games.RemoveAll(existing => existing.Id == game.Id);
        _logs.RemoveAll(log => log.GameId == game.Id);
        return Task.CompletedTask;
    }

    public Task EnsureDatabaseAsync()
    {
        return Task.CompletedTask;
    }

    public Task<BacklogStats> GetStatisticsAsync()
    {
        int total = _games.Count;
        int completed = _games.Count(game => game.Status == GameStatus.Completed);
        var stats = new BacklogStats
        {
            TotalGames = total,
            CompletedPercentage = total == 0 ? 0 : completed * 100d / total,
            TotalHoursPlayed = _games.Sum(game => game.HoursPlayed),
            FavoriteGames = _games.Count(game => game.IsFavorite),
            GamesByPlatform = _games
                .GroupBy(game => game.Platform)
                .ToDictionary(group => group.Key, group => group.Count())
        };

        return Task.FromResult(stats);
    }

    public Task AddPlayLogAsync(PlayLog log)
    {
        if (log.Id == 0)
        {
            log.Id = _nextLogId++;
        }

        _logs.Add(log);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlayLog>> GetPlayLogsForGameAsync(int gameId)
    {
        IReadOnlyList<PlayLog> result = _logs
            .Where(log => log.GameId == gameId)
            .OrderByDescending(log => log.LoggedDate)
            .ThenByDescending(log => log.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<PlayLog>> GetRecentLogsAsync(int take = 100)
    {
        IReadOnlyList<PlayLog> result = _logs
            .OrderByDescending(log => log.LoggedDate)
            .ThenByDescending(log => log.Id)
            .Take(take)
            .Select(log =>
            {
                log.Game = _games.FirstOrDefault(game => game.Id == log.GameId)!;
                return log;
            })
            .Where(log => log.Game != null)
            .ToList();

        return Task.FromResult(result);
    }

    public Task DeletePlayLogAsync(PlayLog log)
    {
        _logs.RemoveAll(existing => existing.Id == log.Id);
        return Task.CompletedTask;
    }

    public Task<UserProfile> GetUserProfileAsync()
    {
        return Task.FromResult(new UserProfile
        {
            Id = _profile.Id,
            DisplayName = _profile.DisplayName,
            Handle = _profile.Handle,
            Bio = _profile.Bio,
            AvatarUrl = _profile.AvatarUrl,
            FavoritePlatform = _profile.FavoritePlatform,
            ShowcaseGameIdsCsv = _profile.ShowcaseGameIdsCsv
        });
    }

    public Task UpdateUserProfileAsync(UserProfile profile)
    {
        profile.Id = _profile.Id;
        _profile = profile;
        return Task.CompletedTask;
    }
}