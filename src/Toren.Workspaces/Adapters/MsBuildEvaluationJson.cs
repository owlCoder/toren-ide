using System.Text.Json;

namespace Toren.Workspaces.Adapters;

/// <summary>Reads the JSON that <c>dotnet msbuild -getProperty/-getItem</c> writes.</summary>
internal static class MsBuildEvaluationJson
{
    public const string EvaluatedProperties =
        "TargetFramework,TargetFrameworks,OutputType,AssemblyName,RootNamespace,IsTestProject,ManagePackageVersionsCentrally,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath,Nullable,LangVersion,DefineConstants,AllowUnsafeBlocks,GeneratedMSBuildEditorConfigFile,UsingMicrosoftNETSdkRazor,ProjectAssetsFile";

    public const string ReferenceItems = "ProjectReference,PackageReference,FrameworkReference";

    /// <summary>
    /// Reads the evaluated properties and items. Returns <see langword="false"/> when the output
    /// has no property object; <paramref name="hasItems"/> reports whether it had an item object.
    /// </summary>
    public static bool TryRead(JsonElement root, out MsBuildEvaluationData data, out bool hasItems)
    {
        data = new MsBuildEvaluationData();
        hasItems = false;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("Items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            hasItems = true;
            ReadItems(items, data);
        }

        if (!root.TryGetProperty("Properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in properties.EnumerateObject())
        {
            data.SetProperty(property.Name, GetString(property.Value));
        }

        return true;
    }

    private static void ReadItems(JsonElement items, MsBuildEvaluationData data)
    {
        foreach (var itemName in MsBuildEvaluationData.PathItems)
        {
            if (!TryGetItemGroup(items, itemName, out var itemGroup))
            {
                continue;
            }

            data.DeclareItems(itemName);
            foreach (var item in itemGroup.EnumerateArray())
            {
                data.AddItemPath(itemName, GetString(item, "FullPath") ?? GetString(item, "Identity"));
            }
        }

        if (TryGetItemGroup(items, "Using", out var usings))
        {
            foreach (var item in usings.EnumerateArray())
            {
                data.AddGlobalUsing(
                    GetString(item, "Identity"),
                    bool.TryParse(GetString(item, "Static"), out var isStatic) && isStatic,
                    GetString(item, "Alias"));
            }
        }

        foreach (var (itemName, kind) in MsBuildEvaluationData.ReferenceItems)
        {
            if (!TryGetItemGroup(items, itemName, out var itemGroup))
            {
                continue;
            }

            foreach (var item in itemGroup.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("Identity", out var identity)
                    && identity.ValueKind == JsonValueKind.String)
                {
                    data.AddReference(kind, identity.GetString());
                }
            }
        }
    }

    private static bool TryGetItemGroup(JsonElement items, string itemName, out JsonElement itemGroup) =>
        items.TryGetProperty(itemName, out itemGroup) && itemGroup.ValueKind == JsonValueKind.Array;

    private static string? GetString(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value)
            ? GetString(value)
            : null;

    private static string? GetString(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
