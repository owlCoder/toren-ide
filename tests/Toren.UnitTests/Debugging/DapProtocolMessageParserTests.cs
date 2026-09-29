using NUnit.Framework;
using Toren.Debugging.Protocol;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DapProtocolMessageParserTests
{
    [Test]
    public void ParseReadsRequestEnvelope()
    {
        const string payload = "{\"seq\":1,\"type\":\"request\",\"command\":\"initialize\",\"arguments\":{}}";
        var result = DapProtocolMessageParser.Parse(payload);
        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Sequence, Is.EqualTo(1));
            Assert.That(result.Value.Kind, Is.EqualTo(DapProtocolMessageKind.Request));
            Assert.That(result.Value.Command, Is.EqualTo("initialize"));
        });
    }

    [Test]
    public void ParseReadsResponseCorrelationFields()
    {
        const string payload = "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"success\":true,\"command\":\"initialize\"}";
        var result = DapProtocolMessageParser.Parse(payload);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.RequestSequence, Is.EqualTo(1));
        Assert.That(result.Value.Success, Is.True);
    }

    [Test]
    public void ParseReadsEventEnvelope()
    {
        const string payload = "{\"seq\":3,\"type\":\"event\",\"event\":\"stopped\",\"body\":{}}";
        var result = DapProtocolMessageParser.Parse(payload);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.EventName, Is.EqualTo("stopped"));
    }

    [Test]
    public void ParseRejectsInvalidJson()
    {
        var result = DapProtocolMessageParser.Parse("{");
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("debug.dap.message.json-invalid"));
    }

    [Test]
    public void ParseRejectsIncompleteResponseEnvelope()
    {
        const string payload = "{\"seq\":2,\"type\":\"response\",\"request_seq\":1,\"command\":\"initialize\"}";
        var result = DapProtocolMessageParser.Parse(payload);
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Code, Is.EqualTo("debug.dap.message.envelope-invalid"));
    }

    [Test]
    public void SequenceGeneratorReturnsMonotonicSequences()
    {
        var generator = new DapSequenceGenerator();
        Assert.That(generator.Next(), Is.EqualTo(1));
        Assert.That(generator.Next(), Is.EqualTo(2));
        Assert.That(generator.Next(), Is.EqualTo(3));
    }
}
