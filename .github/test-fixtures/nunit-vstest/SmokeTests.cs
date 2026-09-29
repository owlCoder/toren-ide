using NUnit.Framework;

namespace NUnitFixture;

public sealed class SmokeTests
{
    [Test]
    public void PassingTest()
    {
        Assert.That(2 + 2, Is.EqualTo(4));
    }
}
