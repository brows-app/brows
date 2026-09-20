using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO;

[TestFixture]
internal sealed class FileSysTest {
    private string Temp;
    private DirectoryInfo TempInfo;

    private IFileSys Subject {
        get => field ??= new FileSys();
        set => field = value;
    }

    private string Write(string text) {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        File.WriteAllText(path, text);
        return path;
    }

    [SetUp]
    public void SetUp() {
        Subject = null;
        Temp = Path.Combine(Path.GetTempPath(), $"Brows.Abstractions.IO.Tests_{Guid.NewGuid()}");
        TempInfo = new DirectoryInfo(Temp);
        TempInfo.Create();
    }

    [TearDown]
    public void TearDown() {
        if (TempInfo.Exists) {
            TempInfo.Delete(recursive: true);
        }
    }

    [Test]
    public async Task WriteFileText_WritesFileText() {
        var text = "\t\tfoo\r\n\rbar  baz @ ";
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        await Subject.WriteFileText(path, text);
        var actual = File.ReadAllText(path);
        Assert.That(actual, Is.EqualTo(text));
    }

    [Test]
    public async Task WriteFileText_UsesResiliency() {
        var text = "ABCDEFG hijklmnop";
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        var open = false;
        var close = false;
        var done = false;
        _ = Task.Run(() => {
            using var file = File.OpenWrite(path);
            open = true;
            SpinWait.SpinUntil(() => close);
        });
        SpinWait.SpinUntil(() => open);
        _ = Task.Run(async () => {
            await Subject.WriteFileText(path, text, new() { Resiliency = 6, ResiliencyDelay = 250 });
            done = true;
        });
        await Task.Delay(250);
        close = true;
        SpinWait.SpinUntil(() => done);
        var actual = File.ReadAllText(path);
        Assert.That(actual, Is.EqualTo(text));
    }

    [Test]
    public async Task ReadFileText_ReturnsFileText() {
        var text = "  All the contents\nand All the lines\r\n and all the CRs\t";
        var path = Write(text);
        var read = await Subject.ReadFileText(path);
        Assert.That(read, Is.EqualTo(text));
    }

    [Test]
    public void ReadFileText_ThrowsIfNotFound() {
        var path = Path.Combine(Temp, "does-not-exist.txt");
        Assert.That(
            () => Subject.ReadFileText(path),
            Throws.InstanceOf<FileNotFoundException>());
    }

    [Test]
    public async Task ReadFileText_DoesNotThrowIfNotFound() {
        var path = Path.Combine(Temp, "does-not-exist.txt");
        var read = await Subject.ReadFileText(path, new() { ThrowOnNotFound = false });
        Assert.That(read, Is.Null);
    }

    [Test]
    public async Task CreateDirectory_CreatesDirectory() {
        var path = Path.Combine(Temp, "new-dir");
        await Subject.CreateDirectory(path);
        Assert.That(Directory.Exists(path));
    }

    [Test]
    public void GetDirectoryFiles_ThrowsIfNotFound() {
        var path = Path.Combine(Temp, "nope");
        Assert.That(
            () => Subject.GetDirectoryFiles(path),
            Throws.InstanceOf<DirectoryNotFoundException>());
    }

    [Test]
    public async Task GetDirectoryFiles_DoesNotThrowIfNotFound() {
        var path = Path.Combine(Temp, "nope");
        var result = await Subject.GetDirectoryFiles(path, new() { ThrowOnNotFound = false });
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DeleteDirectory_DeletesRecursively() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "1"));
        File.WriteAllText(Path.Combine(path, "file"), "foobar");
        File.WriteAllText(Path.Combine(path, "1", "text"), "bar baz");
        await Subject.DeleteDirectory(path, recursive: true);
        Assert.That(Directory.Exists(path), Is.False);
    }

    [Test]
    public void DeleteDirectory_ThrowsIfNotEmpty() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "1"));
        File.WriteAllText(Path.Combine(path, "file"), "foobar");
        File.WriteAllText(Path.Combine(path, "1", "text"), "bar baz");
        Assert.That(
            () => Subject.DeleteDirectory(path),
            Throws.InstanceOf<IOException>());
    }

    [Test]
    public void DeleteDirectory_DoesNotThrowIfNotFound() {
        Assert.That(
            () => Subject.DeleteDirectory(
                Path.Combine(Temp, "does not exist"),
                arg: new() { ThrowOnNotFound = false }),
            Throws.Nothing);
    }

    [Test]
    public void DeleteDirectory_ThrowsIfNotFound() {
        Assert.That(
            () => Subject.DeleteDirectory(
                Path.Combine(Temp, "does not exist")),
            Throws.InstanceOf<IOException>());
    }

    [Test]
    public async Task DeleteDirectory_UsesResiliency() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        var path1 = Path.Combine(path, "1");
        var ready = false;
        var trying = false;
        _ = Task.Run(async () => {
            Directory.CreateDirectory(path1);
            File.WriteAllText(Path.Combine(path1, "text"), "bar baz");
            ready = true;
            SpinWait.SpinUntil(() => trying);
            await Task.Delay(250);
            Directory.Delete(path1, recursive: true);
        });
        SpinWait.SpinUntil(() => ready);
        await Task.Run(async () => {
            trying = true;
            await Subject.DeleteDirectory(path, recursive: false, arg: new() {
                Resiliency = 25,
                ResiliencyDelay = 100
            });
        });
    }

    [Test]
    public async Task FileExists_ReturnsTrue() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        File.WriteAllText(path, "");
        Assert.That(await Subject.FileExists(path));
    }

    [Test]
    public async Task FileExists_ReturnsFalse() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        Assert.That(await Subject.FileExists(path), Is.False);
    }

    [Test]
    public async Task DirectoryExists_ReturnsTrue() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        Directory.CreateDirectory(path);
        Assert.That(await Subject.DirectoryExists(path));
    }

    [Test]
    public async Task DirectoryExists_ReturnsFalse() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        Assert.That(await Subject.DirectoryExists(path), Is.False);
    }

    [Test]
    public async Task DirectoryExists_ReturnsFalse_WhenPathIsFile() {
        var guid = Guid.NewGuid();
        var path = Path.Combine(Temp, $"{guid}");
        File.WriteAllText(path, "");
        Assert.That(await Subject.DirectoryExists(path), Is.False);
    }
}
