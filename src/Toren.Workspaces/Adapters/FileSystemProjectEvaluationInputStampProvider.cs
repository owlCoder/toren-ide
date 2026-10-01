using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using Toren.Core.IO;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Adapters;

/// <summary>
/// Fingerprints the files that MSBuild evaluation reads. The workspace stamp covers every
/// file path under the workspace, because globs make any added or removed file a potential
/// input, plus the content state of solutions, projects and MSBuild imports. Inputs that
/// cannot be discovered from the directory tree are covered by the project stamp.
/// </summary>
public sealed class FileSystemProjectEvaluationInputStampProvider(IWorkspaceFileProvider workspaceFileProvider)
    : IProjectEvaluationInputStampProvider
{
    // Files above the workspace directory that the SDK and NuGet locate by walking upwards.
    private static readonly string[] AncestorInputNames =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Build.rsp",
        "Directory.Packages.props",
        "global.json",
        "NuGet.config",
        "nuget.config",
    ];

    private readonly IWorkspaceFileProvider _workspaceFileProvider = workspaceFileProvider
        ?? throw new ArgumentNullException(nameof(workspaceFileProvider));

    public async Task<string> GetWorkspaceStampAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        var files = await _workspaceFileProvider
            .GetFilesAsync(workspace.Path, cancellationToken)
            .ConfigureAwait(false);
        if (!files.IsSuccess)
        {
            // Evaluation reports the unavailable workspace; nothing is reused across the failure.
            return $"unavailable:{files.Error.Code}";
        }

        return await Task.Run(
            () =>
            {
                using var stamp = new StampBuilder();
                foreach (var file in files.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stamp.Append(file.RelativePath);
                    if (IsEvaluationInput(file.Name))
                    {
                        stamp.AppendFileState(file.Path);
                    }
                }

                var workspaceDirectory = Directory.Exists(workspace.Path)
                    ? Path.GetFullPath(workspace.Path)
                    : Path.GetDirectoryName(Path.GetFullPath(workspace.Path));
                for (var directory = Path.GetDirectoryName(workspaceDirectory);
                     directory is not null;
                     directory = Path.GetDirectoryName(directory))
                {
                    foreach (var name in AncestorInputNames)
                    {
                        var path = Path.Combine(directory, name);
                        stamp.Append(path);
                        stamp.AppendFileState(path);
                    }
                }

                return stamp.ToString();
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<ProjectInputStamp> GetProjectStampAsync(
        IReadOnlyList<WorkspaceProject> projects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projects);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () =>
            {
                using var stamp = new StampBuilder();
                var visited = new HashSet<string>(FileSystemPath.Comparer);
                foreach (var project in projects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendOnce(project.Path);
                    AppendOnce(project.Metadata.DirectoryBuildPropsPath);
                    AppendOnce(project.Metadata.DirectoryBuildTargetsPath);
                    AppendOnce(project.Metadata.DirectoryPackagesPropsPath);
                    AppendOnce(project.Metadata.ProjectAssetsFilePath);
                }

                return new ProjectInputStamp(stamp.ToString(), stamp.LastWriteTimeUtc);

                void AppendOnce(string? path)
                {
                    if (!string.IsNullOrWhiteSpace(path) && visited.Add(path))
                    {
                        stamp.Append(path);
                        stamp.AppendFileState(path);
                    }
                }
            },
            cancellationToken);
    }

    private static bool IsEvaluationInput(string fileName)
    {
        var extension = Path.GetExtension(fileName.AsSpan());
        return extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rsp", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("global.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("nuget.config", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("packages.lock.json", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StampBuilder : IDisposable
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public DateTime LastWriteTimeUtc { get; private set; } = DateTime.MinValue;

        public void Append(string value)
        {
            _hash.AppendData(MemoryMarshal.AsBytes(value.AsSpan()));
            // Separate consecutive values so their boundaries cannot shift unnoticed.
            _hash.AppendData([0, 0]);
        }

        public void AppendFileState(string path)
        {
            Span<byte> state = stackalloc byte[sizeof(long) * 2];
            state.Clear();
            try
            {
                var file = new FileInfo(path);
                if (file.Exists)
                {
                    var lastWriteTimeUtc = file.LastWriteTimeUtc;
                    BinaryPrimitives.WriteInt64LittleEndian(state, lastWriteTimeUtc.Ticks);
                    BinaryPrimitives.WriteInt64LittleEndian(state[sizeof(long)..], file.Length);
                    if (lastWriteTimeUtc > LastWriteTimeUtc)
                    {
                        LastWriteTimeUtc = lastWriteTimeUtc;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
            {
                // An unreadable input is stamped as absent; evaluation reports the real failure.
                state.Clear();
            }

            _hash.AppendData(state);
        }

        public override string ToString() => Convert.ToHexString(_hash.GetCurrentHash());

        public void Dispose() => _hash.Dispose();
    }
}
