using System;

namespace Brows.Commands;

[TestFixture]
internal sealed class SevenZipTest {
    [TestCase(@"C:\panel", @"\\server\share\archive.7z", @"\\server\share\archive.7z")]
    [TestCase(@"C:\panel", @"D:\output\archive.7z", @"D:\output\archive.7z")]
    [TestCase(@"C:\panel", @"\archive.7z", @"C:\archive.7z")]
    [TestCase(@"C:\panel", @"archive.7z", @"C:\panel\archive.7z")]
    [TestCase(@"C:\panel", @"sub\archive", @"C:\panel\sub\archive.7z")]
    [TestCase(@"C:\panel", @"C:archive.7z", @"C:\panel\archive.7z")]
    [TestCase(@"D:\panel", @"C:archive.7z", @"C:\archive.7z")]
    public void ResolveOutputPath_PreservesThePathRootOrUsesThePanelDirectory(
        string directory,
        string output,
        string expected) {
        Assert.That(SevenZip.ResolveOutputPath(directory, output), Is.EqualTo(expected));
    }
}
