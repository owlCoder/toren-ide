using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Toren.TestFixtures.MSTest;

[TestClass]
public sealed class SmokeTests
{
    [TestMethod]
    public void Passes()
    {
        Assert.IsTrue(true);
    }
}
