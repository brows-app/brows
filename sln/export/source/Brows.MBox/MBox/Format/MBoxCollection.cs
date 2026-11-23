using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Brows.MBox.Format;

/// <summary>
/// Creates read-only collections that own their contents.
/// </summary>
internal static class MBoxCollection {
    /// <summary>
    /// Returns a read-only collection that owns a copy of <paramref name="items"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the items in the collection.
    /// </typeparam>
    /// <param name="items">
    /// The items to copy. May be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A read-only collection that owns a copy of <paramref name="items"/>.
    /// </returns>
    internal static IReadOnlyList<T> Own<T>(IReadOnlyList<T> items) {
        if (items is null || items.Count == 0) {
            return Array.Empty<T>();
        }
        var copy = new List<T>(items.Count);
        for (var i = 0; i < items.Count; i++) {
            copy.Add(items[i]);
        }
        return new ReadOnlyCollection<T>(copy);
    }

    /// <summary>
    /// Returns a read-only dictionary that owns a copy of <paramref name="items"/>.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the keys in the dictionary.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of the values in the dictionary.
    /// </typeparam>
    /// <param name="items">
    /// The items to copy. May be <see langword="null"/>.
    /// </param>
    /// <param name="comparer">
    /// The comparer used to compare keys.
    /// </param>
    /// <returns>
    /// A read-only dictionary that owns a copy of <paramref name="items"/>.
    /// </returns>
    internal static IReadOnlyDictionary<TKey, TValue>
        Own<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> items,
                          IEqualityComparer<TKey> comparer) {
        if (items is null || items.Count == 0) {
            return new ReadOnlyDictionary<TKey, TValue>(
                new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default));
        }
        var copy = new Dictionary<TKey, TValue>(items.Count, comparer ?? EqualityComparer<TKey>.Default);
        foreach (var pair in items) {
            copy[pair.Key] = pair.Value;
        }
        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }
}
