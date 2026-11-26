using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CartridgeApp.ViewModels;

/// <summary>
/// Small base class for the new view models (INotifyPropertyChanged + alert helpers).
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected static Page? CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;

    protected static async Task ShowAlertAsync(string title, string message)
    {
        Page? page = CurrentPage;
        if (page != null)
        {
            await page.DisplayAlertAsync(title, message, "OK");
        }
    }

    protected static async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Page? page = CurrentPage;
        return page != null && await page.DisplayAlertAsync(title, message, accept, cancel);
    }
}