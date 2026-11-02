using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.ViewModels;

/// <summary>
/// Presents aggregated backlog statistics for the statistics page.
/// </summary>
public class StatsViewModel : INotifyPropertyChanged
{
    private readonly IGameRepository _gameRepository;
    private BacklogStats _stats = new();

    public StatsViewModel(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
        LoadStatsCommand = new Command(async () => await LoadStatsAsync());
        PlatformStats = new ObservableCollection<KeyValuePair<string, int>>();
        _ = LoadStatsAsync();
    }

    public ICommand LoadStatsCommand { get; }
    public ObservableCollection<KeyValuePair<string, int>> PlatformStats { get; }

    public int TotalGames => _stats.TotalGames;
    public double CompletedPercentage => _stats.CompletedPercentage;
    public int TotalHoursPlayed => _stats.TotalHoursPlayed;
    public int FavoriteGames => _stats.FavoriteGames;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadStatsAsync()
    {
        _stats = await _gameRepository.GetStatisticsAsync();
        PlatformStats.Clear();
        foreach (KeyValuePair<string, int> platform in _stats.GamesByPlatform)
        {
            PlatformStats.Add(platform);
        }

        OnPropertyChanged(nameof(TotalGames));
        OnPropertyChanged(nameof(CompletedPercentage));
        OnPropertyChanged(nameof(TotalHoursPlayed));
        OnPropertyChanged(nameof(FavoriteGames));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
