using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Models;

internal sealed class CSharpCompletionData(CSharpCompletionItem item) : ICompletionData
{
    private readonly CSharpCompletionItem _item = item ?? throw new ArgumentNullException(nameof(item));

    public IImage Image => null!;

    public string Text => _item.DisplayText;

    public object Content => _item.DisplayText;

    public object Description => _item.Detail ?? _item.Kind.ToString();

    public double Priority => _item.Kind switch
    {
        CSharpSymbolKind.Local => 1.0,
        CSharpSymbolKind.Parameter => 0.95,
        CSharpSymbolKind.Property => 0.9,
        CSharpSymbolKind.Field => 0.85,
        CSharpSymbolKind.Method => 0.8,
        CSharpSymbolKind.Type => 0.7,
        _ => 0.5,
    };

    public void Complete(
        TextArea textArea,
        ISegment completionSegment,
        EventArgs insertionRequestEventArgs)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        ArgumentNullException.ThrowIfNull(completionSegment);
        textArea.Document.Replace(completionSegment.Offset, completionSegment.Length, _item.InsertText);
    }
}
