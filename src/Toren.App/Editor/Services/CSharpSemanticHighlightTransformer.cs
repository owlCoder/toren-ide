using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpSemanticHighlightTransformer : DocumentColorizingTransformer
{
    private static readonly IBrush DarkTypeBrush = new SolidColorBrush(Color.FromRgb(78, 201, 176));
    private static readonly IBrush DarkMethodBrush = new SolidColorBrush(Color.FromRgb(220, 220, 170));
    private static readonly IBrush DarkValueBrush = new SolidColorBrush(Color.FromRgb(156, 220, 254));
    private static readonly IBrush LightTypeBrush = new SolidColorBrush(Color.FromRgb(38, 127, 153));
    private static readonly IBrush LightMethodBrush = new SolidColorBrush(Color.FromRgb(121, 94, 38));
    private static readonly IBrush LightValueBrush = new SolidColorBrush(Color.FromRgb(0, 16, 128));

    private IReadOnlyList<CSharpSemanticHighlight> _highlights = [];

    public void SetHighlights(IReadOnlyList<CSharpSemanticHighlight> highlights)
    {
        ArgumentNullException.ThrowIfNull(highlights);
        _highlights = highlights;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;
        foreach (var highlight in _highlights)
        {
            var highlightEnd = highlight.StartOffset + highlight.Length;
            if (highlightEnd <= lineStart)
            {
                continue;
            }

            if (highlight.StartOffset >= lineEnd)
            {
                break;
            }

            var brush = GetBrush(highlight.Kind);
            if (brush is null)
            {
                continue;
            }

            var start = Math.Max(lineStart, highlight.StartOffset);
            var end = Math.Min(lineEnd, highlightEnd);
            if (start >= end)
            {
                continue;
            }

            ChangeLinePart(
                start,
                end,
                element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private static IBrush? GetBrush(CSharpSymbolKind kind)
    {
        var isLight = Application.Current?.RequestedThemeVariant == ThemeVariant.Light;
        return kind switch
        {
            CSharpSymbolKind.Type => isLight ? LightTypeBrush : DarkTypeBrush,
            CSharpSymbolKind.Method => isLight ? LightMethodBrush : DarkMethodBrush,
            CSharpSymbolKind.Property or CSharpSymbolKind.Field or CSharpSymbolKind.Event
                or CSharpSymbolKind.Parameter or CSharpSymbolKind.Local =>
                isLight ? LightValueBrush : DarkValueBrush,
            _ => null,
        };
    }
}
