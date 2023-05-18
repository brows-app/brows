using Brows.Exports;
using System.Collections.Generic;

namespace Brows.Commands {
    public abstract class WorkIO<TService> : Command<WorkIOParameter> where TService : class, IWorkProvidedIO, IProviderExport {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasSource(out var source)) return false;
            if (false == context.HasParameter(out var parameter)) return false;
            if (false == context.HasPanels(parameter.Panel(), out var active, out var passive)) return false;
            if (false == active.HasProviderService(out var activeProvider, out IProvideIO provideIO)) return false;
            if (false == passive.HasProviderService(out var passiveProvider, out TService workIO)) return false;
            return context.Operate(async (progress, token) => {
                var io = new List<IProvidedIO>();
                var ioProvided = await provideIO.Work(io, source, activeProvider, progress, token);
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
