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
                Status = GameStatus.Completed,
                Rating = 10
            },
            new Game
            {
                Title = "Slay the Spire",
                Developer = "Mega Crit",
                Status = GameStatus.Playing,
                Rating = 9
            },
            new Game
            {
                Title = "Cyberpunk 2077",
                Developer = "CD Projekt Red",
                Status = GameStatus.Backlog
            }
        };
    }
}