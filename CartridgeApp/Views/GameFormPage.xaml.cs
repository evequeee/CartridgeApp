using CartridgeApp.ViewModels;

namespace CartridgeApp.Views;

public partial class GameFormPage : ContentPage
{
    public GameFormPage(GameFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
