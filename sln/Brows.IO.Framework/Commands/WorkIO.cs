using System.Collections.Generic;

namespace Brows.Commands {
    using Exports;

    public abstract class WorkIO<TService> : Command<WorkIOParameter> where TService : class, IWorkProvidedIO, IProviderExport {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasParameter(out var parameter) == false) return false;
            if (context.HasPanels(parameter.Panel(), out var active, out var passive) == false) {
                return false;
            }
            if (active.HasProviderService(out var activeProvider, out IProvideIO provideIO) == false) {
                return false;
            }
            if (passive.HasProviderService(out var passiveProvider, out TService workIO) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var io = new List<IProvidedIO>();
                var ioProvided = await provideIO.Work(io, activeProvider, progress, token);
                if (ioProvided) {
                    var copied = await workIO.Work(io, passiveProvider, progress, token);
                    if (copied) {
                        return true;
                    }
                }
                return false;
            });
        }
    }
}
