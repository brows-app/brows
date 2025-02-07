using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IProvider : IDisposable {
        string ID { get; }
        string Parent { get; }
        IEntryDataDefinitionSet Data { get; }
        IEntryObservation Observation { get; }
        void Begin();
        void End();
        Task Refresh(CancellationToken token);
        Task<bool> Take(IMessage message, CancellationToken token);
        Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token);
        TExport Import<TExport>() where TExport : IProviderExport;
    }
}
