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
    private readonly IBackupService _backupService;
    private BacklogStats _stats = new();
    private bool _isLoading;

    public StatsViewModel(IGameRepository gameRepository, IBackupService backupService)
    {
        _gameRepository = gameRepository;
        _backupService = backupService;
        LoadStatsCommand = new Command(async () => await LoadStatsAsync());
        ExportBackupCommand = new Command(async () => await ExportBackupAsync());
        ImportBackupCommand = new Command(async () => await ImportBackupAsync());
        PlatformStats = new ObservableCollection<KeyValuePair<string, int>>();
        _ = LoadStatsAsync();
    }

    public ICommand LoadStatsCommand { get; }
    public ICommand ExportBackupCommand { get; }
    public ICommand ImportBackupCommand { get; }
    public ObservableCollection<KeyValuePair<string, int>> PlatformStats { get; }

    public int TotalGames => _stats.TotalGames;
    public double CompletedPercentage => _stats.CompletedPercentage;
    public double CompletedProgress => Math.Clamp(_stats.CompletedPercentage / 100d, 0d, 1d);
    public int TotalHoursPlayed => _stats.TotalHoursPlayed;
    public int FavoriteGames => _stats.FavoriteGames;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadStatsAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        try
        {
            await _gameRepository.EnsureDatabaseAsync();
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
            OnPropertyChanged(nameof(CompletedProgress));
        }
        catch (Exception exception)
        {
            Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Помилка PostgreSQL", $"Не вдалося завантажити статистику: {exception.Message}", "OK");
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ExportBackupAsync()
    {
        try
        {
            string json = await _backupService.ExportAsync();
            string path = Path.Combine(FileSystem.CacheDirectory, "cartridge-backup.json");
            await File.WriteAllTextAsync(path, json);

            Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Бекап", $"Експортовано до:\n{path}", "OK");
            }
        }
        catch (Exception exception)
        {
            await ShowErrorAsync($"Не вдалося експортувати бекап: {exception.Message}");
        }
    }

    private async Task ImportBackupAsync()
    {
        FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
        {
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = ["application/json", "text/json"],
                [DevicePlatform.iOS] = ["public.json"],
                [DevicePlatform.MacCatalyst] = ["public.json"],
                [DevicePlatform.WinUI] = [".json"]
            })
        });

        if (file == null)
        {
            return;
        }

        await using Stream stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        int added = await _backupService.ImportAsync(await reader.ReadToEndAsync());
        await LoadStatsAsync();

        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page != null)
        {
            await page.DisplayAlertAsync("Імпорт", $"Додано ігор: {added}", "OK");
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page != null)
        {
            await page.DisplayAlertAsync("Помилка", message, "OK");
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
