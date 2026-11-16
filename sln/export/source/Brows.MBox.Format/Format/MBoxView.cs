using System;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Threading;

namespace Brows.MBox.Format;

/// <summary>
/// A read-only view of a mailbox, with its own cursor.
/// </summary>
/// <remarks>
/// The view reports the offsets of the file it is built on, so the records that are read through
/// it keep the coordinates of the mailbox. Every read is made at an explicit offset, so the views
/// of two readers never share a position.
/// </remarks>
internal sealed class MBoxView : Stream {
    private readonly SafeFileHandle _Handle;
    private long _Position;

    /// <summary>
    /// Gets or sets the absolute offset, from the start of the file, at which the view stops.
    /// </summary>
    public long Limit { get; set; } =
        long.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxView"/> class.
    /// </summary>
    /// <param name="handle">
    /// The handle of the file the view reads from.
    /// </param>
    public MBoxView(SafeFileHandle handle) {
        _Handle = handle ?? throw new ArgumentNullException(nameof(handle));
    }

    /// <summary>
    /// Gets the absolute offset, from the start of the file, of the next byte to be read.
    /// </summary>
    public override long Position {
        get => _Position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>
    /// Gets the length of the file.
    /// </summary>
    public override long Length =>
        RandomAccess.GetLength(_Handle);

    /// <summary>
    /// Gets a value indicating whether the view can be read.
    /// </summary>
    public override bool CanRead =>
        true;

    /// <summary>
    /// Gets a value indicating whether the view can be written to, which it cannot.
    /// </summary>
    public override bool CanWrite =>
        false;

    /// <summary>
    /// Gets a value indicating whether the view can seek.
    /// </summary>
    public override bool CanSeek =>
        true;

    /// <summary>
    /// Reads bytes from the view.
    /// </summary>
    /// <param name="buffer">
    /// The buffer that receives the bytes.
    /// </param>
    /// <param name="offset">
    /// The offset of the first byte to write.
    /// </param>
    /// <param name="count">
    /// The number of bytes to read.
    /// </param>
    /// <returns>
    /// The number of bytes that were read.
    /// </returns>
    public override int Read(byte[] buffer, int offset, int count) {
        if (buffer is null) {
            throw new ArgumentNullException(nameof(buffer));
        }
        if (offset < 0 || count < 0 || offset + count > buffer.Length) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        if (count == 0) {
            return 0;
        }
        if (_Position >= Limit) {
            return 0;
        }
        var room = Limit - _Position;
        if (room < count) {
            count = (int)room;
        }
        var read = RandomAccess.Read(_Handle, buffer.AsSpan(offset, count), _Position);
        _Position += read;
        return read;
    }

    /// <summary>
    /// Moves the view to the specified absolute offset.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset to move to.
    /// </param>
    /// <param name="origin">
    /// The origin the offset is relative to. Only <see cref="SeekOrigin.Begin"/> is supported.
    /// </param>
    /// <returns>
    /// The new position of the view.
    /// </returns>
    public override long Seek(long offset, SeekOrigin origin) {
        if (origin != SeekOrigin.Begin) {
            throw new ArgumentException("Only an absolute offset is supported.", nameof(origin));
        }
        if (offset < 0) {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        _Position = offset;
        return _Position;
    }

    /// <summary>
    /// Sets the position of the view.
    /// </summary>
    /// <param name="position">
    /// The new position of the view.
    /// </param>
    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Writes bytes to the view, which it does not support.
    /// </summary>
    /// <param name="buffer">
    /// The buffer that holds the bytes.
    /// </param>
    /// <param name="offset">
    /// The offset of the first byte to write.
    /// </param>
    /// <param name="count">
    /// The number of bytes to write.
    /// </param>
    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Flushes the view, which has nothing to flush.
    /// </summary>
    public override void Flush() {
    }
}
