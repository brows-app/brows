using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Brows;

/// <summary>
/// Holds the previews of the messages of a mailbox, and bounds the memory they use.
/// </summary>
/// <remarks>
/// The cache is a least-recently-used cache that evicts by the number of bytes and by the number
/// of entries. A preview that is too large to be admitted is returned without being retained.
/// </remarks>
internal sealed class MBoxPreviewCache {
    private readonly Lock _Locker = new();
    private readonly Dictionary<string, MBoxPreviewResult> _Cache = [];
    private readonly LinkedList<string> _Order = [];
    private long _Bytes;

    /// <summary>
    /// Gets the maximum number of bytes of preview text that are retained.
    /// </summary>
    public long MaxBytes {
        get;
    }

    /// <summary>
    /// Gets the maximum number of completed previews that are retained.
    /// </summary>
    public int MaxCount {
        get;
    }

    /// <summary>
    /// Gets the number of bytes of preview text that are retained.
    /// </summary>
    public long Bytes {
        get {
            lock (_Locker) {
                return _Bytes;
            }
        }
    }

    /// <summary>
    /// Gets the number of completed previews that are retained.
    /// </summary>
    public int Count {
        get {
            lock (_Locker) {
                return _Cache.Count;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxPreviewCache"/> class.
    /// </summary>
    /// <param name="maxBytes">
    /// The maximum number of bytes of preview text that are retained.
    /// </param>
    /// <param name="maxCount">
    /// The maximum number of completed previews that are retained.
    /// </param>
    public MBoxPreviewCache(long maxBytes, int maxCount) {
        MaxBytes = maxBytes;
        MaxCount = maxCount;
    }

    /// <summary>
    /// Returns the preview of the specified key, or <see langword="null"/> when it is not cached.
    /// </summary>
    /// <param name="key">
    /// The key of the preview.
    /// </param>
    /// <returns>
    /// The preview of the specified key, or <see langword="null"/>.
    /// </returns>
    public MBoxPreviewResult Get(string key) {
        if (key is null) {
            return null;
        }
        lock (_Locker) {
            if (_Cache.TryGetValue(key, out var result) == false) {
                return null;
            }
            Touch(key);
            return result;
        }
    }

    /// <summary>
    /// Adds a preview to the cache, and evicts the least recently used previews when the cache
    /// is full.
    /// </summary>
    /// <param name="key">
    /// The key of the preview.
    /// </param>
    /// <param name="result">
    /// The preview to add.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the preview was retained.
    /// </returns>
    public bool Add(string key, MBoxPreviewResult result) {
        if (key is null || result is null) {
            return false;
        }
        var size = result.Size;
        if (size > MaxBytes) {
            /*
             * A preview that is larger than the whole cache is returned without being retained.
             */
            return false;
        }
        lock (_Locker) {
            Remove(key);
            _Cache[key] = result;
            _Order.AddLast(key);
            _Bytes += size;
            while (_Bytes > MaxBytes || _Cache.Count > MaxCount) {
                var oldest = _Order.First;
                if (oldest == null) {
                    break;
                }
                Remove(oldest.Value);
            }
            return true;
        }
    }

    /// <summary>
    /// Removes every preview from the cache.
    /// </summary>
    public void Clear() {
        lock (_Locker) {
            _Cache.Clear();
            _Order.Clear();
            _Bytes = 0;
        }
    }

    /// <summary>
    /// Removes the previews of the specified keys from the cache.
    /// </summary>
    /// <param name="keys">
    /// The keys of the previews to remove.
    /// </param>
    public void Remove(IEnumerable<string> keys) {
        if (keys == null) {
            return;
        }
        lock (_Locker) {
            foreach (var key in keys) {
                Remove(key);
            }
        }
    }

    private void Touch(string key) {
        var node = _Order.Find(key);
        if (node != null) {
            _Order.Remove(node);
            _Order.AddLast(node.Value);
        }
    }

    private void Remove(string key) {
        if (_Cache.Remove(key, out var result)) {
            _Bytes -= result.Size;
        }
        var node = _Order.Find(key);
        if (node != null) {
            _Order.Remove(node);
        }
    }
}
