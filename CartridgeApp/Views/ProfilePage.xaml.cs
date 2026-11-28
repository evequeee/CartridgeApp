using CartridgeApp.ViewModels;

namespace CartridgeApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_viewModel.IsEditing)
        {
            await _viewModel.RefreshAsync();
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsEditing)
        {
            _viewModel.CancelEditCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}