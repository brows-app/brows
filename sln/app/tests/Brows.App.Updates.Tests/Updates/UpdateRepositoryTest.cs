namespace Brows.Updates;

[TestFixture]
internal sealed class UpdateRepositoryTest {
    [TestCase(null, "brows-app/brows")]
    [TestCase("owner/name", "owner/name")]
    [TestCase("bad value", "brows-app/brows")]
    [TestCase("a/b/c", "brows-app/brows")]
    [TestCase("a/..", "brows-app/brows")]
    public void From_AcceptsOnlyRepositoryNames(string input, string expected) {
        Assert.That(UpdateRepository.From(input), Is.EqualTo(expected));
    }
}
