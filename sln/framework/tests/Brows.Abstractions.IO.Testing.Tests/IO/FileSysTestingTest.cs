using NUnit.Framework;

namespace Brows.IO;

[TestFixture]
internal sealed class FileSysTestingTest {
    [Test]
    public void Integration_IsFileSysInstance() {
        var result = FileSysTesting.Integration();
        Assert.That(result, Is.TypeOf<FileSys>());
    }
}
