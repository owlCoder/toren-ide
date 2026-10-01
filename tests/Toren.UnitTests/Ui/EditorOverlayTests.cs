using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using AvaloniaEdit;
using Toren.App.Editor.Services;
using Toren.App.Search.Services;
using Toren.App.Search.Contracts;
using Toren.Core.Results;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using NUnit.Framework;
using Toren.App.Editor.Views;
using Toren.App.Search.Models;
using Toren.App.Search.Views;
using Toren.Language.CSharp.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Ui;

[TestFixture]
public sealed class EditorOverlayTests
{
    [AvaloniaTest]
    public void GoToFileSupportsKeyboardSelectionAndActivation()
    {
        var overlay = new WorkspaceQuickOpenOverlay();
        var host = Show(overlay);
        try
        {
            WorkspaceFileEntry[] files = [new("/work/A.cs", "A.cs", "A.cs"), new("/work/B.cs", "B.cs", "B.cs")];
            overlay.SetResults(files);
            overlay.SetStatus("2 files");
            overlay.ShowOverlay();
            host.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.That(overlay.FindControl<TextBox>("QueryBox")!.IsFocused, Is.True, "Opening Go to File should focus its query without a click.");
            var activated = false;
            overlay.FileRequested += (_, _) => activated = true;
            host.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            host.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Assert.That(overlay.SelectedFile, Is.EqualTo(files[1]));
            host.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Assert.That(activated, Is.True);
            overlay.HideOverlay();
            Assert.That(overlay.IsVisible, Is.False);
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaTest]
    public void SymbolPickerCanDisplayAndSelectResults()
    {
        var overlay = new CSharpSymbolQuickOpenOverlay();
        var host = Show(overlay);
        try
        {
            var symbol = new CSharpWorkspaceSymbol("Run", "void Run()", CSharpSymbolKind.Method,
                "Sample", new CSharpSourceLocation("/work/Sample.cs", 12, 1));
            overlay.SetResults([symbol]);
            overlay.SetStatus("1 symbol");
            overlay.ShowOverlay();
            AssertFocused(host, overlay, "QueryBox");
            Assert.That(overlay.SelectedSymbol, Is.EqualTo(symbol));
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaTest]
    public void WorkspaceSearchDisplaysGroupedResultsAndOptions()
    {
        var overlay = new WorkspaceTextSearchOverlay();
        var host = Show(overlay);
        try
        {
            var result = new WorkspaceTextSearchResult("/work/Sample.cs", "Sample.cs", 4, 1, "class Sample");
            overlay.SetResults([result]);
            overlay.SetStatus("1 match");
            overlay.ShowOverlay();
            AssertFocused(host, overlay, "QueryBox");
            Assert.That(overlay.SelectedResult, Is.EqualTo(result));
            Assert.That(overlay.MatchCase, Is.False);
            Assert.That(overlay.IncludePatterns, Is.Empty);
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaTest]
    public void EditorNavigationCanSwitchBetweenFindReplaceAndGoToLine()
    {
        var overlay = new EditorNavigationOverlay();
        var host = Show(overlay);
        try
        {
            overlay.ShowFind("Sample");
            AssertFocused(host, overlay, "InputBox");
            overlay.SetMatchStatus(1, 4);
            Assert.That(overlay.Query, Is.EqualTo("Sample"));
            overlay.ShowReplace("Service");
            Assert.That(overlay.Mode, Is.EqualTo(EditorNavigationMode.Replace));
            Assert.That(overlay.Replacement, Is.Empty);
            overlay.ShowGoToLine(120);
            AssertFocused(host, overlay, "InputBox");
            Assert.That(overlay.Query, Is.EqualTo("120"));
            Assert.That(overlay.Mode, Is.EqualTo(EditorNavigationMode.GoToLine));
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaTest]
    public void RenameAndQuickFixCanDisplayTheirInputs()
    {
        var rename = new CSharpRenameOverlay();
        var host = Show(rename);
        try
        {
            rename.ShowOverlay("Sample");
            AssertFocused(host, rename, "NameBox");
            Assert.That(rename.NewName, Is.EqualTo("Sample"));
            rename.SetError("Choose another name.");
            rename.HideOverlay();
            var quickFix = new CSharpCodeActionOverlay();
            host.Content = quickFix;
            var action = new CSharpCodeActionInfo("add-semicolon", "Add semicolon", "CS1002",
                new CSharpTextEdit("/work/Sample.cs", 20, 0, ";"));
            quickFix.ShowOverlay([action]);
            AssertFocused(host, quickFix, "ResultsList");
            Assert.That(quickFix.SelectedAction, Is.EqualTo(action));
            var actionRequested = false;
            quickFix.ActionRequested += (_, _) => actionRequested = true;
            host.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Assert.That(actionRequested, Is.True, "A focused quick-fix row should activate with Enter.");
            quickFix.HideOverlay();
            Assert.That(quickFix.SelectedAction, Is.Null);
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaTest]
    [TestCase(RawInputModifiers.Control)]
    [TestCase(RawInputModifiers.Meta)]
    public void WorkspaceSearchShortcutDoesNotOpenDocumentFind(RawInputModifiers command)
    {
        var editor = new TextEditor { Name = "DocumentEditor", Text = "class Sample { }" };
        var editorHost = new Grid { Children = { editor } };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        Grid.SetRow(editorHost, 2);
        Grid.SetColumn(editorHost, 2);
        root.Children.Add(editorHost);
        var host = new Window { Content = root, Width = 1080, Height = 700 };
        var names = new NameScope();
        names.Register("DocumentEditor", editor);
        NameScope.SetNameScope(host, names);
        EditorSearchController.Attach(host);
        WorkspaceTextSearchController.Attach(host, new EmptySearchService(), () => "/work", () => new Dictionary<string, string>(),
            _ => Task.CompletedTask, _ => { });
        host.Show();
        try
        {
            var documentFind = root.GetLogicalDescendants().OfType<EditorNavigationOverlay>().Single();
            var workspaceFind = root.GetLogicalDescendants().OfType<WorkspaceTextSearchOverlay>().Single();
            editor.Focus();
            host.KeyPress(Key.F, command | RawInputModifiers.Shift, PhysicalKey.F, null);
            host.KeyRelease(Key.F, command | RawInputModifiers.Shift, PhysicalKey.F, null);
            AssertFocused(host, workspaceFind, "QueryBox");
            Assert.That(workspaceFind.IsVisible, Is.True);
            Assert.That(documentFind.IsVisible, Is.False);
            host.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.That(workspaceFind.IsVisible, Is.False);
            editor.Focus();
            host.KeyPress(Key.F, command, PhysicalKey.F, null);
            AssertFocused(host, documentFind, "InputBox");
            Assert.That(documentFind.IsVisible, Is.True);
            Assert.That(workspaceFind.IsVisible, Is.False);
        }
        finally { host.Close(); }
    }

    private sealed class EmptySearchService : IWorkspaceTextSearchService
    {
        public Task<Result<IReadOnlyList<WorkspaceTextSearchResult>>> SearchAsync(string workspacePath, string query,
            WorkspaceTextSearchOptions options, IReadOnlyDictionary<string, string>? textOverrides = null,
            int maxResults = 200, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success<IReadOnlyList<WorkspaceTextSearchResult>>([]));
    }

    [AvaloniaTest]
    public void HoverQuickInfoDisplaysTheSymbolAndSourceLocation()
    {
        var hover = new CSharpQuickInfoView();
        hover.SetSymbol(new CSharpSymbolInfo("Sample", "class Sample", CSharpSymbolKind.Type,
            new CSharpSourceLocation("/work/Sample.cs", 12, 1)));
        Assert.That(hover.FindControl<TextBlock>("DisplayText")!.Text, Is.EqualTo("class Sample"));
        Assert.That(hover.FindControl<TextBlock>("LocationText")!.Text, Is.EqualTo("Sample.cs · Ln 12"));
    }

    private static void AssertFocused(Window host, Control overlay, string targetName)
    {
        host.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.That(overlay.FindControl<Control>(targetName)!.IsKeyboardFocusWithin, Is.True,
            "Opening an overlay should move keyboard focus to its primary input.");
    }

    private static Window Show(Control overlay)
    {
        var host = new Window { Content = overlay, Width = 640, Height = 400 };
        host.Show();
        return host;
    }
}
