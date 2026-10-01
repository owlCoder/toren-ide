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
using Toren.App.Shell;
using Toren.App.Settings.ViewModels;
using Toren.App.Packages.ViewModels;
using Toren.App.Packages.Services;
using Toren.DotNet.Packages.Services;
using Toren.Platform.Execution.Adapters;
using Toren.Workspaces.Services;
using Toren.Workspaces.Contracts;
using Toren.DotNet.Packages.Contracts;
using Toren.DotNet.Packages.Models;
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
            Dispatcher.UIThread.RunJobs();
            Assert.That(first.FocusManager!.GetFocusedElement(), Is.SameAs(((Control)panel!).FindControl<TextBox>(
                title == "Settings" ? "SettingsSearchBox" : "PackageSearchBox")));
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
            Assert.That(dialog.Width, Is.EqualTo(first.Width));
            Assert.That(dialog.Height, Is.EqualTo(first.Height));
            Assert.That(shell.FindControl<TabControl>("ToolTabs")!.Items.Count, Is.EqualTo(5));
        }
        finally
        {
            dialog?.Close();
            ((MainWindowViewModel)shell.DataContext!).Dispose();
        }
    }

    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public async Task AboutDialogClosesWithEscapeAndPreservesItsOwner(bool light)
    {
        Application.Current!.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
        var owner = new Window { Width = 1080, Height = 700 };
        AboutWindow? dialog = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            if (window is AboutWindow about) dialog = about;
        });
        owner.Show();
        try
        {
            var closed = AboutWindow.ShowAsync(owner);
            Assert.That(dialog, Is.Not.Null);
            dialog!.UpdateLayout();
            foreach (var label in dialog.GetVisualDescendants().OfType<TextBlock>().Where(label => label.IsEffectivelyVisible))
            {
                var origin = label.TranslatePoint(default, dialog)!.Value;
                Assert.That(origin.X + label.Bounds.Width, Is.LessThanOrEqualTo(dialog.Bounds.Width + 1));
                Assert.That(origin.Y + label.Bounds.Height, Is.LessThanOrEqualTo(dialog.Bounds.Height + 1));
            }
            Capture(dialog, $"About-{(light ? "light" : "dark")}");
            dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            await closed;
            Assert.That(owner.IsVisible, Is.True);
            Assert.That(dialog.IsVisible, Is.False);
        }
        finally { dialog?.Close(); owner.Close(); }
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
        ToolPanelController.For(shell).Hide();
        shell.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var content = shell.Content;
        shell.Content = null;
        var host = new Window { Content = content, DataContext = shell.DataContext, Width = 1080, Height = 700 };
        host.Show();
        host.UpdateLayout();
        Capture(host, $"{kind}-progress");
        host.Close();
        Assert.That(service.Request!.Kind, Is.EqualTo(kind));
        Assert.That(ToolPanelController.For(shell).IsVisible, Is.True);
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
            var problems = ((Control)content!).GetLogicalDescendants().OfType<ProblemsPanel>().Distinct().Single();
            var activations = 0;
            problems.ProblemActivated += _ => activations++;
            var list = problems.FindControl<ListBox>("ProblemsList")!;
            var row = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            var point = row.TranslatePoint(new Point(40, 15), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            for (var attempt = 0; attempt < 40 && model.Documents.ActiveDocument is null; attempt++) await Task.Delay(10);
            Assert.That(model.Documents.ActiveDocument!.Path, Is.EqualTo(file));
            var editor = ((Control)content!).GetLogicalDescendants().OfType<TextEditor>().Distinct().Single(editor => editor.Name == "DocumentEditor");
            Assert.That(editor.Document.GetLocation(editor.CaretOffset).Line, Is.EqualTo(3));
            Assert.That(editor.Document.GetLocation(editor.CaretOffset).Column, Is.EqualTo(5));
            var emptyArea = list.TranslatePoint(new Point(40, list.Bounds.Height - 20), window)!.Value;
            window.MouseDown(emptyArea, MouseButton.Left);
            window.MouseUp(emptyArea, MouseButton.Left);
            Assert.That(activations, Is.EqualTo(1), "Clicking the list background should not reopen the previously selected problem.");
        }
        finally { window.Close(); model.Dispose(); }
    }

    private static string Profile() => Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", $"tools-{Guid.NewGuid():N}");

    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public void ToolPanelCanHideExpandAndRestoreWithoutStoppingTheTerminal(bool light)
    {
        Application.Current!.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
        var runner = new UiInteractiveProcessRunner();
        var shell = Toren.App.App.CreateMainWindow(Profile(), runner);
        var model = (MainWindowViewModel)shell.DataContext!;
        var tabs = shell.FindControl<TabControl>("ToolTabs")!;
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(tab => tab.Content is Toren.App.Views.Terminal.TerminalPanel);
        var controller = ToolPanelController.For(shell);
        var grid = shell.FindControl<Grid>("ShellGrid")!;
        grid.RowDefinitions[3].Height = new GridLength(350);
        var content = shell.Content;
        shell.Content = null;
        var host = new Window { Content = content, DataContext = model, Width = 1080, Height = 700 };
        host.Show();
        try
        {
            controller.Hide();
            host.UpdateLayout();
            Assert.That(grid.RowDefinitions[3].ActualHeight, Is.Zero);
            Assert.That(runner.Sessions.Single().Killed, Is.False);
            Capture(host, $"panel-hidden-{(light ? "light" : "dark")}");
            shell.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.J, KeyModifiers = KeyModifiers.Meta });
            host.UpdateLayout();
            Assert.That(controller.IsVisible, Is.True);
            Assert.That(grid.RowDefinitions[3].Height.Value, Is.EqualTo(350));
            controller.ToggleExpanded();
            host.UpdateLayout();
            Assert.That(grid.RowDefinitions[2].ActualHeight, Is.Zero);
            Assert.That(grid.RowDefinitions[3].ActualHeight, Is.GreaterThan(550));
            Capture(host, $"panel-expanded-{(light ? "light" : "dark")}");
            controller.ToggleExpanded();
            host.UpdateLayout();
            Assert.That(grid.RowDefinitions[2].ActualHeight, Is.GreaterThanOrEqualTo(240));
            Assert.That(grid.RowDefinitions[3].Height.Value, Is.EqualTo(350));
            controller.ToggleExpanded();
            controller.Hide();
            controller.Show();
            host.UpdateLayout();
            Assert.That(controller.IsExpanded, Is.False);
            Assert.That(runner.Sessions.Single().Killed, Is.False);
            Capture(host, $"panel-restored-{(light ? "light" : "dark")}");
        }
        finally { host.Close(); model.Dispose(); }
    }

    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public void LongPackageNamesAndBusyStateFitTheMinimumDialog(bool light)
    {
        var shell = Toren.App.App.CreateMainWindow(Profile(), new UiInteractiveProcessRunner());
        Application.Current!.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
        ToolDialog? dialog = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            if (window is ToolDialog tool && tool.Title == "Packages") dialog = tool;
        });
        shell.FindControl<Button>("PackagesActivityButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            var panel = (Control)dialog!.FindControl<ContentControl>("DialogContent")!.Content!;
            var model = (PackageManagerViewModel)panel.DataContext!;
            model.SearchResults.Add(new NuGetPackageSearchResult("Example.Extremely.Long.Package.Name.For.Responsive.Layout", "10.0.0-preview.123456789", 12345, "Example", "https://packages.example.test/v3/index.json"));
            model.InstalledPackages.Add(new NuGetInstalledPackage("/work/Web.csproj", "net10.0", "Example.Extremely.Long.Installed.Package.Name", "10.0.0-*", "10.0.0-preview.123456789"));
            model.StatusText = "A long package status must leave both the project selector and refresh action accessible at the minimum dialog size.";
            dialog!.Width = 640; dialog.Height = 480;
            dialog.UpdateLayout();
            foreach (var list in panel.GetLogicalDescendants().OfType<ListBox>())
            {
                Assert.That(list.IsVisible, Is.True);
                foreach (var label in list.GetVisualDescendants().OfType<TextBlock>().Where(label => label.Bounds.Width > 0))
                {
                    var origin = label.TranslatePoint(default, list)!.Value;
                    Assert.That(origin.X + label.Bounds.Width, Is.LessThanOrEqualTo(list.Bounds.Width));
                    Assert.That(label.Bounds.Width, Is.GreaterThan(100));
                }
            }
            Capture(dialog, $"packages-populated-{(light ? "light" : "dark")}");
            model.IsBusy = true;
            dialog.UpdateLayout();
            Assert.That(panel.GetLogicalDescendants().OfType<ComboBox>().Single().IsEnabled, Is.False);
            Assert.That(panel.FindControl<TextBox>("PackageSearchBox")!.IsEnabled, Is.False);
            Assert.That(panel.GetLogicalDescendants().OfType<ProgressBar>().Single().IsVisible, Is.True);
            Capture(dialog, $"packages-busy-{(light ? "light" : "dark")}");
        }
        finally { dialog?.Close(); ((MainWindowViewModel)shell.DataContext!).Dispose(); }
    }

    [AvaloniaTest]
    public async Task RemappedSaveShortcutReplacesTheDefaultShortcut()
    {
        var profile = Profile();
        Directory.CreateDirectory(profile);
        var file = Path.Combine(profile, "Shortcut.cs");
        await File.WriteAllTextAsync(file, "class Original {}\n");
        var shell = Toren.App.App.CreateMainWindow(profile, new UiInteractiveProcessRunner());
        var model = (MainWindowViewModel)shell.DataContext!;
        await model.Documents.OpenAsync(file);
        ToolDialog? dialog = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            if (window is ToolDialog tool && tool.Title == "Settings") dialog = tool;
        });
        shell.FindControl<Button>("SettingsActivityButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            var settings = (ApplicationSettingsViewModel)((Control)dialog!.FindControl<ContentControl>("DialogContent")!.Content!).DataContext!;
            settings.SaveKeybindingIndex = 2;
            model.Documents.ActiveDocument!.Text = "class Modified {}\n";
            shell.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Control });
            Assert.That(model.Documents.ActiveDocument.IsDirty, Is.True);
            Assert.That(await File.ReadAllTextAsync(file), Is.EqualTo("class Original {}\n"));
            shell.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Alt });
            for (var attempt = 0; attempt < 30 && model.Documents.ActiveDocument.IsDirty; attempt++) await Task.Delay(10);
            Assert.That(model.Documents.ActiveDocument.IsDirty, Is.False);
            Assert.That(await File.ReadAllTextAsync(file), Is.EqualTo("class Modified {}\n"));
        }
        finally { dialog?.Close(); model.Dispose(); }
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("TOREN_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        frame!.Save(Path.Combine(directory, $"dialog-{name}.png"), PngBitmapEncoderOptions.Default);
    }

    [AvaloniaTest]
    public async Task PackageTargetsAreClearedWhileTheNewWorkspaceIsLoading()
    {
        var profile = Profile();
        Directory.CreateDirectory(profile);
        var shell = Toren.App.App.CreateMainWindow(profile, new UiInteractiveProcessRunner());
        var model = (MainWindowViewModel)shell.DataContext!;
        model.WorkspacePath = profile;
        model.Explorer.IsWorkspaceOpen = true;
        var packages = new PackageManagerViewModel(new DotNetPackageService(new SystemProcessRunner()));
        packages.SetWorkspace(profile, [new WorkspaceProject(Path.Combine(profile, "Old.csproj"), "Old workspace project",
            new ProjectMetadata(["net10.0"], "Library", "Old", "Old", false, false, null, null, null), [])]);
        packages.SearchResults.Add(new NuGetPackageSearchResult("Example", "1.0.0", null, null, "nuget.org"));
        packages.SelectedSearchResultIndex = 0;
        Assert.That(packages.CanInstallSelected, Is.True);
        var button = new Button { Name = "PackagesActivityButton" };
        var window = new Window { Content = button };
        var scope = new NameScope();
        scope.Register(button.Name, button);
        NameScope.SetNameScope(window, scope);
        window.Show();
        var catalog = new DelayedProjectCatalog();
        try
        {
            PackageManagerController.Attach(window, model, new WorkspaceClassifier(), catalog, packages, _ => { });
            Assert.That(catalog.Request!.Path, Is.EqualTo(profile));
            Assert.That(packages.Projects, Is.Empty);
            Assert.That(packages.SelectedProject, Is.Null);
            Assert.That(packages.CanInstallSelected, Is.False);
            Assert.That(packages.StatusText, Is.EqualTo("Loading workspace projects…"));
            catalog.Complete();
            for (var attempt = 0; attempt < 30 && packages.StatusText.StartsWith("Loading", StringComparison.Ordinal); attempt++) await Task.Delay(10);
            Assert.That(packages.StatusText, Is.EqualTo("No .NET projects were found in this workspace."));
        }
        finally { window.Close(); model.Dispose(); }
    }

    [AvaloniaTest]
    public async Task InstalledPackagesAreQueriedWhenThePackagesDialogIsFirstOpened()
    {
        var profile = Profile();
        Directory.CreateDirectory(profile);
        var shell = Toren.App.App.CreateMainWindow(profile, new UiInteractiveProcessRunner());
        var model = (MainWindowViewModel)shell.DataContext!;
        model.WorkspacePath = profile;
        model.Explorer.IsWorkspaceOpen = true;
        var service = new CountingPackageService();
        var packages = new PackageManagerViewModel(service);
        var button = new Button { Name = "PackagesActivityButton" };
        var window = new Window { Content = button };
        var scope = new NameScope();
        scope.Register(button.Name, button);
        NameScope.SetNameScope(window, scope);
        window.Show();
        ToolDialog? dialog = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((candidate, _) =>
        {
            if (candidate is ToolDialog tool && tool.Title == "Packages") dialog = tool;
        });
        var catalog = new DelayedProjectCatalog();
        try
        {
            PackageManagerController.Attach(window, model, new WorkspaceClassifier(), catalog, packages, _ => { });
            catalog.Complete(new WorkspaceProject(Path.Combine(profile, "App.csproj"), "App",
                new ProjectMetadata(["net10.0"], "Exe", "App", "App", false, false, null, null, null), []));
            for (var attempt = 0; attempt < 30 && packages.Projects.Count == 0; attempt++) await Task.Delay(10);
            Assert.That(packages.Projects, Has.Count.EqualTo(1));
            Assert.That(service.InstalledRequests, Is.Zero);

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (var attempt = 0; attempt < 30 && service.InstalledRequests == 0; attempt++) await Task.Delay(10);
            Assert.That(service.InstalledRequests, Is.EqualTo(1));

            // Reopening the dialog does not repeat the query on its own.
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(30);
            Assert.That(service.InstalledRequests, Is.EqualTo(1));
        }
        finally { dialog?.Close(); window.Close(); model.Dispose(); }
    }

    private sealed class CountingPackageService : IDotNetPackageService
    {
        public int InstalledRequests { get; private set; }

        public Task<Result<IReadOnlyList<NuGetPackageSearchResult>>> SearchAsync(string workingDirectory, string query, IReadOnlyList<string>? sources = null, int skip = 0, int take = 20, bool includePrerelease = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<NuGetPackageSearchResult>>([]));

        public Task<Result<IReadOnlyList<NuGetInstalledPackage>>> GetInstalledAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            InstalledRequests++;
            return Task.FromResult(Result.Success<IReadOnlyList<NuGetInstalledPackage>>([]));
        }

        public Task<Result<bool>> AddAsync(string projectPath, string packageId, string? version = null, string? source = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> UpdateAsync(string projectPath, string packageId, string? version = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> RemoveAsync(string projectPath, string packageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));
    }

    private sealed class DelayedProjectCatalog : IWorkspaceProjectCatalog
    {
        private readonly TaskCompletionSource<Result<IReadOnlyList<WorkspaceProject>>> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public WorkspaceDescriptor? Request { get; private set; }
        public Task<Result<IReadOnlyList<WorkspaceProject>>> GetProjectsAsync(WorkspaceDescriptor workspace, CancellationToken cancellationToken = default)
        {
            Request = workspace;
            return _completion.Task.WaitAsync(cancellationToken);
        }
        public void Complete(params WorkspaceProject[] projects) => _completion.SetResult(Result.Success<IReadOnlyList<WorkspaceProject>>(projects));
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
