using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.MBox.Format;

/// <summary>
/// Holds the header fields of a message, in the order they were stored.
/// </summary>
internal sealed class MBoxHeaders {
    private readonly IReadOnlyList<MBoxHeader> _Items;
    private readonly IReadOnlyDictionary<string, MBoxHeader> _First;
    private readonly IReadOnlyList<string> _Names;

    /// <summary>
    /// Gets the header fields, in the order they were stored.
    /// </summary>
    internal IReadOnlyList<MBoxHeader> Items =>
        _Items;

    /// <summary>
    /// Gets the date of the message, or <see langword="null"/> when the message has no valid
    /// <c>Date</c> header field.
    /// </summary>
    /// <remarks>
    /// The date is read with an invariant parser that never falls back to a culture-dependent
    /// one, and that never substitutes the current time for a missing time zone.
    /// </remarks>
    internal DateTimeOffset? Date {
        get {
            var header = Get("Date");
            if (header == null) {
                return null;
            }
            return MBoxDate.TryParseHeader(header.RawValue, out var date)
                ? date
                : null;
        }
    }

    /// <summary>
    /// Gets the names of the header fields, in the order they were stored.
    /// </summary>
    internal IReadOnlyList<string> Names =>
        _Names;

    /// <summary>
    /// Gets the first header field with the specified name, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="name">
    /// The name of the header field. The comparison is case-insensitive.
    /// </param>
    /// <returns>
    /// The first header field with the specified name, or <see langword="null"/>.
    /// </returns>
    internal MBoxHeader this[string name] {
        get {
            if (name is null) {
                return null;
            }
            return _First.TryGetValue(name, out var header) ? header : null;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxHeaders"/> class.
    /// </summary>
    /// <param name="items">
    /// The header fields, in the order they were stored.
    /// </param>
    internal MBoxHeaders(IReadOnlyList<MBoxHeader> items) {
        _Items = MBoxCollection.Own(items);
        var first = new Dictionary<string, MBoxHeader>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>(_Items.Count);
        for (var i = 0; i < _Items.Count; i++) {
            var header = _Items[i];
            names.Add(header.Name);
            if (first.ContainsKey(header.Name) == false) {
                first.Add(header.Name, header);
            }
        }
        _First = new System.Collections.ObjectModel.ReadOnlyDictionary<string, MBoxHeader>(first);
        _Names = new System.Collections.ObjectModel.ReadOnlyCollection<string>(names);
    }

    /// <summary>
    /// Returns the first header field with the specified name, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="name">
    /// The name of the header field. The comparison is case-insensitive.
    /// </param>
    /// <returns>
    /// The first header field with the specified name, or <see langword="null"/>.
    /// </returns>
    internal MBoxHeader Get(string name) {
        return this[name];
    }

    /// <summary>
    /// Returns every header field with the specified name, in the order they were stored.
    /// </summary>
    /// <param name="name">
    /// The name of the header field. The comparison is case-insensitive.
    /// </param>
    /// <returns>
    /// Every header field with the specified name. The collection is empty when there is none.
    /// </returns>
    internal IReadOnlyList<MBoxHeader> GetAll(string name) {
        if (name is null) {
            return Array.Empty<MBoxHeader>();
        }
        var matches = _Items
            .Where(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return MBoxCollection.Own(matches);
    }

    /// <summary>
    /// Returns whether a header field with the specified name exists.
    /// </summary>
    /// <param name="name">
    /// The name of the header field. The comparison is case-insensitive.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a header field with the specified name exists.
    /// </returns>
    internal bool Contains(string name) {
        if (name is null) {
            return false;
        }
        return _First.ContainsKey(name);
    }
}
