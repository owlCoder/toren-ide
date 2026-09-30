using Avalonia.Controls;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Views;

internal sealed partial class CSharpQuickInfoView : UserControl
{
    public CSharpQuickInfoView()
    {
        InitializeComponent();
    }

    public void SetSymbol(CSharpSymbolInfo symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        DisplayText.Text = symbol.DisplayText;
        KindText.Text = symbol.Kind.ToString();
        LocationText.Text = symbol.Definition is { FilePath: { Length: > 0 } path } definition
            ? $"{Path.GetFileName(path)} · Ln {definition.Line}"
            : symbol.Definition is { } sourceDefinition
                ? $"Ln {sourceDefinition.Line}"
                : "Metadata / external symbol";
    }
}
