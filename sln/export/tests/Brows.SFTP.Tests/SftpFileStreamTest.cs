using Brows.SFTP.Native;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

#pragma warning disable CA2022

[TestFixture]
internal sealed class SftpFileStreamTest {
    private static T CreateUninitialized<T>() where T : class {
        var instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        GC.SuppressFinalize(instance);
        return instance;
    }

    private static SftpFileStream CreateStream(bool canRead, bool canWrite) {
        return new SftpFileStream(
            CreateUninitialized<SftpClient>(),
            CreateUninitialized<SftpFile>(),
            canRead,
            canWrite);
    }

    [Test]
    public void Read_NullBuffer_ThrowsBeforeCallingClient() {
        var stream = CreateStream(canRead: true, canWrite: false);

        Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 0));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public void Read_NegativeBufferRange_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: true, canWrite: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(new byte[2], offset, count));
    }

    [TestCase(3, 0)]
    [TestCase(1, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void Read_BufferRangeOutsideArray_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: true, canWrite: false);

        Assert.Catch<ArgumentException>(() => stream.Read(new byte[2], offset, count));
    }

    [Test]
    public async Task ReadAsync_NullBuffer_ThrowsBeforeCallingClient() {
        var stream = CreateStream(canRead: true, canWrite: false);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => {
                await stream.ReadAsync(null!, 0, 0, CancellationToken.None);
            });
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public async Task ReadAsync_NegativeBufferRange_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: true, canWrite: false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => {
                await stream.ReadAsync(new byte[2], offset, count, CancellationToken.None);
            });
    }

    [TestCase(3, 0)]
    [TestCase(1, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public async Task ReadAsync_BufferRangeOutsideArray_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: true, canWrite: false);

        await Assert.CatchAsync<ArgumentException>(
            async () => {
                await stream.ReadAsync(new byte[2], offset, count, CancellationToken.None);
            });
    }

    [Test]
    public void Write_NullBuffer_ThrowsBeforeCallingClient() {
        var stream = CreateStream(canRead: false, canWrite: true);

        Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 0));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public void Write_NegativeBufferRange_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: false, canWrite: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(new byte[2], offset, count));
    }

    [TestCase(3, 0)]
    [TestCase(1, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void Write_BufferRangeOutsideArray_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: false, canWrite: true);

        Assert.Catch<ArgumentException>(() => stream.Write(new byte[2], offset, count));
    }

    [Test]
    public async Task WriteAsync_NullBuffer_ThrowsBeforeCallingClient() {
        var stream = CreateStream(canRead: false, canWrite: true);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => {
                await stream.WriteAsync(null!, 0, 0, CancellationToken.None);
            });
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public async Task WriteAsync_NegativeBufferRange_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: false, canWrite: true);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => {
                await stream.WriteAsync(new byte[2], offset, count, CancellationToken.None);
            });
    }

    [TestCase(3, 0)]
    [TestCase(1, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public async Task WriteAsync_BufferRangeOutsideArray_ThrowsBeforeCallingClient(int offset, int count) {
        var stream = CreateStream(canRead: false, canWrite: true);

        await Assert.CatchAsync<ArgumentException>(
            async () => {
                await stream.WriteAsync(new byte[2], offset, count, CancellationToken.None);
            });
    }

    [Test]
    public void Read_ZeroCountAtEndOfBuffer_ReturnsZeroWithoutCallingClient() {
        var stream = CreateStream(canRead: true, canWrite: false);
        var buffer = new byte[2];

        Assert.That(stream.Read(buffer, buffer.Length, 0), Is.EqualTo(0));
    }

    [Test]
    public async Task ReadAsync_ZeroCountAtEndOfBuffer_ReturnsZeroWithoutCallingClient() {
        var stream = CreateStream(canRead: true, canWrite: false);
        var buffer = new byte[2];

        Assert.That(await stream.ReadAsync(buffer, buffer.Length, 0, CancellationToken.None), Is.EqualTo(0));
    }

    [Test]
    public void Write_ZeroCountAtEndOfBuffer_ReturnsWithoutCallingClient() {
        var stream = CreateStream(canRead: false, canWrite: true);
        var buffer = new byte[2];

        Assert.DoesNotThrow(() => stream.Write(buffer, buffer.Length, 0));
    }

    [Test]
    public async Task WriteAsync_ZeroCountAtEndOfBuffer_ReturnsWithoutCallingClient() {
        var stream = CreateStream(canRead: false, canWrite: true);
        var buffer = new byte[2];

        await Assert.DoesNotThrowAsync(
            async () => {
                await stream.WriteAsync(buffer, buffer.Length, 0, CancellationToken.None);
            });
    }
}

#pragma warning restore CA2022
