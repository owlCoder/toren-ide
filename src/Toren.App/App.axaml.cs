using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Toren.App.ViewModels;
using Toren.DotNet.Environment;
using Toren.Platform.Execution;
using Toren.Workspaces;

namespace Toren.App;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var processRunner = new SystemProcessRunner();
            var dotNetEnvironmentService = new DotNetEnvironmentService(processRunner);
            var workspaceClassifier = new WorkspaceClassifier();
            var viewModel = new MainWindowViewModel(dotNetEnvironmentService, workspaceClassifier);

            desktop.MainWindow = new MainWindow(viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
