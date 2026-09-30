using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
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
            Assert.That(overlay.FindControl<TextBox>("QueryBox")!.Focus(), Is.True);
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
            overlay.SetMatchStatus(1, 4);
            Assert.That(overlay.Query, Is.EqualTo("Sample"));
            overlay.ShowReplace("Service");
            Assert.That(overlay.Mode, Is.EqualTo(EditorNavigationMode.Replace));
            Assert.That(overlay.Replacement, Is.Empty);
            overlay.ShowGoToLine(120);
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
            Assert.That(rename.NewName, Is.EqualTo("Sample"));
            rename.SetError("Choose another name.");
            rename.HideOverlay();
            var quickFix = new CSharpCodeActionOverlay();
            host.Content = quickFix;
            var action = new CSharpCodeActionInfo("add-semicolon", "Add semicolon", "CS1002",
                new CSharpTextEdit("/work/Sample.cs", 20, 0, ";"));
            quickFix.ShowOverlay([action]);
            Assert.That(quickFix.SelectedAction, Is.EqualTo(action));
            quickFix.HideOverlay();
            Assert.That(quickFix.SelectedAction, Is.Null);
        }
        finally
        {
            host.Close();
        }
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

    private static Window Show(Control overlay)
    {
        var host = new Window { Content = overlay, Width = 640, Height = 400 };
        host.Show();
        return host;
    }
}
