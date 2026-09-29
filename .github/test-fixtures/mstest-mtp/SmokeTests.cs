using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Toren.TestFixtures.Mtp;

[TestClass]
public sealed class SmokeTests
{
    [TestMethod]
    public void Passes()
    {
        Assert.IsTrue(true);
    }
}
