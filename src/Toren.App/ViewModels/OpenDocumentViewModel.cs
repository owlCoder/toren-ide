using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Documents.Models;

namespace Toren.App.ViewModels;

public sealed partial class OpenDocumentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isActive;

    public OpenDocumentViewModel(TextDocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Path = content.Path;
        Title = System.IO.Path.GetFileName(content.Path);
        Encoding = content.Encoding;
        _text = content.Text;
    }

    public string Path { get; }

    public string Title { get; }

    public TextDocumentEncoding Encoding { get; }

    public TextDocumentContent CreateContent() => new(Path, Text, Encoding);

    public void MarkSaved() => IsDirty = false;

    partial void OnTextChanged(string value) => IsDirty = true;
}
