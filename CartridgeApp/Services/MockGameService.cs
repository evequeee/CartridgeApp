namespace CartridgeApp.Services;

using CartridgeApp.Models;

public class MockGameService
{
    public List<Game> GetGames()
    {
        return new List<Game>
        {
            new Game
            {
                Title = "Persona 4 Golden",
                Developer = "Atlus",
                Platform = "PC",
                Status = GameStatus.Completed,
                Rating = 10,
                IsFavorite = true
            },
            new Game
            {
                Title = "Slay the Spire",
                Developer = "Mega Crit",
                Platform = "Steam Deck",
                Status = GameStatus.Playing,
                Rating = 9,
                IsFavorite = true
            },
            new Game
            {
                Title = "Cyberpunk 2077",
                Developer = "CD Projekt Red",
                Platform = "PC",
                Status = GameStatus.Backlog,
                IsFavorite = true
            }
        };
    }
}