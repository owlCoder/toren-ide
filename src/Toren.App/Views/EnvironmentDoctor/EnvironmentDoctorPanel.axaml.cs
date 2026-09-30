using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.EnvironmentDoctor.ViewModels;

namespace Toren.App.Views.EnvironmentDoctor;

internal sealed partial class EnvironmentDoctorPanel : UserControl
{
    public EnvironmentDoctorPanel()
    {
        InitializeComponent();
    }

    private async void Refresh_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is EnvironmentDoctorViewModel viewModel)
        {
            await viewModel.RefreshAsync().ConfigureAwait(true);
        }
    }
}
