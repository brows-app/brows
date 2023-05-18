using Brows.Exports;
using System;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class NativeDetail : FileSystemCommand<NativeDetailParameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasSourceFileSystemInfo(out _, out var list)) return false;
            var service = DetailFileSystemInfos;
            if (service == null) {
                return false;
            }
            return context.Operate(async (progress, cancellationToken) => {
                return await service.Work(list, progress, cancellationToken);
            });
        }

        public IDetailFileSystemInfos DetailFileSystemInfos { get; set; }
    }
}
