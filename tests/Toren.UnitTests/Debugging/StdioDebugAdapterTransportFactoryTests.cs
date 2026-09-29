using NUnit.Framework;
using Toren.Debugging.Adapters;
using Toren.Debugging.Models;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class StdioDebugAdapterTransportFactoryTests
{
    [Test]
    public void StartReturnsRecoverableFailureWhenAdapterDoesNotExist()
    {
        var factory = new StdioDebugAdapterTransportFactory();
        var descriptor = new DebugAdapterDescriptor(
            $"toren-missing-debug-adapter-{Guid.NewGuid():N}",
            [],
            "Missing adapter");

        var result = factory.Start(descriptor);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("debug.adapter.start-failed"));
    }
}
