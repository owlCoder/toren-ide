using NUnit.Framework;
using Toren.Core.Results;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class ProjectCompilationReferenceResolverTests
{
    [Test]
    public void ReturnsResolvedReferencesThatExist()
    {
        var references = new[] { typeof(object).Assembly.Location };

        var result = ProjectCompilationReferenceResolver.Resolve(CreateMetadata() with { ReferencePaths = references });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(references));
        });
    }

    [Test]
    public void MissingProjectReferenceIsReportedInsteadOfCreatingFalseSemanticErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-missing-reference-{Guid.NewGuid():N}.dll");

        var result = ProjectCompilationReferenceResolver.Resolve(
            CreateMetadata() with { ReferencePaths = [typeof(object).Assembly.Location, path] });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(result.Error.Message, Does.Contain(path));
            Assert.That(result.Error.Message, Does.Contain("Build the solution"));
        });
    }

    [Test]
    public void ReferenceBuiltAfterEvaluationIsAcceptedWithoutReevaluating()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toren-built-reference-{Guid.NewGuid():N}.dll");
        var metadata = CreateMetadata() with { ReferencePaths = [path] };
        try
        {
            Assert.That(ProjectCompilationReferenceResolver.Resolve(metadata).IsFailure, Is.True);
            File.WriteAllBytes(path, []);
            Assert.That(ProjectCompilationReferenceResolver.Resolve(metadata).IsSuccess, Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void CompilerInputErrorTakesPrecedence()
    {
        var error = OperationError.Create("test.unrestored", "Run restore first.");

        var result = ProjectCompilationReferenceResolver.Resolve(CreateMetadata() with { CompilerInputsError = error });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(error));
        });
    }

    [Test]
    public void UnresolvedCompilerInputsAreReported()
    {
        var result = ProjectCompilationReferenceResolver.Resolve(CreateMetadata());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
        });
    }

    private static ProjectMetadata CreateMetadata() =>
        new(["net10.0"], "Library", "App", "App", false, false, null, null, null);
}
