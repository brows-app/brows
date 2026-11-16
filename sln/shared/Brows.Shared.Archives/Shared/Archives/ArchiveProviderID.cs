using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Brows.Shared.Archives;

internal sealed class ArchiveProviderID : IEquatable<ArchiveProviderID> {
    private readonly ReadOnlyCollection<string> _NestedMembers;

    internal ArchiveProviderIDFormat Format { get; }

    internal string FileName { get; }

    internal IReadOnlyList<string> NestedMembers => _NestedMembers;

    internal string Directory { get; }

    internal ArchiveProviderID(
        ArchiveProviderIDFormat format,
        string fileName,
        IEnumerable<string> nestedMembers,
        string directory) {
        Format = format;
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        if (FileName.Length == 0) {
            throw new ArgumentException("The archive file name cannot be empty.", nameof(fileName));
        }
        if (nestedMembers is null) {
            throw new ArgumentNullException(nameof(nestedMembers));
        }
        if (directory is null) {
            throw new ArgumentNullException(nameof(directory));
        }

        var copiedNestedMembers = nestedMembers.ToArray();
        var hasEmptyNestedMember = copiedNestedMembers.Any(member => string.IsNullOrEmpty(member));
        if (hasEmptyNestedMember) {
            throw new ArgumentException("Nested archive member names cannot be empty.", nameof(nestedMembers));
        }

        _NestedMembers = Array.AsReadOnly(copiedNestedMembers);
        Directory = directory;
    }

    internal ArchiveProviderID WithNestedMember(string nestedMember) {
        if (nestedMember is null) {
            throw new ArgumentNullException(nameof(nestedMember));
        }

        return new ArchiveProviderID(Format, FileName, _NestedMembers.Append(nestedMember), "");
    }

    internal ArchiveProviderID WithDirectory(string directory) {
        return new ArchiveProviderID(Format, FileName, _NestedMembers, directory);
    }

    public bool Equals(ArchiveProviderID other) {
        if (other is null) {
            return false;
        }

        var sameHeader = Format == other.Format
            && string.Equals(FileName, other.FileName, StringComparison.Ordinal)
            && string.Equals(Directory, other.Directory, StringComparison.Ordinal);
        if (sameHeader == false || _NestedMembers.Count != other._NestedMembers.Count) {
            return false;
        }

        for (var i = 0; i < _NestedMembers.Count; i++) {
            var sameMember = string.Equals(_NestedMembers[i], other._NestedMembers[i], StringComparison.Ordinal);
            if (sameMember == false) {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object other) {
        return Equals(other as ArchiveProviderID);
    }

    public override int GetHashCode() {
        var hashCode = new HashCode();
        hashCode.Add(Format);
        hashCode.Add(FileName, StringComparer.Ordinal);
        foreach (var nestedMember in _NestedMembers) {
            hashCode.Add(nestedMember, StringComparer.Ordinal);
        }
        hashCode.Add(Directory, StringComparer.Ordinal);
        return hashCode.ToHashCode();
    }
}
