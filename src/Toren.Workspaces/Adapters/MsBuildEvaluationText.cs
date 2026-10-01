using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

/// <summary>
/// Reads the per-project file written by the collection target in
/// <c>Toren.Evaluation.targets</c>: one tab-separated record per line, closed by an end marker.
/// </summary>
internal static class MsBuildEvaluationText
{
    private const char Separator = '\t';

    /// <summary>
    /// Returns the project the records belong to, or <see langword="null"/> for a file that is
    /// incomplete, which happens when the build is cancelled while it is being written.
    /// </summary>
    public static string? Read(IEnumerable<string> lines, out MsBuildEvaluationData data)
    {
        data = new MsBuildEvaluationData();
        string? projectPath = null;
        var complete = false;

        foreach (var line in lines)
        {
            var fields = line.Split(Separator);
            switch (fields[0])
            {
                case "Project" when fields.Length >= 2:
                    projectPath = fields[1];
                    break;
                case "Property" when fields.Length >= 3:
                    data.SetProperty(fields[1], fields[2]);
                    break;
                case "Items" when fields.Length >= 2:
                    data.DeclareItems(fields[1]);
                    break;
                case "Using" when fields.Length >= 2:
                    data.AddGlobalUsing(
                        fields[1],
                        fields.Length > 2 && bool.TryParse(fields[2], out var isStatic) && isStatic,
                        fields.Length > 3 ? fields[3] : null);
                    break;
                case "End":
                    complete = true;
                    break;
                default:
                    if (fields.Length >= 2)
                    {
                        ReadItem(fields[0], fields[1], data);
                    }

                    break;
            }
        }

        return complete && !string.IsNullOrWhiteSpace(projectPath) ? projectPath : null;
    }

    private static void ReadItem(string itemName, string value, MsBuildEvaluationData data)
    {
        foreach (var (referenceItem, kind) in MsBuildEvaluationData.ReferenceItems)
        {
            if (itemName == referenceItem)
            {
                data.AddReference(kind, value);
                return;
            }
        }

        if (Array.IndexOf(MsBuildEvaluationData.PathItems, itemName) >= 0)
        {
            data.AddItemPath(itemName, value);
        }
    }
}
