namespace Toren.Workspaces.Adapters;

/// <summary>The collection targets that batch evaluation injects into each project.</summary>
internal static class MsBuildEvaluationTargets
{
    public const string FileName = "Toren.Evaluation.targets";
    public const string EvaluationTarget = "_TorenCollectEvaluation";
    public const string CompilerInputsTarget = "_TorenCollectCompilerInputs";

    /// <summary>
    /// Design-time targets that resolve compiler inputs without compiling: SDK/package
    /// generators, Razor inputs, framework preprocessor symbols and reference assemblies.
    /// </summary>
    public static readonly string[] CompilerInputTargets =
        ["PrepareForBuild", "GenerateGlobalUsings", "ResolveReferences", "GenerateMSBuildEditorConfigFile"];

    private static readonly Lazy<string> Content = new(ReadContent);

    public static string Text => Content.Value;

    private static string ReadContent()
    {
        using var stream = typeof(MsBuildEvaluationTargets).Assembly.GetManifestResourceStream(FileName)
            ?? throw new InvalidOperationException($"Embedded resource '{FileName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
