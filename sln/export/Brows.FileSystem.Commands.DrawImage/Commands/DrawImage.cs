using Domore.Threading;
using System;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class DrawImage : FileSystemCommand<DrawImageParameter> {
        private readonly STAThreadPool ThreadPool = new(nameof(DrawImage)) { WorkerCountMin = 0 };

        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasParameter(out var parameter)) return false;
            if (false == active.HasFileSystemRename(out var rename)) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var work = new DrawImageWork(parameter, rename, ThreadPool);
                return await work.Run(progress, token);
            });
        }
    }
}
