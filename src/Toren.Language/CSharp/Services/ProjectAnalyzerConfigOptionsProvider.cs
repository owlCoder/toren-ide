using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Toren.Language.CSharp.Services;

internal sealed class ProjectAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigSet _configs;

    public ProjectAnalyzerConfigOptionsProvider(IReadOnlyList<string> paths)
    {
        var configs = paths.Where(File.Exists)
            .Select(path => AnalyzerConfig.Parse(File.ReadAllText(path), Path.GetFullPath(path))).ToArray();
        _configs = AnalyzerConfigSet.Create(configs);
        GlobalOptions = new Options(_configs.GlobalConfigOptions.AnalyzerOptions);
    }

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => ForPath(tree.FilePath);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => ForPath(textFile.Path);

    private Options ForPath(string path) => new(_configs.GetOptionsForSourcePath(Path.GetFullPath(path)).AnalyzerOptions);

    private sealed class Options(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}

internal sealed class ProjectAdditionalText(string path) : AdditionalText
{
    public override string Path { get; } = System.IO.Path.GetFullPath(path);

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SourceText.From(File.ReadAllText(Path));
    }
}
