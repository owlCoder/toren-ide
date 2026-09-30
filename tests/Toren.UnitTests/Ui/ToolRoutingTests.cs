using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using NUnit.Framework;
using Toren.App.Execution.ViewModels;
using Toren.App.Terminal.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views;
using Toren.App.Views.Problems;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.Language.CSharp.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Ui;

[TestFixture]
public sealed class ToolRoutingTests
{
    [AvaloniaTest]
    [TestCase("Settings", false, false)]
    [TestCase("Settings", true, true)]
    [TestCase("Settings", false, true)]
    [TestCase("Settings", true, false)]
    [TestCase("Packages", false, false)]
    [TestCase("Packages", true, true)]
    [TestCase("Packages", false, true)]
    [TestCase("Packages", true, false)]
    public void ToolsOpenInSeparateWindowsAndReopenWithoutLosingTheirPanel(string title, bool light, bool minimumSize)
    {
        var shell = Toren.App.App.CreateMainWindow(Profile(), new UiInteractiveProcessRunner());
        Application.Current!.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
        ToolDialog? dialog = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            if (window is ToolDialog tool && tool.Title == title) dialog = tool;
        });
        var button = shell.FindControl<Button>($"{title}ActivityButton")!;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            Assert.That(dialog, Is.Not.Null);
            var first = dialog!;
            var panel = first.FindControl<ContentControl>("DialogContent")!.Content;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(dialog, Is.SameAs(first), "Repeated clicks should activate the existing window.");
            if (minimumSize) { first.Width = 640; first.Height = 480; }
            first.UpdateLayout();
            foreach (var control in first.GetVisualDescendants().OfType<Control>()
                         .Where(control => control is Button or TextBox or ComboBox))
            {
                if (!control.IsEffectivelyVisible || control.Bounds.Width <= 0) continue;
                var origin = control.TranslatePoint(default, first)!.Value;
                Assert.That(origin.X, Is.GreaterThanOrEqualTo(-1));
                Assert.That(origin.X + control.Bounds.Width, Is.LessThanOrEqualTo(first.Bounds.Width + 1));
            }

            Capture(first, $"{title}-{(light ? "light" : "dark")}-{(minimumSize ? "minimum" : "normal")}");
            first.Close();
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(dialog, Is.Not.SameAs(first));
            Assert.That(dialog!.FindControl<ContentControl>("DialogContent")!.Content, Is.SameAs(panel));
            Assert.That(shell.FindControl<TabControl>("ToolTabs")!.Items.Count, Is.EqualTo(5));
        }
        finally
        {
            dialog?.Close();
            ((MainWindowViewModel)shell.DataContext!).Dispose();
        }
    }

    [AvaloniaTest]
    public async Task TerminalStartsOnSelectionKeepsSessionsAndCanKillAndRestart()
    {
        var runner = new UiInteractiveProcessRunner();
        var shell = Toren.App.App.CreateMainWindow(Profile(), runner);
        var tabs = shell.FindControl<TabControl>("ToolTabs")!;
        var tab = tabs.Items.OfType<TabItem>().Single(item => item.Content is Toren.App.Views.Terminal.TerminalPanel);
        var panel = (Control)tab.Content!;
        var host = (TerminalHostViewModel)panel.DataContext!;
        Assert.That(runner.Sessions, Is.Empty);
        tabs.SelectedItem = tab;
        Assert.That(runner.Sessions, Has.Count.EqualTo(1));
        Assert.That(host.SelectedSession!.IsRunning, Is.True);
        tabs.SelectedIndex = 0;
        tabs.SelectedItem = tab;
        Assert.That(runner.Sessions, Has.Count.EqualTo(1), "Switching tabs must retain the running process.");
        host.AddSession();
        Assert.That(runner.Sessions, Has.Count.EqualTo(2));
        var kill = panel.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, "Kill"));
        kill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.That(runner.Sessions[1].Killed, Is.True);
        Assert.That(host.SelectedSession!.IsRunning, Is.False);
        Assert.That(runner.Sessions[0].Killed, Is.False);
        tabs.SelectedIndex = 0;
        tabs.SelectedItem = tab;
        Assert.That(runner.Sessions, Has.Count.EqualTo(3));
        await host.DisposeAsync();
        ((MainWindowViewModel)shell.DataContext!).Dispose();
    }

    [AvaloniaTest]
    [TestCase(DotNetCommandKind.Build, "BuildSolutionButton")]
    [TestCase(DotNetCommandKind.Rebuild, "RebuildSolutionButton")]
    [TestCase(DotNetCommandKind.Clean, "CleanSolutionButton")]
    public async Task ToolbarCommandsTargetTheSolutionAndShowProgressUntilCompletion(DotNetCommandKind kind, string name)
    {
        var service = new ControlledCommandService();
        var shell = Toren.App.App.CreateMainWindow(Profile(), new UiInteractiveProcessRunner(), service);
        var tabs = shell.FindControl<TabControl>("ToolTabs")!;
        var execution = (WorkspaceExecutionViewModel)((Control)((TabItem)tabs.Items[1]!).Content!).DataContext!;
        var path = Path.GetFullPath("Sample.slnx");
        execution.SetWorkspace(new WorkspaceDescriptor(path, "Sample", WorkspaceKind.SolutionX));
        execution.ConfigurationIndex = 1;
        shell.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var content = shell.Content;
        shell.Content = null;
        var host = new Window { Content = content, DataContext = shell.DataContext, Width = 1080, Height = 700 };
        host.Show();
        host.UpdateLayout();
        Capture(host, $"{kind}-progress");
        host.Close();
        Assert.That(service.Request!.Kind, Is.EqualTo(kind));
        Assert.That(service.Request.TargetPath, Is.EqualTo(path));
        Assert.That(service.Request.Configuration, Is.EqualTo("Release"));
        Assert.That(tabs.SelectedIndex, Is.EqualTo(1));
        Assert.That(shell.FindControl<ProgressBar>("ExecutionProgressBar")!.IsVisible, Is.True);
        Assert.That(shell.FindControl<Button>(name)!.IsEnabled, Is.False);
        service.Complete(kind);
        for (var attempt = 0; attempt < 30 && execution.IsRunning; attempt++) await Task.Delay(10);
        Assert.That(execution.IsRunning, Is.False);
        Assert.That(shell.FindControl<ProgressBar>("ExecutionProgressBar")!.IsVisible, Is.False);
        Assert.That(shell.FindControl<Button>(name)!.IsEnabled, Is.True);
        ((MainWindowViewModel)shell.DataContext!).Dispose();
    }

    [AvaloniaTest]
    public async Task ClickingAProblemOpensItsFileAndMovesToTheReportedLocation()
    {
        var profile = Profile();
        Directory.CreateDirectory(profile);
        var file = Path.Combine(profile, "Broken.cs");
        await File.WriteAllTextAsync(file, "class Broken\n{\n    int Value;\n}\n");
        var shell = Toren.App.App.CreateMainWindow(profile, new UiInteractiveProcessRunner());
        var model = (MainWindowViewModel)shell.DataContext!;
        model.Problems.Replace(file, [new CSharpDiagnostic("TEST001", "Go to declaration", CSharpDiagnosticSeverity.Error, 3, 5, 3, 10)]);
        var content = shell.Content;
        shell.Content = null;
        var window = new Window { Content = content, DataContext = model, Width = 1080, Height = 700 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var list = ((Control)content!).GetLogicalDescendants().OfType<ProblemsPanel>().Distinct().Single().FindControl<ListBox>("ProblemsList")!;
            var row = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            var point = row.TranslatePoint(new Point(40, 15), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            for (var attempt = 0; attempt < 40 && model.Documents.ActiveDocument is null; attempt++) await Task.Delay(10);
            Assert.That(model.Documents.ActiveDocument!.Path, Is.EqualTo(file));
            var editor = ((Control)content!).GetLogicalDescendants().OfType<TextEditor>().Distinct().Single(editor => editor.Name == "DocumentEditor");
            Assert.That(editor.Document.GetLocation(editor.CaretOffset).Line, Is.EqualTo(3));
            Assert.That(editor.Document.GetLocation(editor.CaretOffset).Column, Is.EqualTo(5));
        }
        finally { window.Close(); model.Dispose(); }
    }

    private static string Profile() => Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", $"tools-{Guid.NewGuid():N}");

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("TOREN_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        frame!.Save(Path.Combine(directory, $"dialog-{name}.png"), PngBitmapEncoderOptions.Default);
    }

    private sealed class ControlledCommandService : IDotNetCommandService
    {
        private readonly TaskCompletionSource<Result<DotNetCommandResult>> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DotNetCommandRequest? Request { get; private set; }
        public Task<Result<DotNetCommandResult>> ExecuteAsync(DotNetCommandRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return _completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(DotNetCommandKind kind) => _completion.SetResult(Result.Success(new DotNetCommandResult(kind, 0, "Done", "")));
    }
}
