using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Toren.Core.Debugging.Protocol;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DapRequestEncoderTests
{
    [Test]
    public void EncodeCreatesFramedRequestWithArguments()
    {
        var encoder = new DapRequestEncoder(new DapSequenceGenerator());

        var request = encoder.Encode(
            "initialize",
            new { clientID = "toren", supportsVariableType = true });
        var frame = DapMessageFraming.TryRead(request.Frame);

        Assert.That(frame.IsSuccess, Is.True);
        Assert.That(frame.Value!.IsComplete, Is.True);
        using var document = JsonDocument.Parse(frame.Value.Payload!);
        var root = document.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(request.Sequence, Is.EqualTo(1));
            Assert.That(request.Command, Is.EqualTo("initialize"));
            Assert.That(root.GetProperty("seq").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("request"));
            Assert.That(root.GetProperty("command").GetString(), Is.EqualTo("initialize"));
            Assert.That(root.GetProperty("arguments").GetProperty("clientID").GetString(), Is.EqualTo("toren"));
        });
    }

    [Test]
    public void EncodeOmitsArgumentsWhenNotProvided()
    {
        var encoder = new DapRequestEncoder(new DapSequenceGenerator());

        var first = encoder.Encode("threads");
        var second = encoder.Encode("disconnect");
        var decoded = DapMessageFraming.TryRead(first.Frame);

        Assert.That(decoded.IsSuccess, Is.True);
        using var document = JsonDocument.Parse(decoded.Value!.Payload!);
        Assert.Multiple(() =>
        {
            Assert.That(first.Sequence, Is.EqualTo(1));
            Assert.That(second.Sequence, Is.EqualTo(2));
            Assert.That(document.RootElement.TryGetProperty("arguments", out _), Is.False);
            Assert.That(Encoding.UTF8.GetString(first.Frame), Does.Contain("\"type\":\"request\""));
        });
    }
}
