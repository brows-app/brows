using System;

namespace Brows;

[TestFixture]
public sealed class FileProtocolUriTest {
    private static Uri Url(string uri) {
        return new Uri(uri);
    }

    [TestCase("ftp://user:secret@host/path/", "ftp://user:****@host/path/")]
    [TestCase("sftp://user:@host/", "sftp://user:****@host/")]
    [TestCase("ftp://user@host/path/", "ftp://user@host/path/")]
    [TestCase("ftp://host/path/", "ftp://host/path/")]
    public void Redact_ReplacesAPasswordWithAsterisks(string uri, string expected) {
        Assert.That(FileProtocolUri.Redact(Url(uri)), Is.EqualTo(expected));
    }

    [Test]
    public void Redact_ReturnsNullForNull() {
        Assert.That(FileProtocolUri.Redact(null), Is.Null);
    }

    [Test]
    public void Extend_AppendsASinglePart() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a"));
    }

    [Test]
    public void Extend_AppendsATrailingSlashWhenRequested() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a"], includeTrailingSlash: true);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a/"));
    }

    [Test]
    public void Extend_AppendsMultipleParts() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/a/b/"), ["c", "d"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a/b/c/d"));
    }

    [Test]
    public void Extend_AppendsMultiplePartsWithATrailingSlash() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/a/b/"), ["c", "d"], includeTrailingSlash: true);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a/b/c/d/"));
    }

    [TestCase("ftp://host/a/b")]
    [TestCase("ftp://host/a/b/")]
    [TestCase("ftp://host/a/b///")]
    public void Extend_NormalizesTheTrailingSlashOfTheOriginalUri(string original) {
        var uri = FileProtocolUri.Extend(Url(original), ["c"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a/b/c"));
    }

    [Test]
    public void Extend_TrimsTheTrailingSlashOfAPart() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a/"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a"));
    }

    [Test]
    public void Extend_AcceptsAPartThatContainsSeparators() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a/b/c"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a/b/c"));
    }

    [Test]
    public void Extend_IgnoresAnEmptyPart() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/a/"), ["", "b"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a/b"));
    }

    [Test]
    public void Extend_WithNoPartsReturnsTheOriginalPathWithoutATrailingSlash() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/a/"), [], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a"));
    }

    [Test]
    public void Extend_WithNoPartsReturnsTheOriginalPathWithATrailingSlash() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/a"), [], includeTrailingSlash: true);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/a/"));
    }

    [Test]
    public void Extend_AtTheRootWithNoPartsAndATrailingSlashIsTheRoot() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), [], includeTrailingSlash: true);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host/"));
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public void Extend_PreservesServerAbsoluteRoot(string scheme) {
        var root = Url($"{scheme}://host//");

        var appended = FileProtocolUri.Extend(root, ["child"], includeTrailingSlash: false);
        var unchanged = FileProtocolUri.Extend(root, [], includeTrailingSlash: true);

        Assert.Multiple(() => {
            Assert.That(appended.AbsolutePath, Is.EqualTo("//child"));
            Assert.That(unchanged.AbsolutePath, Is.EqualTo("//"));
        });
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public void FtpPathHelpers_RejectUnsupportedTripleSlashRoot(string scheme) {
        var root = Url($"{scheme}://host///");
        var descendant = Url($"{scheme}://host///child");

        Assert.Multiple(() => {
            Assert.That(
                () => FileProtocolUri.Extend(root, ["child"], includeTrailingSlash: false),
                Throws.ArgumentException);
            Assert.That(
                () => FileProtocolUri.RelativePath(root, descendant),
                Throws.ArgumentException);
        });
    }

    [Test]
    public void Extend_PreservesTheScheme() {
        var uri = FileProtocolUri.Extend(Url("sftp://host/"), ["a"], includeTrailingSlash: false);
        Assert.That(uri.Scheme, Is.EqualTo("sftp"));
    }

    [Test]
    public void Extend_PreservesANonDefaultPort() {
        var uri = FileProtocolUri.Extend(Url("ftp://host:2121/"), ["a"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("ftp://host:2121/a"));
    }

    [Test]
    public void Extend_PreservesTheUserInfo() {
        var uri = FileProtocolUri.Extend(Url("ftp://user:pass@host/"), ["a"], includeTrailingSlash: false);
        Assert.That(uri.UserInfo, Is.EqualTo("user:pass"));
    }

    [Test]
    public void Extend_PreservesTheQuery() {
        var uri = FileProtocolUri.Extend(Url("http://host/a?q=1"), ["b"], includeTrailingSlash: false);
        Assert.That(uri.ToString(), Is.EqualTo("http://host/a/b?q=1"));
    }

    [Test]
    public void Extend_EscapesASpaceInAPart() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a b"], includeTrailingSlash: false);
        Assert.That(uri.AbsolutePath, Is.EqualTo("/a%20b"));
    }

    [Test]
    public void Extend_UnescapesToTheOriginalPart() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a b"], includeTrailingSlash: false);
        Assert.That(Uri.UnescapeDataString(uri.AbsolutePath), Is.EqualTo("/a b"));
    }

    [Test]
    public void Extend_EscapesAQuestionMarkInAPart() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a?b"], includeTrailingSlash: false);
        Assert.That(uri.AbsolutePath, Is.EqualTo("/a%3Fb"));
    }

    [Test]
    public void Extend_EscapesAHashInAPart() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/"), ["a#b"], includeTrailingSlash: false);
        Assert.That(uri.AbsolutePath, Is.EqualTo("/a%23b"));
    }

    [Test]
    public void Extend_DoesNotDoubleEscapeAnAlreadyEscapedOriginalUri() {
        var uri = FileProtocolUri.Extend(Url("ftp://host/a%20b/"), ["c"], includeTrailingSlash: false);
        Assert.That(Uri.UnescapeDataString(uri.AbsolutePath), Is.EqualTo("/a b/c"));
    }

    [TestCase("..")]
    [TestCase("nested/../outside")]
    public void Extend_RejectsDotSegments(string path) {
        Assert.Throws<ArgumentException>(() =>
            FileProtocolUri.Extend(Url("ftp://host/root/"), [path], includeTrailingSlash: false));
    }
    [Test]
    public void Extend_ThrowsWhenUriIsNull() {
        Assert.Throws<ArgumentNullException>(() => FileProtocolUri.Extend(null, ["a"], includeTrailingSlash: false));
    }

    [Test]
    public void Extend_ThrowsWhenPathsIsNull() {
        Assert.Throws<ArgumentNullException>(() =>
            FileProtocolUri.Extend(Url("ftp://host/"), null, includeTrailingSlash: false));
    }

    [Test]
    public void Extend_ThrowsWhenAPartIsNull() {
        Assert.Throws<ArgumentNullException>(() =>
            FileProtocolUri.Extend(Url("ftp://host/"), [null], includeTrailingSlash: false));
    }

    [Test]
    public void Extend_DoesNotMutateTheOriginalUri() {
        var original = Url("ftp://host/a/");
        FileProtocolUri.Extend(original, ["b"], includeTrailingSlash: true);
        Assert.That(original.ToString(), Is.EqualTo("ftp://host/a/"));
    }

    [Test]
    public void Extend_AppendsDecodedRelativePathsWithoutDoubleEncoding() {
        var root = Url("ftp://host/root/");
        var original = FileProtocolUri.Extend(root, ["folder name", "literal%2Fname"], includeTrailingSlash: false);
        var relative = FileProtocolUri.RelativePath(root, original);
        var extended = FileProtocolUri.Extend(root, [relative], includeTrailingSlash: false);
        Assert.That(extended.AbsolutePath, Is.EqualTo(original.AbsolutePath));
    }

    [TestCase("ftp://host/a/b", "ftp://host/a/")]
    [TestCase("ftp://host/a/b/", "ftp://host/a/")]
    [TestCase("ftp://host/a", "ftp://host/")]
    public void Parent_ReturnsTheContainingDirectory(string uri, string expected) {
        Assert.That(FileProtocolUri.Parent(Url(uri)).ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Parent_ReturnsNullAtTheRoot() {
        Assert.That(FileProtocolUri.Parent(Url("ftp://host/")), Is.Null);
    }

    [Test]
    public void Parent_PreservesThePortAndEscapedPath() {
        var parent = FileProtocolUri.Parent(Url("ftp://host:2121/a%20b/c"));
        Assert.That(parent.AbsolutePath, Is.EqualTo("/a%20b/"));
    }

    [TestCase("ftp://host/root", "ftp://host/root/a", "a")]
    [TestCase("ftp://host/root/", "ftp://host/root/a/b/", "a/b/")]
    [TestCase("ftp://host/", "ftp://host/a", "a")]
    public void RelativePath_ReturnsTheDescendantPath(string baseUri, string uri, string expected) {
        var path = FileProtocolUri.RelativePath(Url(baseUri), Url(uri));
        Assert.That(path, Is.EqualTo(expected));
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public void RelativePath_PreservesServerAbsoluteRootAndRejectsLoginRelativePaths(string scheme) {
        var serverRoot = Url($"{scheme}://host//");
        var serverChild = Url($"{scheme}://host//child/");
        var loginRelativeChild = Url($"{scheme}://host/child/");

        Assert.Multiple(() => {
            Assert.That(FileProtocolUri.RelativePath(serverRoot, serverChild), Is.EqualTo("child/"));
            Assert.That(
                () => FileProtocolUri.RelativePath(serverRoot, loginRelativeChild),
                Throws.ArgumentException);
        });
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public void RelativePath_RejectsServerAbsoluteChildOfLoginRelativeRoot(string scheme) {
        var loginRelativeRoot = Url($"{scheme}://host/");
        var serverAbsoluteChild = Url($"{scheme}://host//child/");

        Assert.That(
            () => FileProtocolUri.RelativePath(loginRelativeRoot, serverAbsoluteChild),
            Throws.ArgumentException);
    }

    [Test]
    public void RelativePath_UnescapesPathSegmentsExactlyOnce() {
        var baseUri = Url("ftp://host/root/");
        var uri = FileProtocolUri.Extend(baseUri, ["100% done"], includeTrailingSlash: false);
        Assert.That(FileProtocolUri.RelativePath(baseUri, uri), Is.EqualTo("100% done"));
    }

    [Test]
    public void RelativePath_PreservesLiteralPercentSequences() {
        var baseUri = Url("ftp://host/root/");
        var uri = FileProtocolUri.Extend(baseUri, ["literal%2Fname"], includeTrailingSlash: false);
        Assert.That(FileProtocolUri.RelativePath(baseUri, uri), Is.EqualTo("literal%2Fname"));
    }

    [Test]
    public void RelativePath_RejectsAnEncodedSeparator() {
        var baseUri = Url("ftp://host/root/");
        var uri = Url("ftp://host/root/a%2Fb");
        Assert.Throws<ArgumentException>(() => FileProtocolUri.RelativePath(baseUri, uri));
    }

    [Test]
    public void RelativePath_RejectsAnEncodedDotSegment() {
        var baseUri = Url("ftp://host/root/");
        var uri = Url("ftp://host/root/%2e%2e/outside");
        Assert.Throws<ArgumentException>(() => FileProtocolUri.RelativePath(baseUri, uri));
    }

    [TestCase("ftp://other/root/a")]
    [TestCase("ftp://host:2121/root/a")]
    [TestCase("sftp://host/root/a")]
    public void RelativePath_RejectsAnotherAuthority(string uri) {
        var baseUri = Url("ftp://host/root/");
        Assert.Throws<ArgumentException>(() => FileProtocolUri.RelativePath(baseUri, Url(uri)));
    }

    [Test]
    public void RelativePath_RejectsSiblingPathsWithTheSamePrefix() {
        var baseUri = Url("ftp://host/root");
        var uri = Url("ftp://host/root-other/file");
        Assert.Throws<ArgumentException>(() => FileProtocolUri.RelativePath(baseUri, uri));
    }

    [Test]
    public void CombinePath_UsesProtocolSeparatorsAndEscapesLiteralNames() {
        var path = FileProtocolUri.CombinePath(Url("ftp://host/root/"), "nested/folder name/file%2F?.txt");
        Assert.That(path, Is.EqualTo("/root/nested/folder name/file%2F?.txt"));
    }

    [Test]
    public void CombinePath_PreservesEncodedSlashTextInALiteralName() {
        var path = FileProtocolUri.CombinePath(Url("ftp://host/root/"), "literal%2Fname");
        Assert.That(path, Is.EqualTo("/root/literal%2Fname"));
    }

    [TestCase("/outside/file")]
    [TestCase("../outside/file")]
    [TestCase("nested/../outside/file")]
    [TestCase("./file")]
    public void CombinePath_RejectsRootedAndDotSegments(string relativePath) {
        Assert.Throws<ArgumentException>(() =>
            FileProtocolUri.CombinePath(Url("ftp://host/root/"), relativePath));
    }

    [Test]
    public void CombinePath_RejectsAnEncodedSeparatorInTheBaseUri() {
        var baseUri = Url("ftp://host/root/a%2Fb/");
        Assert.Throws<ArgumentException>(() => FileProtocolUri.CombinePath(baseUri, "file.txt"));
    }

    [Test]
    public void CombinePath_PreservesEncodedDotTextAsALiteralName() {
        var path = FileProtocolUri.CombinePath(Url("ftp://host/root/"), "%2e%2e/file.txt");
        Assert.That(path, Is.EqualTo("/root/%2e%2e/file.txt"));
    }

    [Test]
    public void RelativePath_PreservesEncodedDotTextAsALiteralName() {
        var baseUri = Url("ftp://host/root/");
        var uri = FileProtocolUri.Extend(baseUri, ["%2e%2e", "file.txt"], includeTrailingSlash: false);
        Assert.That(FileProtocolUri.RelativePath(baseUri, uri), Is.EqualTo("%2e%2e/file.txt"));
    }
}
