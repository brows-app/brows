using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Threading;

namespace Brows.MBox.Format;

/// <summary>
/// Opens a mailbox file and reads its records and messages.
/// </summary>
/// <remarks>
/// The file is opened for reading only, and the sharing mode denies writes, deletions, and
/// replacements, so the mailbox cannot change while it is indexed. Every operation takes a read
/// lease, and the file handle is released once the last lease is finished. A file that is
/// currently being written to cannot be opened, and the failure is reported to the caller.
/// </remarks>
public sealed class MBoxFile : IDisposable {
    private readonly MBoxParserOptions _Options;
    private readonly MBoxParser _Parser;
    private readonly SafeFileHandle _Handle;
    private readonly Lock _Locker = new();
    private readonly List<Lease> _Leases = [];
    private bool _Disposed;

    /// <summary>
    /// Gets the canonical path of the mailbox.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the length of the mailbox, in bytes.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the options the file is read with.
    /// </summary>
    public MBoxParserOptions Options =>
        _Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxFile"/> class.
    /// </summary>
    /// <param name="path">
    /// The path of the mailbox.
    /// </param>
    /// <param name="options">
    /// The options that control how the mailbox is read. The options are snapshotted.
    /// </param>
    public MBoxFile(string path, MBoxParserOptions options) {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("The path of the mailbox is not valid.", nameof(path));
        }
        _Options = (options ?? throw new ArgumentNullException(nameof(options))) with { };
        _Parser = new MBoxParser(_Options);
        var fullPath = System.IO.Path.GetFullPath(path);
        _Handle = File.OpenHandle(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.None);
        Path = fullPath;
        Length = RandomAccess.GetLength(_Handle);
    }

    /// <summary>
    /// Scans the mailbox and returns the records it contains.
    /// </summary>
    /// <param name="diagnostic">
    /// The callback that receives the problems found while scanning. May be <see langword="null"/>.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the scan.
    /// </param>
    /// <returns>
    /// The records of the mailbox, in the order they are stored.
    /// </returns>
    public IEnumerable<MBoxRecord> Scan(Action<MBoxDiagnostic> diagnostic, CancellationToken token) {
        var lease = Acquire(token);
        try {
            foreach (var record in _Parser.Scan(lease.View, diagnostic, token)) {
                yield return record;
            }
        }
        finally {
            lease.Dispose();
        }
    }

    /// <summary>
    /// Reads the header fields of a record, without reading its body.
    /// </summary>
    /// <param name="record">
    /// The record to read.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// The message of the record, with a body that was not read.
    /// </returns>
    public MBoxMessage ReadHeaders(MBoxRecord record, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(record);
        var lease = Acquire(token);
        try {
            return _Parser.ReadHeaders(lease.View, record, token);
        }
        finally {
            lease.Dispose();
        }
    }

    /// <summary>
    /// Reads a record, including a bounded preview of its body.
    /// </summary>
    /// <param name="record">
    /// The record to read.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// The message of the record.
    /// </returns>
    public MBoxMessage ReadMessage(MBoxRecord record, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(record);
        var lease = Acquire(token);
        try {
            return _Parser.ReadMessage(lease.View, record, token);
        }
        finally {
            lease.Dispose();
        }
    }

    /// <summary>
    /// Extracts a record as an Internet message, without altering its bytes.
    /// </summary>
    /// <param name="record">
    /// The record to extract.
    /// </param>
    /// <param name="destination">
    /// The stream that receives the message. The stream is left open.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the extraction.
    /// </param>
    public void ExtractMessage(MBoxRecord record, Stream destination, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(destination);
        var lease = Acquire(token);
        try {
            _Parser.ExtractMessage(lease.View, record, destination, token);
        }
        finally {
            lease.Dispose();
        }
    }

    /// <summary>
    /// Releases the mailbox and its file handle.
    /// </summary>
    public void Dispose() {
        bool release;
        lock (_Locker) {
            if (_Disposed) {
                return;
            }
            _Disposed = true;
            release = _Leases.Count == 0;
        }
        /*
         * The leases that are still running keep the handle alive, so the handle is released
         * only after the last of them is finished.
         */
        if (release) {
            _Handle.Dispose();
        }
    }

    private Lease Acquire(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        lock (_Locker) {
            if (_Disposed) {
                throw new ObjectDisposedException(nameof(MBoxFile));
            }
            var lease = new Lease(this, new MBoxView(_Handle));
            _Leases.Add(lease);
            return lease;
        }
    }

    private void Release(Lease lease) {
        bool release = false;
        lock (_Locker) {
            _Leases.Remove(lease);
            release = _Disposed && _Leases.Count == 0;
        }
        if (release) {
            _Handle.Dispose();
        }
    }

    /// <summary>
    /// Holds a read lease on the mailbox.
    /// </summary>
    private sealed class Lease : IDisposable {
        private readonly MBoxFile _Owner;
        private bool _Disposed;

        public Lease(MBoxFile owner, MBoxView view) {
            _Owner = owner;
            View = view;
        }

        public MBoxView View { get; }

        public void Dispose() {
            if (_Disposed) {
                return;
            }
            _Disposed = true;
            _Owner.Release(this);
        }
    }
}
