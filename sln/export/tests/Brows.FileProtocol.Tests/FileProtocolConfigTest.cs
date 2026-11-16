using Brows.Tests;

namespace Brows;

[TestFixture]
public sealed class FileProtocolConfigTest {
    [Test]
    public void StreamSourceConcurrency_DefaultsToTwo() {
        Assert.That(new TestConfig().StreamSourceConcurrency, Is.EqualTo(2));
    }

    [TestCase(7)]
    [TestCase(1)]
    [TestCase(0)]
    [TestCase(-1)]
    public void StreamSourceConcurrency_AcceptsAnyValue(int value) {
        var subject = new TestConfig { StreamSourceConcurrency = value };
        Assert.That(subject.StreamSourceConcurrency, Is.EqualTo(value));
    }
}
