using System;
using System.Runtime.CompilerServices;

namespace Brows.SFTP.Native;

[TestFixture]
internal sealed class SftpFileTest {
    private static T CreateUninitialized<T>() where T : class {
        var instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        GC.SuppressFinalize(instance);
        return instance;
    }

    [Test]
    public void Read_NullBuffer_ThrowsBeforeCreatingNativeHandle() {
        var file = CreateUninitialized<SftpFile>();

        Assert.Throws<ArgumentNullException>(() => file.Read(null!, 0, 0, CancellationToken.None));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public void Read_NegativeBufferRange_ThrowsBeforeCreatingNativeHandle(int offset, int count) {
        var file = CreateUninitialized<SftpFile>();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => file.Read(new byte[2], offset, count, CancellationToken.None));
    }

    [TestCase(3, 0)]
    [TestCase(1, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void Read_BufferRangeOutsideArray_ThrowsBeforeCreatingNativeHandle(int offset, int count) {
        var file = CreateUninitialized<SftpFile>();

        Assert.Throws<ArgumentException>(
            () => file.Read(new byte[2], offset, count, CancellationToken.None));
    }

    [Test]
    public void Write_NullBuffer_ThrowsBeforeCreatingNativeHandle() {
        var file = CreateUninitialized<SftpFile>();

        Assert.Throws<ArgumentNullException>(() => file.Write(null!, 0, 0, CancellationToken.None));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public void Write_NegativeBufferRange_ThrowsBeforeCreatingNativeHandle(int offset, int count) {
        var file = CreateUninitialized<SftpFile>();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => file.Write(new byte[2], offset, count, CancellationToken.None));
    }

    [TestCase(3, 0)]
    [TestCase(1, 2)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void Write_BufferRangeOutsideArray_ThrowsBeforeCreatingNativeHandle(int offset, int count) {
        var file = CreateUninitialized<SftpFile>();

        Assert.Throws<ArgumentException>(
            () => file.Write(new byte[2], offset, count, CancellationToken.None));
    }

    [Test]
    public void Read_ZeroCountAtEndOfBuffer_ReturnsZeroWithoutCreatingNativeHandle() {
        var file = CreateUninitialized<SftpFile>();
        var buffer = new byte[2];

        Assert.That(file.Read(buffer, buffer.Length, 0, CancellationToken.None), Is.EqualTo(0));
    }

    [Test]
    public void Write_ZeroCountAtEndOfBuffer_ReturnsZeroWithoutCreatingNativeHandle() {
        var file = CreateUninitialized<SftpFile>();
        var buffer = new byte[2];

        Assert.That(file.Write(buffer, buffer.Length, 0, CancellationToken.None), Is.EqualTo(0));
    }

}
