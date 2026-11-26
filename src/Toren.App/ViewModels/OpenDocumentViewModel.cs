using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Documents.Models;
using Toren.App.ViewModels.Utilities;

namespace Toren.App.ViewModels;

public sealed partial class OpenDocumentViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isActive;

    /// <summary>A pinned tab stays on the left and cannot be closed until it is unpinned.</summary>
    [ObservableProperty]
    private bool _isPinned;

    /// <summary>A preview tab is replaced by the next previewed file until it is edited or double-clicked.</summary>
    [ObservableProperty]
    private bool _isPreview;

    [ObservableProperty]
    private IReadOnlyList<BreadcrumbSegment> _breadcrumbs = [];

    public OpenDocumentViewModel(TextDocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Path = content.Path;
        Title = System.IO.Path.GetFileName(content.Path);
        Encoding = content.Encoding;

        // One TextDocument is shared by every editor that shows this file, so split editors
        // stay in sync and keep a single undo history.
        Document = new TextDocument(content.Text);
        Document.TextChanged += Document_OnTextChanged;
        UpdateBreadcrumbs(null);
    }

    public string Path { get; }

    public string Title { get; }

    public TextDocumentEncoding Encoding { get; }

    public TextDocument Document { get; }

    /// <summary>Caret position remembered while another document is shown in an editor.</summary>
    public int CaretOffset { get; set; }

    /// <summary>
    /// TextDocument.Text may only be read on the thread that created the document, but
    /// diagnostics and saving read the text from background threads. A snapshot can be taken
    /// from any thread; setting the text must still happen on the UI thread.
    /// </summary>
    public string Text
    {
        get => Document.CreateSnapshot().Text;
        set
        {
            var text = value ?? string.Empty;
            if (!Document.CreateSnapshot().Text.Equals(text, StringComparison.Ordinal))
            {
                Document.Text = text;
            }
        }
    }

    public TextDocumentContent CreateContent() => new(Path, Text, Encoding);

    public void MarkSaved() => IsDirty = false;

    public void UpdateBreadcrumbs(string? workspaceRoot) =>
        Breadcrumbs = BreadcrumbBuilder.Build(Path, workspaceRoot);

    partial void OnIsDirtyChanged(bool value)
    {
        // Editing a previewed file keeps it open.
        if (value)
        {
            IsPreview = false;
        }
    }

    private void Document_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        IsDirty = true;
        OnPropertyChanged(nameof(Text));
    }
}
