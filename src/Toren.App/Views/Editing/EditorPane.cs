using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;
using Toren.App.ViewModels;
using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;
namespace Toren.App.Views.Editing;

internal sealed class EditorPane : IDisposable
{
    private enum FoldingKind
    {
        None,
        Brace,
        Xml,
    }

    private static readonly TimeSpan FoldingDelay = TimeSpan.FromMilliseconds(500);

    private readonly RegistryOptions _registryOptions;
    private readonly TextDocument _emptyDocument = new();
    private readonly DispatcherTimer _foldingTimer;
    private readonly XmlFoldingStrategy _xmlFoldingStrategy = new();
    private FoldingManager? _foldingManager;
    private FoldingKind _foldingKind;
    private OpenDocumentViewModel? _document;
    private IBrush? _foldingMarkerBrush;
    private IBrush? _foldingMarkerBackgroundBrush;
    private IBrush? _foldingSelectedMarkerBrush;
    private bool _switching;

    public EditorPane(TextEditor editor, RegistryOptions registryOptions)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _registryOptions = registryOptions ?? throw new ArgumentNullException(nameof(registryOptions));

        Installation = editor.InstallTextMate(registryOptions);
        _foldingTimer = new DispatcherTimer { Interval = FoldingDelay };
        _foldingTimer.Tick += FoldingTimer_OnTick;
        editor.TextChanged += Editor_OnTextChanged;
    }

    public TextEditor Editor { get; }

    public TextMateInstallation Installation { get; }

    /// <summary>Raised when the user (not a document switch) changed the text.</summary>
    public event EventHandler? TextEdited;

    /// <summary>Makes the editor show <paramref name="document"/>; null clears it.</summary>
    public void Show(OpenDocumentViewModel? document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        if (_document is not null)
        {
            _document.CaretOffset = Editor.CaretOffset;
        }

        _switching = true;
        try
        {
            _foldingTimer.Stop();
            UninstallFoldings();

            _document = document;
            Editor.Document = document?.Document ?? _emptyDocument;
            ApplyGrammar(document?.Path);
            InstallFoldings(document?.Path);

            if (document is not null)
            {
                Editor.CaretOffset = Math.Min(document.CaretOffset, document.Document.TextLength);
                Dispatcher.UIThread.Post(
                    () => Editor.ScrollTo(Editor.TextArea.Caret.Line, Editor.TextArea.Caret.Column),
                    DispatcherPriority.Background);
            }
        }
        finally
        {
            _switching = false;
        }
    }

    public void SetTheme(IRawTheme theme) => Installation.SetTheme(theme);

    /// <summary>The default fold markers are drawn for a light theme, so they get the Toren colours.</summary>
    public void SetFoldingBrushes(IBrush? marker, IBrush? markerBackground, IBrush? selectedMarker)
    {
        _foldingMarkerBrush = marker;
        _foldingMarkerBackgroundBrush = markerBackground;
        _foldingSelectedMarkerBrush = selectedMarker;
        ApplyFoldingBrushes();
    }

    public void Dispose()
    {
        _foldingTimer.Stop();
        _foldingTimer.Tick -= FoldingTimer_OnTick;
        Editor.TextChanged -= Editor_OnTextChanged;
        UninstallFoldings();
        Installation.Dispose();
    }

    private void ApplyGrammar(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Installation.SetGrammar(null);
            return;
        }

        var language = _registryOptions.GetLanguageByExtension(Path.GetExtension(path));
        var scopeName = language is null
            ? null
            : _registryOptions.GetScopeByLanguageId(language.Id);
        Installation.SetGrammar(scopeName);
    }

    private void InstallFoldings(string? path)
    {
        _foldingKind = Path.GetExtension(path ?? string.Empty).ToLowerInvariant() switch
        {
            ".cs" or ".json" => FoldingKind.Brace,
            ".xml" or ".axaml" or ".xaml" or ".csproj" or ".props" or ".targets" => FoldingKind.Xml,
            _ => FoldingKind.None,
        };

        if (_foldingKind == FoldingKind.None || _document is null)
        {
            return;
        }

        _foldingManager = FoldingManager.Install(Editor.TextArea);
        ApplyFoldingBrushes();
        UpdateFoldings();
    }

    private void UninstallFoldings()
    {
        if (_foldingManager is null)
        {
            return;
        }

        FoldingManager.Uninstall(_foldingManager);
        _foldingManager = null;
    }

    private void UpdateFoldings()
    {
        if (_foldingManager is null)
        {
            return;
        }

        if (_foldingKind == FoldingKind.Brace)
        {
            _foldingManager.UpdateFoldings(BraceFoldingStrategy.CreateFoldings(Editor.Document), -1);
        }
        else if (_foldingKind == FoldingKind.Xml)
        {
            _xmlFoldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        }
    }

    private void ApplyFoldingBrushes()
    {
        if (_foldingManager is null)
        {
            return;
        }

        foreach (var margin in Editor.TextArea.LeftMargins.OfType<FoldingMargin>())
        {
            if (_foldingMarkerBrush is not null)
            {
                margin.FoldingMarkerBrush = _foldingMarkerBrush;
            }

            if (_foldingMarkerBackgroundBrush is not null)
            {
                margin.FoldingMarkerBackgroundBrush = _foldingMarkerBackgroundBrush;
            }

            if (_foldingSelectedMarkerBrush is not null)
            {
                margin.SelectedFoldingMarkerBrush = _foldingSelectedMarkerBrush;
                margin.SelectedFoldingMarkerBackgroundBrush = _foldingMarkerBackgroundBrush ?? _foldingSelectedMarkerBrush;
            }
        }
    }

    private void Editor_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        // Replacing the editor's document also raises TextChanged; only real edits matter.
        if (_switching)
        {
            return;
        }

        if (_foldingManager is not null)
        {
            _foldingTimer.Stop();
            _foldingTimer.Start();
        }

        TextEdited?.Invoke(this, eventArgs);
    }

    private void FoldingTimer_OnTick(object? sender, EventArgs eventArgs)
    {
        _foldingTimer.Stop();
        UpdateFoldings();
    }
}
