using System;

namespace Brows;

[TestFixture]
internal sealed class BrowsTestTest {
    [Test]
    public void BrowsTest_EnvOutput() {
        var env = BrowsTest.Env();
        Console.WriteLine($"Brows test env: {env}");
    }
}
