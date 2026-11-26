using Microsoft.EntityFrameworkCore;

using CartridgeApp.Data;
using CartridgeApp.Models;

namespace CartridgeApp.Services;

/// <summary>
/// PostgreSQL-backed implementation of the game repository.
/// </summary>
/// <remarks>
/// In a MAUI app the scoped <see cref="CartridgeDbContext"/> effectively lives for the whole process,
/// and several view models can call the repository at the same time, so every operation is serialized
/// with a semaphore (DbContext is not thread-safe) and the change tracker is cleared after writes
/// (reads use AsNoTracking and writes attach fresh instances, so stale tracked copies would clash).
/// </remarks>
public class PostgresGameRepository : IGameRepository
{
    private readonly CartridgeDbContext _dbContext;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public PostgresGameRepository(CartridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ------------------------------------------------------------------ seed data

    private sealed record SeedGame(
        string Title,
        string Developer,
        string Platform,
        GameStatus Status,
        double Rating,
        int HoursPlayed,
        bool IsFavorite,
        bool IsLiked,
        bool IsMastered,
        int SteamAppId);

    private sealed record SeedLog(
        string Title,
        string Platform,
        int DaysAgo,
        double Hours,
        GameStatus StatusAfter,
        string Review,
        bool IsReplay);

    // Direct vertical 2:3 posters (600x900) from the public Steam CDN. If an image is ever unavailable
    // the cards still show the XMB placeholder poster; any other URL can be pasted in the game form.
    private static string SteamCover(int appId) =>
        $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900.jpg";

    private static readonly SeedGame[] SeedGames =
    [
        new("Persona 4 Golden", "Atlus", "PC", GameStatus.Completed, 5.0, 85, true, true, true, 1113000),
        new("Slay the Spire", "Mega Crit", "Steam Deck", GameStatus.Playing, 4.5, 42, true, true, false, 646570),
        new("Cyberpunk 2077", "CD Projekt Red", "PC", GameStatus.Backlog, 3.5, 0, false, false, false, 1091500),
        new("Hades", "Supergiant Games", "PC", GameStatus.Completed, 4.5, 30, false, true, false, 1145360),
        new("Baldur's Gate 3", "Larian Studios", "PC", GameStatus.Playing, 5.0, 60, true, false, false, 1086940),
        new("Elden Ring", "FromSoftware", "PlayStation", GameStatus.Backlog, 0.0, 0, false, false, false, 1245620)
    ];

    private static readonly SeedLog[] SeedLogs =
    [
        new("Persona 4 Golden", "PC", 21, 12.5, GameStatus.Completed,
            "Фінал і True Ending пройдені. Один із найкращих JRPG — атмосфера Інаби чіпляє до кінця.", false),
        new("Hades", "PC", 9, 2.0, GameStatus.Completed,
            "Ще один вечірній забіг на високому Heat. Кожна спроба відчувається по-новому.", true),
        new("Slay the Spire", "Steam Deck", 3, 3.0, GameStatus.Playing,
            "Дійшов до третього акту з Silent, але не вистачило сильного білда. Спробую ще раз.", false),
        new("Baldur's Gate 3", "PC", 2, 4.5, GameStatus.Playing,
            "Другий акт: розмови з компаньйонами — окремий вид задоволення. Зберігаюсь перед кожним вибором.", false),
        new("Persona 4 Golden", "PC", 1, 6.0, GameStatus.Completed,
            "Другий прохід заради 100%: закриваю соціальні лінки та Compendium.", true)
    ];

    private static readonly string[] ShowcaseSeedTitles =
    [
        "Persona 4 Golden", "Baldur's Gate 3", "Hades", "Slay the Spire"
    ];

    // ------------------------------------------------------------------ initialization / schema upgrade

    public async Task EnsureDatabaseAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            await _dbContext.Database.EnsureCreatedAsync();

            // EnsureCreated does nothing for a database that already has tables, so an existing library
            // gets new columns / tables through idempotent SQL. The "existed" flags tell us which one-time
            // demo data still has to be added.
            bool coverColumnExisted = await ColumnExistsAsync("Games", "CoverUrl");
            bool playLogsTableExisted = await TableExistsAsync("PlayLogs");
            bool profileTableExisted = await TableExistsAsync("UserProfiles");

            await UpgradeSchemaAsync();

            bool freshLibrary = !await _dbContext.Games.AnyAsync();

            if (freshLibrary)
            {
                await SeedGamesAsync();
                await SeedProfileAsync();
                await SeedPlayLogsAsync(null);
            }
            else
            {
                if (!coverColumnExisted)
                {
                    await BackfillSeedGameDataAsync();
                }

                HashSet<string> newTitles = new();
                if (!profileTableExisted)
                {
                    newTitles = await AddMissingSeedGamesAsync();
                    await SeedProfileAsync();
                }

                if (!playLogsTableExisted)
                {
                    await SeedPlayLogsAsync(null);
                }
                else if (newTitles.Count > 0)
                {
                    await SeedPlayLogsAsync(newTitles);
                }
            }

            _dbContext.ChangeTracker.Clear();
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> ColumnExistsAsync(string table, string column)
    {
        return await _dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = current_schema() AND table_name = {table} AND column_name = {column}
            ) AS "Value"
            """).FirstAsync();
    }

    private async Task<bool> TableExistsAsync(string table)
    {
        return await _dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = current_schema() AND table_name = {table}
            ) AS "Value"
            """).FirstAsync();
    }

    private async Task UpgradeSchemaAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "Games" ADD COLUMN IF NOT EXISTS "CoverUrl" character varying(1000) NULL""");

        await _dbContext.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "Games" ADD COLUMN IF NOT EXISTS "IsLiked" boolean NOT NULL DEFAULT FALSE""");

        await _dbContext.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "Games" ADD COLUMN IF NOT EXISTS "IsMastered" boolean NOT NULL DEFAULT FALSE""");

        // Old scale: nullable integer 1..10. New scale: double 0.0..5.0 (0.0 = not rated),
        // so 10 -> 5.0, 9 -> 4.5, 7 -> 3.5 and NULL -> 0.0. Runs only while the column is still an integer.
        await _dbContext.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'Games'
                      AND column_name = 'Rating'
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE "Games" ALTER COLUMN "Rating" TYPE double precision
                        USING (COALESCE("Rating", 0)::double precision / 2.0);
                    ALTER TABLE "Games" ALTER COLUMN "Rating" SET NOT NULL;
                END IF;
            END
            $$
            """);

        await _dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PlayLogs" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "GameId" integer NOT NULL,
                "LoggedDate" timestamp without time zone NOT NULL,
                "HoursSession" double precision NOT NULL,
                "StatusAfterSession" text NOT NULL,
                "ReviewText" character varying(4000) NULL,
                "IsReplay" boolean NOT NULL,
                CONSTRAINT "PK_PlayLogs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayLogs_Games_GameId" FOREIGN KEY ("GameId")
                    REFERENCES "Games" ("Id") ON DELETE CASCADE
            )
            """);

        await _dbContext.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_PlayLogs_GameId" ON "PlayLogs" ("GameId")""");

        await _dbContext.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_PlayLogs_LoggedDate" ON "PlayLogs" ("LoggedDate")""");

        await _dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserProfiles" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "DisplayName" character varying(100) NOT NULL,
                "Handle" character varying(60) NOT NULL,
                "Bio" character varying(1000) NOT NULL,
                "AvatarUrl" character varying(1000) NULL,
                "FavoritePlatform" character varying(100) NOT NULL,
                "ShowcaseGameIdsCsv" character varying(200) NOT NULL,
                CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("Id")
            )
            """);

        // The first demo build used the legacy Steam CDN host; move those covers to the new poster host.
        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Games"
            SET "CoverUrl" = REPLACE("CoverUrl",
                'https://cdn.cloudflare.steamstatic.com/steam/apps/',
                'https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/')
            WHERE "CoverUrl" LIKE 'https://cdn.cloudflare.steamstatic.com/steam/apps/%'
            """);
    }

    private static Game ToGame(SeedGame seed)
    {
        return new Game
        {
            Title = seed.Title,
            Developer = seed.Developer,
            Platform = seed.Platform,
            Status = seed.Status,
            Rating = seed.Rating,
            HoursPlayed = seed.HoursPlayed,
            IsFavorite = seed.IsFavorite,
            IsLiked = seed.IsLiked,
            IsMastered = seed.IsMastered,
            CoverUrl = SteamCover(seed.SteamAppId)
        };
    }

    private async Task SeedGamesAsync()
    {
        _dbContext.Games.AddRange(SeedGames.Select(ToGame));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    /// <summary>One-time: adds demo games the library does not contain yet. Returns the titles that were added.</summary>
    private async Task<HashSet<string>> AddMissingSeedGamesAsync()
    {
        var added = new HashSet<string>();

        foreach (SeedGame seed in SeedGames)
        {
            bool exists = await _dbContext.Games
                .AnyAsync(item => item.Title == seed.Title && item.Platform == seed.Platform);

            if (!exists)
            {
                _dbContext.Games.Add(ToGame(seed));
                added.Add(seed.Title);
            }
        }

        if (added.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
        }

        return added;
    }

    private async Task BackfillSeedGameDataAsync()
    {
        foreach (SeedGame seed in SeedGames)
        {
            Game? game = await _dbContext.Games
                .FirstOrDefaultAsync(item => item.Title == seed.Title && item.Platform == seed.Platform);

            if (game == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(game.CoverUrl))
            {
                game.CoverUrl = SteamCover(seed.SteamAppId);
            }

            game.IsLiked = seed.IsLiked;
            game.IsMastered = seed.IsMastered;
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedProfileAsync()
    {
        if (await _dbContext.UserProfiles.AnyAsync())
        {
            return;
        }

        var showcaseIds = new List<int>();
        foreach (string title in ShowcaseSeedTitles)
        {
            int? gameId = await _dbContext.Games
                .Where(item => item.Title == title)
                .OrderBy(item => item.Id)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync();

            showcaseIds.Add(gameId ?? 0);
        }

        _dbContext.UserProfiles.Add(new UserProfile
        {
            DisplayName = "Player One",
            Handle = "@cartridge_hunter",
            Bio = "Збираю беклог, полюю на платини й намагаюсь пройти бодай половину того, що купив торік.",
            FavoritePlatform = "PC / PS5",
            ShowcaseGameIdsCsv = string.Join(",", showcaseIds)
        });

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedPlayLogsAsync(HashSet<string>? onlyTitles)
    {
        foreach (SeedLog seed in SeedLogs)
        {
            if (onlyTitles != null && !onlyTitles.Contains(seed.Title))
            {
                continue;
            }

            int? gameId = await _dbContext.Games
                .Where(item => item.Title == seed.Title && item.Platform == seed.Platform)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync();

            if (gameId == null)
            {
                continue;
            }

            _dbContext.PlayLogs.Add(new PlayLog
            {
                GameId = gameId.Value,
                LoggedDate = NormalizeDate(DateTime.Today.AddDays(-seed.DaysAgo)),
                HoursSession = seed.Hours,
                StatusAfterSession = seed.StatusAfter,
                ReviewText = seed.Review,
                IsReplay = seed.IsReplay
            });
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    // ------------------------------------------------------------------ games

    public Task<IReadOnlyList<Game>> GetGamesAsync()
    {
        return QueryAsync<IReadOnlyList<Game>>(async () => await _dbContext.Games
            .AsNoTracking()
            .OrderBy(game => game.Title)
            .ToListAsync());
    }

    public Task AddGameAsync(Game game)
    {
        return ExecuteAsync(async () =>
        {
            await _dbContext.Games.AddAsync(game);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
        });
    }

    public Task UpdateGameAsync(Game game)
    {
        return ExecuteAsync(async () =>
        {
            _dbContext.Games.Update(game);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
        });
    }

    public Task DeleteGameAsync(Game game)
    {
        return ExecuteAsync(async () =>
        {
            Game? trackedGame = await _dbContext.Games.FindAsync(game.Id);
            if (trackedGame != null)
            {
                _dbContext.Games.Remove(trackedGame);
                await _dbContext.SaveChangesAsync();
            }

            _dbContext.ChangeTracker.Clear();
        });
    }

    public Task<BacklogStats> GetStatisticsAsync()
    {
        return QueryAsync(async () =>
        {
            IReadOnlyList<Game> games = await _dbContext.Games.AsNoTracking().ToListAsync();
            int total = games.Count;
            int completed = games.Count(game => game.Status == GameStatus.Completed);

            return new BacklogStats
            {
                TotalGames = total,
                CompletedPercentage = total == 0 ? 0 : completed * 100d / total,
                TotalHoursPlayed = games.Sum(game => game.HoursPlayed),
                FavoriteGames = games.Count(game => game.IsFavorite),
                GamesByPlatform = games
                    .GroupBy(game => game.Platform)
                    .ToDictionary(group => group.Key, group => group.Count())
            };
        });
    }

    // ------------------------------------------------------------------ play logs

    public Task AddPlayLogAsync(PlayLog log)
    {
        return ExecuteAsync(async () =>
        {
            // Insert a clean copy: attaching log.Game would make EF try to re-insert / re-attach the game.
            var entity = new PlayLog
            {
                GameId = log.GameId,
                LoggedDate = NormalizeDate(log.LoggedDate),
                HoursSession = log.HoursSession,
                StatusAfterSession = log.StatusAfterSession,
                ReviewText = string.IsNullOrWhiteSpace(log.ReviewText) ? null : log.ReviewText.Trim(),
                IsReplay = log.IsReplay
            };

            await _dbContext.PlayLogs.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            log.Id = entity.Id;
            _dbContext.ChangeTracker.Clear();
        });
    }

    public Task<IReadOnlyList<PlayLog>> GetPlayLogsForGameAsync(int gameId)
    {
        return QueryAsync<IReadOnlyList<PlayLog>>(async () => await _dbContext.PlayLogs
            .AsNoTracking()
            .Where(log => log.GameId == gameId)
            .OrderByDescending(log => log.LoggedDate)
            .ThenByDescending(log => log.Id)
            .ToListAsync());
    }

    public Task<IReadOnlyList<PlayLog>> GetRecentLogsAsync(int take = 100)
    {
        return QueryAsync<IReadOnlyList<PlayLog>>(async () => await _dbContext.PlayLogs
            .AsNoTracking()
            .Include(log => log.Game)
            .OrderByDescending(log => log.LoggedDate)
            .ThenByDescending(log => log.Id)
            .Take(take)
            .ToListAsync());
    }

    public Task DeletePlayLogAsync(PlayLog log)
    {
        return ExecuteAsync(async () =>
        {
            PlayLog? tracked = await _dbContext.PlayLogs.FindAsync(log.Id);
            if (tracked != null)
            {
                _dbContext.PlayLogs.Remove(tracked);
                await _dbContext.SaveChangesAsync();
            }

            _dbContext.ChangeTracker.Clear();
        });
    }

    // ------------------------------------------------------------------ profile

    public Task<UserProfile> GetUserProfileAsync()
    {
        return QueryAsync(async () =>
        {
            UserProfile? profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync();

            if (profile == null)
            {
                profile = new UserProfile();
                _dbContext.UserProfiles.Add(profile);
                await _dbContext.SaveChangesAsync();
                _dbContext.ChangeTracker.Clear();
            }

            return profile;
        });
    }

    public Task UpdateUserProfileAsync(UserProfile profile)
    {
        return ExecuteAsync(async () =>
        {
            int? existingId = await _dbContext.UserProfiles
                .OrderBy(item => item.Id)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync();

            if (existingId == null)
            {
                profile.Id = 0;
                _dbContext.UserProfiles.Add(profile);
            }
            else
            {
                profile.Id = existingId.Value;
                _dbContext.UserProfiles.Update(profile);
            }

            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
        });
    }

    // ------------------------------------------------------------------ helpers

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<T> QueryAsync<T>(Func<Task<T>> query)
    {
        await _gate.WaitAsync();
        try
        {
            return await query();
        }
        finally
        {
            _gate.Release();
        }
    }
}