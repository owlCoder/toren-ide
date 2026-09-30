using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using Toren.App.Editor.Views;
using Toren.App.Search.Models;
using Toren.App.Search.Views;
using Toren.App.Views.Testing;
using Toren.App.Shell;
using Toren.App.Views;
using Toren.App.Debugging.ViewModels;
using Toren.App.SourceControl.ViewModels;
using Toren.App.Http.ViewModels;
using Toren.Debugging.Models;
using Toren.Git.Models;
using Toren.DotNet.Http.Models;
using Toren.App.ViewModels;
using Toren.Language.CSharp.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Ui;

[TestFixture]
public sealed class ShellLayoutTests
{
    [AvaloniaTest]
    [TestCase(1080, 700, false, 240)]
    [TestCase(1080, 700, true, 240)]
    [TestCase(1080, 700, false, 270)]
    [TestCase(1080, 700, true, 270)]
    [TestCase(1080, 700, false, 420)]
    [TestCase(1080, 700, true, 420)]
    [TestCase(1440, 900, false, 270)]
    [TestCase(1440, 900, true, 270)]
    public void ToolPanelsRemainUsableAtSupportedSizes(int width, int height, bool light, int sidebarWidth)
    {
        var application = (Toren.App.App)Application.Current!;
        var profile = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", $"ui-{Guid.NewGuid():N}");
        var shell = Toren.App.App.CreateMainWindow(profile, new UiInteractiveProcessRunner());
        application.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
        var model = (MainWindowViewModel)shell.DataContext!;
        model.SdkSummary = ".NET SDK: 10.0.100";
        model.StatusText = "Ready — a long workspace status should leave room for the theme switch and SDK";
        model.RecentWorkspaces.Add(new WorkspaceDescriptor(
            "/work/very-long-directory-name/another-long-directory-name/Toren.Sample.Application.slnx",
            "Toren.Sample.Application.With.A.Long.Workspace.Name", WorkspaceKind.Solution));
        model.HasRecentWorkspaces = true;
        shell.FindControl<Grid>("ShellGrid")!.ColumnDefinitions[1].Width = new GridLength(sidebarWidth);
        var content = shell.Content;
        shell.Content = null;
        // Render the composed shell without firing startup/session persistence or running tools.
        var host = new Window { Content = content, DataContext = shell.DataContext, Width = width, Height = height };
        host.Show();
        try
        {
            var tabs = ((Control)content!).GetLogicalDescendants().OfType<TabControl>().Single(control => control.Name == "ToolTabs");
            Assert.That(tabs, Is.Not.Null);
            Assert.That(tabs.Items.Count, Is.EqualTo(5));
            Assert.That(tabs.Items.OfType<TabItem>().Count(item => item.Content is Toren.App.Views.Terminal.TerminalPanel), Is.EqualTo(1));
            Capture(host, width, height, light, $"sidebar{sidebarWidth}-welcome");

            foreach (var tab in tabs.Items.OfType<TabItem>())
            {
                tabs.SelectedItem = tab;
                Dispatcher.UIThread.RunJobs();
                host.UpdateLayout();
                Assert.That(tab.Content, Is.InstanceOf<UserControl>(), $"{tab.Header}: placeholder tab");
                var panel = (Control)tab.Content!;
                Assert.That(panel.Bounds.Height, Is.GreaterThan(170), $"{tab.Header}: no usable content area");
                AssertContained(panel, host);
                AssertContained(tab, tab.GetVisualAncestors().OfType<ScrollViewer>().First());
                if (panel is Toren.App.Views.SourceControl.SourceControlPanel)
                {
                    Assert.That(panel.GetLogicalDescendants().OfType<TextBox>().Single(box => box.IsReadOnly).Bounds.Height,
                        Is.GreaterThan(60), "Source Control: no usable diff viewport");
                }

                foreach (var button in panel.GetVisualDescendants().OfType<Button>())
                {
                    if (!button.IsEffectivelyVisible || button.Bounds.Width <= 0 || IsScrollable(button, panel))
                    {
                        continue;
                    }

                    AssertContained(button, panel);
                }

                Capture(host, width, height, light, $"sidebar{sidebarWidth}-{panel.GetType().Name}");
            }

            // Populated diagnostics exercise the scope/filter toolbar and long message columns.
            model.Problems.Replace("/work/Very.Long.Source.File.Name.cs",
                [new CSharpDiagnostic("CS1002", "A long diagnostic message should be trimmed without hiding its source location.",
                    CSharpDiagnosticSeverity.Error, 120, 15, 120, 16)]);
            tabs.SelectedIndex = 0;
            host.UpdateLayout();
            Capture(host, width, height, light, $"sidebar{sidebarWidth}-diagnostics");

            var root = (Control)content!;
            var sidebar = SidebarController.For(shell);
            foreach (var name in new[] { "Tests", "SourceControl", "Debug", "Http", "Data", "Explorer" })
            {
                root.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == $"{name}ActivityButton")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                host.UpdateLayout();
                var panel = sidebar.ActiveContent!;
                PopulateSidebar(panel);
                host.UpdateLayout();
                AssertContained(panel, host);
                Assert.That(root.GetLogicalDescendants().OfType<Button>().Count(button => button.Classes.Contains("active")), Is.EqualTo(1));
                foreach (var nestedTabs in panel.GetLogicalDescendants().OfType<TabControl>().ToArray())
                {
                    foreach (var nestedTab in nestedTabs.Items.OfType<TabItem>())
                    {
                        nestedTabs.SelectedItem = nestedTab;
                        host.UpdateLayout();
                        Assert.That(nestedTabs.SelectedContent, Is.Not.Null);
                        Assert.That(((Control)nestedTabs.SelectedContent!).Bounds.Height, Is.GreaterThan(100));
                        VerifyPanelControls(panel);
                        Capture(host, width, height, light, $"sidebar{sidebarWidth}-{panel.GetType().Name}-{nestedTab.Header}");
                    }
                }

                VerifyPanelControls(panel);
                Capture(host, width, height, light, $"sidebar{sidebarWidth}-{panel.GetType().Name}");
            }

            // Search popovers must fit the remaining editor region even when the sidebar is wide.
            var quickOpen = root.GetLogicalDescendants().OfType<WorkspaceQuickOpenOverlay>().Single();
            quickOpen.SetResults(Enumerable.Range(1, 30).Select(index => new WorkspaceFileEntry(
                $"/work/File{index}.cs", $"src/Long.Directory.Name/File{index}.cs", $"File{index}.cs")).ToArray());
            VerifyOverlay(quickOpen, host, width, height, light, sidebarWidth);

            var search = ((Control)content!).GetLogicalDescendants().OfType<WorkspaceTextSearchOverlay>().Single();
            search.SetResults(Enumerable.Range(1, 30).Select(index => new WorkspaceTextSearchResult(
                "/work/Service.cs", "src/Long.Directory.Name/Service.cs", index, 12,
                "var example = ThisIsALongSearchResultPreviewThatShouldStayInsideThePopover();")).ToArray());
            VerifyOverlay(search, host, width, height, light, sidebarWidth);

            var symbols = ((Control)content!).GetLogicalDescendants().OfType<CSharpSymbolQuickOpenOverlay>().Single();
            symbols.SetResults(Enumerable.Range(1, 30).Select(index => new CSharpWorkspaceSymbol(
                $"Method{index}", $"public void Method{index}()", CSharpSymbolKind.Method, "Sample.Service",
                new CSharpSourceLocation("/work/Service.cs", index, 1))).ToArray());
            VerifyOverlay(symbols, host, width, height, light, sidebarWidth);
        }
        finally
        {
            host.Close();
            ((Toren.App.ViewModels.MainWindowViewModel)shell.DataContext!).Dispose();
        }
    }

    private static void VerifyOverlay(UserControl overlay, Window host, int width, int height, bool light, int sidebarWidth)
    {
        overlay.IsVisible = true;
        host.UpdateLayout();
        AssertContained(overlay, host);
        var results = overlay.GetLogicalDescendants().OfType<ListBox>().Single();
        AssertContained(results, overlay);
        Assert.That(results.Bounds.Height, Is.GreaterThan(60));
        Capture(host, width, height, light, $"sidebar{sidebarWidth}-{overlay.GetType().Name}");
        overlay.IsVisible = false;
    }

    private static void PopulateSidebar(Control panel)
    {
        if (panel.DataContext is DebugSessionViewModel debug)
        {
            debug.StackFrames.Add(new DebugStackFrame(1, "ParcelBox.Service.LoadWorkspaceAsync", "/work/src/Service.cs", 42, 9));
            debug.Locals.Add(new DebugLocalItemViewModel("Locals", "workspace", "{ DisplayName = \"ParcelBox\", Projects = 8 }", "WorkspaceDescriptor", 0));
            debug.Watches.Add(new DebugWatchItemViewModel("workspace.Projects.Count") { Value = "8", Type = "int" });
            debug.Breakpoints.Add(new DebugBreakpointItemViewModel("/work/src/Very.Long.Source.File.Name.cs", 42, "workspace != null", true, "Breakpoint verified"));
            debug.ConsoleLines.Add(new DebugConsoleLineViewModel("[debug] Workspace loaded successfully."));
        }
        else if (panel.DataContext is SourceControlViewModel git)
        {
            git.StatusText = "1 change on fix/ui-polish";
            git.Branches.Add(new GitBranchInfo("fix/ui-polish", true, null));
            git.SelectedBranchIndex = 0;
            git.Changes.Add(new SourceControlChangeViewModel(new GitChange("src/Very.Long.Directory.Name/WorkspaceStatus.cs", null, '.', 'M')));
            git.DiffText = "@@ -17,1 +17,1 @@\n- DisplayName = \"Toren\";\n+ DisplayName = workspace.Name;";
        }
        else if (panel.DataContext is HttpClientViewModel http)
        {
            http.SetDocument("/work/requests.http", "### Health check with a long descriptive name\nGET https://localhost:7254/api/health\n");
            http.SetEnvironments([new HttpEnvironment("Development", new Dictionary<string, string>())]);
            http.ResponseStatus = "200 OK · 42 ms";
            http.ResponseHeaders = "content-type: application/json\ncache-control: no-cache";
            http.ResponseBody = "{\n  \"status\": \"healthy\"\n}";
        }
    }

    private static void VerifyPanelControls(Control panel)
    {
        foreach (var button in panel.GetVisualDescendants().OfType<Button>()
                     .Where(button => button.IsEffectivelyVisible && button.Bounds.Width > 0 && !IsScrollable(button, panel)))
        {
            AssertContained(button, panel);
        }
    }

    private static bool IsScrollable(Control control, Control panel) => control.GetVisualAncestors()
        .TakeWhile(ancestor => !ReferenceEquals(ancestor, panel))
        .Any(ancestor => ancestor is ScrollViewer);

    private static void AssertContained(Control control, Control container)
    {
        var origin = control.TranslatePoint(default, container);
        Assert.That(origin, Is.Not.Null);
        Assert.That(origin!.Value.X, Is.GreaterThanOrEqualTo(-1), $"{control}: left edge");
        Assert.That(origin.Value.Y, Is.GreaterThanOrEqualTo(-1), $"{control}: top edge");
        Assert.That(origin.Value.X + control.Bounds.Width, Is.LessThanOrEqualTo(container.Bounds.Width + 1), $"{control}: right edge");
        Assert.That(origin.Value.Y + control.Bounds.Height, Is.LessThanOrEqualTo(container.Bounds.Height + 1), $"{control}: bottom edge");
    }

    private static void Capture(Window host, int width, int height, bool light, string name)
    {
        var directory = Environment.GetEnvironmentVariable("TOREN_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var frame = host.CaptureRenderedFrame();
        Assert.That(frame, Is.Not.Null);
        frame!.Save(Path.Combine(directory, $"{width}x{height}-{(light ? "light" : "dark")}-{name}.png"), PngBitmapEncoderOptions.Default);
    }
}
