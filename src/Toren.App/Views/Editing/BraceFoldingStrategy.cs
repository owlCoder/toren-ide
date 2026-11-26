using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace Toren.App.Views.Editing;

/// <summary>
/// Folds every multi-line { ... } block. Braces inside comments, strings and character
/// literals are ignored, which is enough for C# and JSON.
/// </summary>
internal static class BraceFoldingStrategy
{
    public static IEnumerable<NewFolding> CreateFoldings(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var text = document.Text;
        var foldings = new List<NewFolding>();
        var openBraces = new Stack<int>();

        var index = 0;
        while (index < text.Length)
        {
            var current = text[index];
            var next = index + 1 < text.Length ? text[index + 1] : '\0';

            if (current == '/' && next == '/')
            {
                index = SkipLine(text, index);
            }
            else if (current == '/' && next == '*')
            {
                var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 2;
            }
            else if (current == '"')
            {
                index = SkipString(text, index);
            }
            else if (current == '\'')
            {
                index = SkipCharacter(text, index);
            }
            else
            {
                if (current == '{')
                {
                    openBraces.Push(index);
                }
                else if (current == '}' && openBraces.Count > 0)
                {
                    var start = openBraces.Pop();
                    if (document.GetLineByOffset(start).LineNumber != document.GetLineByOffset(index).LineNumber)
                    {
                        foldings.Add(new NewFolding(start, index + 1));
                    }
                }

                index++;
            }
        }

        foldings.Sort((left, right) => left.StartOffset.CompareTo(right.StartOffset));
        return foldings;
    }

    private static int SkipLine(string text, int index)
    {
        var end = text.IndexOf('\n', index);
        return end < 0 ? text.Length : end + 1;
    }

    private static int SkipString(string text, int start)
    {
        if (start + 2 < text.Length && text[start + 1] == '"' && text[start + 2] == '"')
        {
            var rawEnd = text.IndexOf("\"\"\"", start + 3, StringComparison.Ordinal);
            return rawEnd < 0 ? text.Length : rawEnd + 3;
        }

        var verbatim = start > 0 && text[start - 1] == '@';
        var index = start + 1;
        while (index < text.Length)
        {
            var current = text[index];
            if (verbatim)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        index += 2;
                        continue;
                    }

                    return index + 1;
                }
            }
            else
            {
                if (current == '\\')
                {
                    index += 2;
                    continue;
                }

                if (current == '"')
                {
                    return index + 1;
                }

                if (current == '\n')
                {
                    return index + 1;
                }
            }

            index++;
        }

        return text.Length;
    }

    private static int SkipCharacter(string text, int start)
    {
        var index = start + 1;
        while (index < text.Length)
        {
            var current = text[index];
            if (current == '\\')
            {
                index += 2;
                continue;
            }

            if (current == '\'')
            {
                return index + 1;
            }

            if (current == '\n')
            {
                return index + 1;
            }

            index++;
        }

        return text.Length;
    }
}
