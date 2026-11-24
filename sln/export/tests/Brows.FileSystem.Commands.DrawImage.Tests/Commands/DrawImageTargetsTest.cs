using Brows.Support;
using Brows.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class DrawImageTargetsTest {
    private TempDirectory Temp;

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string longPath, StringBuilder shortPath, uint length);

    /*
     * Returns the 8.3 alias of an existing file, or the path itself when the
     * volume does not create short names.
     */
    private static string ShortPath(string path) {
        var buffer = new StringBuilder(1024);
        var length = GetShortPathNameW(path, buffer, (uint)buffer.Capacity);
        if (length == 0 || length > buffer.Capacity) {
            return path;
        }
        var shortName = Path.GetFileName(buffer.ToString());
        return Path.Combine(Path.GetDirectoryName(path), shortName);
    }

    private string PathOf(string name) {
        return Path.Combine(Temp.Root.FullName, name);
    }

    private FileInfo FileOf(string name, string content) {
        return Temp.CreateFile(name, content);
    }

    private static readonly Win32KernelService Kernel = new();

    private static Task<bool> PathsAreEquivalent(string path1, string path2, CancellationToken token) {
        return Task.FromResult(Kernel.PathsAreEquivalent(path1, path2));
    }

    /*
     * Answers as a case-sensitive directory would, where names that differ
     * only by case are different files.
     */
    private static Task<bool> CaseSensitivePathsAreEquivalent(string path1, string path2, CancellationToken token) {
        return Task.FromResult(string.Equals(path1, path2, StringComparison.Ordinal));
    }

    private static DrawImageTargets Targets(bool overwrite, params (FileInfo Source, string Path)[] requests) {
        return Targets(PathsAreEquivalent, overwrite, requests);
    }

    private static DrawImageTargets Targets(Func<string, string, CancellationToken, Task<bool>> pathsAreEquivalent,
                                            bool overwrite, params (FileInfo Source, string Path)[] requests) {
        var list = new List<KeyValuePair<FileInfo, string>>();
        foreach (var request in requests) {
            list.Add(new(request.Source, request.Path));
        }
        var plan = DrawImageTargets.Plan(list, overwrite, pathsAreEquivalent, CancellationToken.None);
        return plan.GetAwaiter().GetResult();
    }

    private Dictionary<string, string> PublishInOrder(string directory, params string[] order) {
        var root = Temp.CreateDirectory(directory).FullName;
        var sources = new[] { "a.jpg", "b.jpg", "c.jpg" }
            .Select(name => new FileInfo(Path.Combine(Temp.Root.FullName, name)))
            .ToList();
        var targets = Targets(false, sources.Select(source => (source, Path.Combine(root, "out.png"))).ToArray());
        var names = new Dictionary<string, string>();
        foreach (var name in order) {
            var source = sources.Single(source => source.Name == name);
            var output = targets.Publish(source, Encoded(targets, source, name));
            names[name] = Path.GetFileName(output);
        }
        return names;
    }

    private long PublishSameName(int count) {
        var directory = Temp.CreateDirectory($"same{count}").FullName;
        var sources = Enumerable.Range(0, count)
            .Select(i => new FileInfo(Path.Combine(directory, $"source{i}.jpg")))
            .ToList();
        var temporaries = sources
            .Select(source => {
                var path = Path.Combine(directory, $"{source.Name}.tmp.png");
                File.WriteAllText(path, source.Name);
                return path;
            })
            .ToList();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var targets = Targets(false, sources.Select(source => (source, Path.Combine(directory, "same.png"))).ToArray());
        for (var i = 0; i < count; i++) {
            targets.Publish(sources[i], temporaries[i]);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(Directory.GetFiles(directory, "same*.png"), Has.Length.EqualTo(count));
        return allocated;
    }

    private string Encoded(DrawImageTargets targets, FileInfo source, string content) {
        var temporary = targets.Temporary(source);
        File.WriteAllText(temporary, content);
        return temporary;
    }

    [SetUp]
    public void SetUp() {
        Temp = new TempDirectory();
    }

    [TearDown]
    public void TearDown() {
        Temp.Dispose();
    }

    [Test]
    public void Publish_TwoSourcesForOneNameEncodedBeforePublishing_PublishesDistinctFiles() {
        var jpg = FileOf("race.jpg", "jpg source");
        var png = FileOf("race.png", "png source");
        var targets = Targets(false, (jpg, PathOf("race.png")), (png, PathOf("race.png")));
        var jpgTemporary = Encoded(targets, jpg, "from jpg");
        var pngTemporary = Encoded(targets, png, "from png");
        var jpgOutput = targets.Publish(jpg, jpgTemporary);
        var pngOutput = targets.Publish(png, pngTemporary);
        using (Assert.EnterMultipleScope()) {
            Assert.That(jpgOutput, Is.Not.EqualTo(pngOutput).IgnoreCase);
            Assert.That(File.ReadAllText(jpgOutput), Is.EqualTo("from jpg"));
            Assert.That(File.ReadAllText(pngOutput), Is.EqualTo("from png"));
            Assert.That(File.ReadAllText(png.FullName), Is.EqualTo("png source"));
            Assert.That(Temp.Root.GetFiles(), Has.Length.EqualTo(4));
        }
    }

    [Test]
    public void Publish_SuffixRequestedByAnotherSource_SkipsSuffix() {
        var first = FileOf("a.jpg", "a");
        var second = FileOf("b.jpg", "b");
        FileOf("out.png", "existing");
        var targets = Targets(false, (first, PathOf("out.png")), (second, PathOf("out (1).png")));
        var firstOutput = targets.Publish(first, Encoded(targets, first, "from a"));
        var secondOutput = targets.Publish(second, Encoded(targets, second, "from b"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(firstOutput, Is.EqualTo(PathOf("out (2).png")));
            Assert.That(secondOutput, Is.EqualTo(PathOf("out (1).png")));
        }
    }

    [Test]
    public void Publish_ExistingDestinationWithOverwrite_ReplacesDestination() {
        var source = FileOf("source.png", "source");
        var destination = FileOf("unselected.png", "unselected");
        var targets = Targets(true, (source, destination.FullName));
        var output = targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(destination.FullName));
            Assert.That(File.ReadAllText(destination.FullName), Is.EqualTo("converted"));
            Assert.That(File.Exists(PathOf("unselected (1).png")), Is.False);
        }
    }

    [Test]
    public void Publish_ExistingDestinationWithoutOverwrite_AddsSuffix() {
        var source = FileOf("source.png", "source");
        var destination = FileOf("existing.png", "existing");
        var targets = Targets(false, (source, destination.FullName));
        var output = targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(PathOf("existing (1).png")));
            Assert.That(File.ReadAllText(destination.FullName), Is.EqualTo("existing"));
            Assert.That(File.ReadAllText(output), Is.EqualTo("converted"));
        }
    }

    [Test]
    public void Publish_SourceWithOverwrite_ReplacesSource() {
        var source = FileOf("source.png", "source");
        var targets = Targets(true, (source, source.FullName));
        var output = targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(source.FullName));
            Assert.That(File.ReadAllText(source.FullName), Is.EqualTo("converted"));
        }
    }

    [Test]
    public void Publish_OtherSourceWithOverwrite_KeepsOtherSource() {
        var jpg = FileOf("photo.jpg", "jpg source");
        var png = FileOf("photo.png", "png source");
        var targets = Targets(true, (jpg, png.FullName), (png, PathOf("photo-small.png")));
        var output = targets.Publish(jpg, Encoded(targets, jpg, "from jpg"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(PathOf("photo (1).png")));
            Assert.That(File.ReadAllText(png.FullName), Is.EqualTo("png source"));
        }
    }

    [Test]
    public void Publish_OtherSourceThroughJunctionWithOverwrite_KeepsOtherSource() {
        var directory = Temp.CreateDirectory("images");
        var junction = Temp.CreateJunction("alias", directory);
        var jpg = FileOf(@"images\a.jpg", "jpg source");
        var png = FileOf(@"images\b.png", "png source");
        var requested = Path.Combine(junction.FullName, "b.png");
        var targets = Targets(true, (jpg, requested), (png, requested));
        var jpgOutput = targets.Publish(jpg, Encoded(targets, jpg, "from jpg"));
        var pngSourceAfterJpg = File.ReadAllText(png.FullName);
        var pngOutput = targets.Publish(png, Encoded(targets, png, "from png"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(pngSourceAfterJpg, Is.EqualTo("png source"));
            Assert.That(jpgOutput, Is.EqualTo(Path.Combine(junction.FullName, "b (1).png")));
            Assert.That(pngOutput, Is.EqualTo(requested));
            Assert.That(File.ReadAllText(png.FullName), Is.EqualTo("from png"));
        }
    }

    [Test]
    public void Publish_NamesDifferingOnlyByCaseWithOverwrite_PublishesBoth() {
        var upper = FileOf(@"one\Out.jpg", "upper");
        var lower = FileOf(@"two\out.jpg", "lower");
        Temp.CreateDirectory("output");
        var targets = Targets(true,
            (upper, PathOf(@"output\Out.png")),
            (lower, PathOf(@"output\out.png")));
        var upperOutput = targets.Publish(upper, Encoded(targets, upper, "from upper"));
        var lowerOutput = targets.Publish(lower, Encoded(targets, lower, "from lower"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(upperOutput, Is.Not.EqualTo(lowerOutput).IgnoreCase);
            Assert.That(File.ReadAllText(upperOutput), Is.EqualTo("from upper"));
            Assert.That(File.ReadAllText(lowerOutput), Is.EqualTo("from lower"));
        }
    }

    [Test]
    public void Publish_ExistingDestinationWithNamedStream_KeepsStream() {
        var source = FileOf("source.png", "source");
        var destination = FileOf("destination.png", "destination");
        File.WriteAllText(destination.FullName + ":review-tag", "kept");
        var targets = Targets(true, (source, destination.FullName));
        targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(destination.FullName), Is.EqualTo("converted"));
            Assert.That(File.ReadAllText(destination.FullName + ":review-tag"), Is.EqualTo("kept"));
        }
    }

    [Test]
    public void Publish_DirectoryAtDestinationWithOverwrite_AddsSuffix() {
        var source = FileOf("source.png", "source");
        var directory = Temp.CreateDirectory("out.png");
        var targets = Targets(true, (source, directory.FullName));
        var output = targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(PathOf("out (1).png")));
            Assert.That(File.ReadAllText(output), Is.EqualTo("converted"));
            Assert.That(Directory.Exists(directory.FullName), Is.True);
        }
    }

    [Test]
    public void Publish_OppositeCompletionOrders_AssignSameNames() {
        var sorted = PublishInOrder("sorted", "a.jpg", "b.jpg", "c.jpg");
        var reversed = PublishInOrder("reversed", "c.jpg", "b.jpg", "a.jpg");
        Assert.That(reversed, Is.EquivalentTo(sorted));
    }

    [Test]
    public void Publish_ManySourcesForOneName_AllocatesLinearly() {
        PublishSameName(50);
        var small = PublishSameName(200);
        var large = PublishSameName(800);
        Assert.That(large, Is.LessThan(small * 8), $"200 sources allocated {small} bytes; 800 allocated {large}.");
    }

    /*
     * Out.jpg and out.jpg are separate files in a case-sensitive directory.
     * The fixture directory cannot be made case-sensitive without elevation,
     * so the two sources are supplied as distinct entries of one directory.
     */
    [Test]
    public void Publish_SourcesDifferingOnlyByCase_KeepSeparateOutputs() {
        var upper = new FileInfo(PathOf("Out.jpg"));
        var lower = new FileInfo(PathOf("out.jpg"));
        var targets = Targets(true, (upper, PathOf("01.png")), (lower, PathOf("02.png")));
        var upperOutput = targets.Publish(upper, Encoded(targets, upper, "from upper"));
        var lowerOutput = targets.Publish(lower, Encoded(targets, lower, "from lower"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(upperOutput, Is.EqualTo(PathOf("01.png")));
            Assert.That(lowerOutput, Is.EqualTo(PathOf("02.png")));
            Assert.That(File.ReadAllText(upperOutput), Is.EqualTo("from upper"));
            Assert.That(File.ReadAllText(lowerOutput), Is.EqualTo("from lower"));
        }
    }

    [Test]
    public void Publish_PathOfOtherSourceDifferingOnlyByCase_AddsSuffix() {
        var upper = FileOf("Out.jpg", "upper source");
        var lower = new FileInfo(PathOf("out.jpg"));
        var targets = Targets(CaseSensitivePathsAreEquivalent, true,
            (upper, PathOf("upper.png")),
            (lower, upper.FullName));
        var output = targets.Publish(lower, Encoded(targets, lower, "from lower"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(PathOf("Out (1).jpg")));
            Assert.That(File.ReadAllText(upper.FullName), Is.EqualTo("upper source"));
        }
    }

    [Test]
    public void Publish_OwnSourceSpelledInOtherCaseWithOverwrite_ReplacesSource() {
        var source = FileOf("Photo.jpg", "photo source");
        var requested = PathOf("photo.jpg");
        var targets = Targets(true, (source, requested));
        var output = targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(requested));
            Assert.That(File.ReadAllText(source.FullName), Is.EqualTo("converted"));
            Assert.That(Temp.Root.GetFiles(), Has.Length.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Publish_ShortNameOfOtherSourceWithOverwrite_KeepsOtherSource(bool extendedLength) {
        var other = FileOf("Long image name for Windows alias.jpg", "long source");
        var shortPath = ShortPath(other.FullName);
        if (string.Equals(shortPath, other.FullName, StringComparison.OrdinalIgnoreCase)) {
            Assert.Ignore("The volume does not create 8.3 names.");
        }
        var requested = extendedLength ? @"\\?\" + shortPath : shortPath;
        var first = FileOf("a.jpg", "a source");
        var targets = Targets(true, (first, requested), (other, requested));
        var firstOutput = targets.Publish(first, Encoded(targets, first, "from a"));
        var otherAfterFirst = File.ReadAllText(other.FullName);
        targets.Publish(other, Encoded(targets, other, "from long"));
        var contents = Temp.Root.GetFiles().Select(file => File.ReadAllText(file.FullName)).ToList();
        using (Assert.EnterMultipleScope()) {
            Assert.That(otherAfterFirst, Is.EqualTo("long source"));
            Assert.That(File.ReadAllText(firstOutput), Is.EqualTo("from a"));
            Assert.That(File.ReadAllText(other.FullName), Is.EqualTo("from long"));
            Assert.That(contents, Is.EquivalentTo(new[] { "a source", "from long", "from a" }));
        }
    }

    /*
     * A folder named "dir." can exist only through an extended-length path,
     * which Windows takes literally. Without its prefix the path would name
     * the folder "dir" instead.
     */
    [Test]
    public void Publish_ExtendedLengthPathToFolderEndingInPeriod_PublishesToThatFolder() {
        var source = FileOf("source.png", "source");
        FileOf(@"dir\x.png", "plain");
        var literal = @"\\?\" + PathOf("dir.");
        Directory.CreateDirectory(literal);
        var requested = Path.Combine(literal, "x.png");
        try {
            var targets = Targets(true, (source, requested));
            var output = targets.Publish(source, Encoded(targets, source, "converted"));
            using (Assert.EnterMultipleScope()) {
                Assert.That(output, Is.EqualTo(requested));
                Assert.That(File.ReadAllText(requested), Is.EqualTo("converted"));
                Assert.That(File.ReadAllText(PathOf(@"dir\x.png")), Is.EqualTo("plain"));
            }
        }
        finally {
            foreach (var file in Directory.GetFiles(literal)) {
                File.Delete(file);
            }
            Directory.Delete(literal);
        }
    }

    [Test]
    public void Publish_ExtendedLengthPathWithParentSegment_DoesNotReplaceNormalizedTarget() {
        var source = FileOf("source.png", "source");
        Temp.CreateDirectory("a");
        FileOf(@"b\x.png", "plain");
        var requested = @"\\?\" + PathOf(@"a\..\b\x.png");
        var targets = Targets(true, (source, requested));
        Assert.That(() => targets.Publish(source, Encoded(targets, source, "converted")),
                    Throws.InstanceOf<IOException>());
        Assert.That(File.ReadAllText(PathOf(@"b\x.png")), Is.EqualTo("plain"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Publish_OwnShortNameWithOverwrite_ReplacesSource(bool extendedLength) {
        var source = FileOf("Long image name for Windows alias.jpg", "long source");
        var shortPath = ShortPath(source.FullName);
        if (string.Equals(shortPath, source.FullName, StringComparison.OrdinalIgnoreCase)) {
            Assert.Ignore("The volume does not create 8.3 names.");
        }
        var requested = extendedLength ? @"\\?\" + shortPath : shortPath;
        var targets = Targets(true, (source, requested));
        targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(source.FullName), Is.EqualTo("converted"));
            Assert.That(Temp.Root.GetFiles(), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void Publish_OwnSourceThroughJunctionSpelledInOtherCaseWithOverwrite_ReplacesSource() {
        var directory = Temp.CreateDirectory("images");
        var junction = Temp.CreateJunction("alias", directory);
        var source = FileOf(@"images\Photo.jpg", "photo source");
        var requested = Path.Combine(junction.FullName, "photo.jpg");
        var targets = Targets(true, (source, requested));
        var output = targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(requested));
            Assert.That(File.ReadAllText(source.FullName), Is.EqualTo("converted"));
            Assert.That(directory.GetFiles(), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void Publish_OwnSourceSpelledInOtherCaseWithoutEquivalenceCheck_AddsSuffix() {
        var source = FileOf("Photo.jpg", "photo source");
        var targets = Targets(null, true, (source, PathOf("photo.jpg")));
        var output = targets.Publish(source, Encoded(targets, source, "converted"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(PathOf("photo (1).jpg")));
            Assert.That(File.ReadAllText(source.FullName), Is.EqualTo("photo source"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Publish_SameDirectoryThroughTwoJunctionsWithOverwrite_PublishesBoth(bool destinationExists) {
        var destination = Temp.CreateDirectory("destination");
        if (destinationExists) {
            FileOf(@"destination\out.png", "existing");
        }
        var aliasA = Temp.CreateJunction("alias-a", destination);
        var aliasB = Temp.CreateJunction("alias-b", destination);
        var a = FileOf("a.jpg", "a source");
        var b = FileOf("b.jpg", "b source");
        var targets = Targets(true,
            (a, Path.Combine(aliasA.FullName, "out.png")),
            (b, Path.Combine(aliasB.FullName, "out.png")));
        targets.Publish(a, Encoded(targets, a, "from a"));
        targets.Publish(b, Encoded(targets, b, "from b"));
        var contents = destination.GetFiles().Select(file => File.ReadAllText(file.FullName)).ToList();
        Assert.That(contents, Is.EquivalentTo(new[] { "from a", "from b" }));
    }

    [Test]
    public void Plan_CanceledBeforePlanning_DoesNotEnumerateRequests() {
        var enumerated = 0;
        var requests = Enumerable.Range(0, 1000).Select(i => {
            enumerated++;
            return new KeyValuePair<FileInfo, string>(new FileInfo(PathOf($"s{i}.jpg")), PathOf("same.png"));
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.That(
            async () => await DrawImageTargets.Plan(requests, false, null, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(enumerated, Is.Zero);
    }

    [Test]
    public void Plan_CanceledWhilePlanning_StopsPlanning() {
        using var cancellation = new CancellationTokenSource();
        var requests = Enumerable.Range(0, 1000).Select(i => {
            if (i == 999) {
                cancellation.Cancel();
            }
            return new KeyValuePair<FileInfo, string>(new FileInfo(PathOf($"s{i}.jpg")), PathOf("same.png"));
        });
        Assert.That(
            async () => await DrawImageTargets.Plan(requests, false, null, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
