using System;

namespace Brows.SSH {
    public abstract class SSHEntryInfo {
        public abstract long? Length { get; }
        public abstract string Name { get; }
        public abstract string Path { get; }
        public abstract string Extension { get; }
        public abstract string OwnedByUser { get; }
        public abstract string OwnedByGroup { get; }
        public abstract string LinkTarget { get; }
        public abstract string Permissions { get; }
        public abstract SSHEntryKind Kind { get; }
        public virtual DateTime? LastAccessTime { get; }
        public virtual DateTime? LastAccessTimeUtc { get; }
        public virtual DateTime? LastWriteTime { get; }
        public virtual DateTime? LastWriteTimeUtc { get; }
        public virtual DateTime? CreationTime { get; }
        public virtual DateTime? CreationTimeUtc { get; }
    }
}
