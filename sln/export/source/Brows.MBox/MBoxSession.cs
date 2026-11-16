using Brows.MBox.Format;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows;

/// <summary>
/// Holds an open mailbox, its index, and the identity of the generation of the data that it
/// holds.
/// </summary>
/// <remarks>
/// A session owns the file handle of the mailbox, and it is the only object that reads the
/// mailbox. A session is never reopened by path, so that a read always uses the bytes that
/// were indexed. A retired session accepts no further read, and it releases its handle once
/// its last lease is finished.
/// </remarks>
internal sealed class MBoxSession : IDisposable {
    private readonly Lock _Locker = new();
    private readonly MBoxFile _File;
    private readonly List<MBoxRecord> _Records = [];
    private int _LeaseCount;
    private bool _Retired;
    private bool _Disposed;

    internal sealed class Lease : IDisposable {
        private MBoxSession _Session;

        internal Lease(MBoxSession session) {
            _Session = session;
        }

        public void Dispose() {
            var session = Interlocked.Exchange(ref _Session, null);
            session?.ReleaseLease();
        }
    }

    /// <summary>
    /// Gets the identifier of the generation of the data of the session.
    /// </summary>
    public string GenerationId {
        get;
    }

    /// <summary>
    /// Gets the file the session reads.
    /// </summary>
    public MBoxFile File =>
        _File;

    /// <summary>
    /// Gets the options the session reads the mailbox with.
    /// </summary>
    public MBoxParserOptions Options {
        get;
    }

    /// <summary>
    /// Gets the records of the mailbox, in the order they are stored.
    /// </summary>
    public IReadOnlyList<MBoxRecord> Records {
        get {
            lock (_Locker) {
                return _Records.ToArray();
            }
        }
    }

    /// <summary>
    /// Gets whether the session was retired, so that it accepts no further read.
    /// </summary>
    public bool IsRetired {
        get {
            lock (_Locker) {
                return _Retired;
            }
        }
    }

    internal Lease TryAcquireLease() {
        lock (_Locker) {
            var sessionIsUnavailable = _Retired || _Disposed;
            if (sessionIsUnavailable) {
                return null;
            }
            _LeaseCount++;
            return new Lease(this);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxSession"/> class.
    /// </summary>
    /// <param name="file">
    /// The file the session reads.
    /// </param>
    /// <param name="options">
    /// The options the session reads the mailbox with.
    /// </param>
    public MBoxSession(MBoxFile file, MBoxParserOptions options) {
        _File = file ?? throw new ArgumentNullException(nameof(file));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        GenerationId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Adds a record to the index of the session.
    /// </summary>
    /// <param name="record">
    /// The record to add.
    /// </param>
    public void Add(MBoxRecord record) {
        ArgumentNullException.ThrowIfNull(record);
        lock (_Locker) {
            if (_Retired) {
                return;
            }
            _Records.Add(record);
        }
    }

    /// <summary>
    /// Returns the record that is stored at the specified offset, or <see langword="null"/> when
    /// there is none.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset of the record.
    /// </param>
    /// <returns>
    /// The record that is stored at the specified offset, or <see langword="null"/>.
    /// </returns>
    public MBoxRecord Find(long offset) {
        lock (_Locker) {
            foreach (var record in _Records) {
                if (record.Offset == offset) {
                    return record;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Retires the session, so that it accepts no further read.
    /// </summary>
    public void Retire() {
        MBoxFile file = null;
        lock (_Locker) {
            _Retired = true;
            var shouldDisposeFile = _LeaseCount == 0 && _Disposed == false;
            if (shouldDisposeFile) {
                _Disposed = true;
                file = _File;
            }
        }
        file?.Dispose();
    }

    /// <summary>
    /// Releases the session and the file it holds.
    /// </summary>
    public void Dispose() {
        Retire();
    }

    private void ReleaseLease() {
        MBoxFile file = null;
        lock (_Locker) {
            if (_LeaseCount > 0) {
                _LeaseCount--;
            }
            var canDisposeFile = _Retired && _LeaseCount == 0 && _Disposed == false;
            if (canDisposeFile) {
                _Disposed = true;
                file = _File;
            }
        }
        file?.Dispose();
    }
}
