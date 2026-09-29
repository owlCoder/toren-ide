using NUnit.Framework;
using Toren.Core.Debugging.Protocol;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DapPendingRequestRegistryTests
{
    [Test]
    public void CompleteCorrelatesAndRemovesPendingRequest()
    {
        var registry = new DapPendingRequestRegistry();
        var request = new DapOutboundRequest(7, "threads", []);
        registry.Register(request);
        var response = new DapProtocolMessage(
            8,
            DapProtocolMessageKind.Response,
            "threads",
            null,
            7,
            true,
            "{}");

        var result = registry.Complete(response);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(new DapPendingRequest(7, "threads")));
            Assert.That(registry.Count, Is.Zero);
        });
    }

    [Test]
    public void CompleteKeepsPendingRequestWhenCommandDoesNotMatch()
    {
        var registry = new DapPendingRequestRegistry();
        registry.Register(new DapOutboundRequest(3, "stackTrace", []));
        var response = new DapProtocolMessage(
            4,
            DapProtocolMessageKind.Response,
            "threads",
            null,
            3,
            true,
            "{}");

        var result = registry.Complete(response);

        Assert.That(result.IsFailure, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error.Code, Is.EqualTo("debug.dap.response.command-mismatch"));
            Assert.That(registry.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void CompleteRejectsUnknownRequestSequence()
    {
        var registry = new DapPendingRequestRegistry();
        var response = new DapProtocolMessage(
            5,
            DapProtocolMessageKind.Response,
            "continue",
            null,
            99,
            true,
            "{}");

        var result = registry.Complete(response);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("debug.dap.response.unknown-request"));
    }
}
