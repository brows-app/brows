using Domore.IO;
using System;
using System.IO;

namespace Brows {
    internal sealed class FileSystemOperationProgress : FileSystemProgress {
        public IOperationProgress Agent { get; }

        public FileSystemOperationProgress(IOperationProgress agent) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        public sealed override void AddToTarget(long value) {
            Agent.Target.Add(value);
        }

        public sealed override void AddToProgress(long value) {
            Agent.Add(value);
        }

        public sealed override void SetCurrentInfo(FileSystemInfo value) {
            Agent.Info.Data(value?.Name);
        }
    }
}
