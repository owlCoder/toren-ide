using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using NUnit.Framework;
using Toren.App.ViewModels;

namespace Toren.UnitTests.Ui;

[TestFixture]
public sealed class EditorThemeTests
{
    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public async Task DocumentEditorSharesTheShellPaletteAfterThemeChanges(bool light)
    {
        var profile = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", $"editor-theme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profile);
        var sourcePath = Path.Combine(profile, "WorkspaceStatus.cs");
        await File.WriteAllTextAsync(sourcePath, SampleSource);
        var shell = Toren.App.App.CreateMainWindow(profile);
        var model = (MainWindowViewModel)shell.DataContext!;
        var editor = shell.FindControl<TextEditor>("DocumentEditor")!;
        var toggle = shell.FindControl<Button>("ThemeToggleButton")!;
        // Exercise a round trip, including TextMate's theme callback with an open document.
        var opened = await model.Documents.OpenAsync(sourcePath);
        Assert.That(opened.IsSuccess, Is.True);
        model.IsWelcomeSelected = false;
        model.SdkSummary = ".NET SDK: 10.0.100";
        model.StatusText = "WorkspaceStatus.cs — UTF-8";
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!light)
        {
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        var variant = light ? ThemeVariant.Light : ThemeVariant.Dark;
        Assert.That(shell.TryFindResource("TorenBackgroundBrush", variant, out var background), Is.True);
        Assert.That(((ISolidColorBrush)editor.Background!).Color, Is.EqualTo(((ISolidColorBrush)background!).Color),
            "TextMate must not replace the shell's editor background with another palette.");
        Assert.That(((ISolidColorBrush)editor.TextArea.TextView.CurrentLineBorder!.Brush!).Color,
            Is.EqualTo(((ISolidColorBrush)editor.TextArea.TextView.CurrentLineBackground!).Color),
            "The current line should use a neutral highlight instead of a fallback outline.");

        var content = shell.Content;
        shell.Content = null;
        var host = new Window { Content = content, DataContext = model, Width = 1440, Height = 900 };
        host.Show();
        try
        {
            host.UpdateLayout();
            editor.Focus();
            editor.Select(SampleSource.IndexOf("DisplayName", StringComparison.Ordinal), "DisplayName".Length);
            Dispatcher.UIThread.RunJobs();
            host.UpdateLayout();
            var directory = Environment.GetEnvironmentVariable("TOREN_UI_CAPTURE_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                using var frame = host.CaptureRenderedFrame();
                frame!.Save(Path.Combine(directory, $"1440x900-{(light ? "light" : "dark")}-editor.png"), PngBitmapEncoderOptions.Default);
            }
        }
        finally
        {
            host.Close();
            model.Dispose();
        }
    }

    private const string SampleSource = """
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Toren.Workspaces;

        /// <summary>Loads a workspace without blocking the editor.</summary>
        public sealed class WorkspaceStatus
        {
            private readonly IWorkspaceProvider _provider;

            public WorkspaceStatus(IWorkspaceProvider provider)
            {
                _provider = provider;
            }

            public string DisplayName { get; private set; } = "Toren IDE";

            public async Task<IReadOnlyList<Project>> OpenAsync(
                string path,
                CancellationToken cancellationToken = default)
            {
                // Keep standard .NET projects portable across tools.
                var workspace = await _provider.LoadAsync(path, cancellationToken);
                DisplayName = workspace.Name;

                if (workspace.Projects.Count == 0)
                {
                    return [];
                }

                return workspace.Projects;
            }
        }
        """;
}
