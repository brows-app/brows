using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryProvider : IDisposable {
        string ID { get; }
        string Parent { get; }
        IEntryDataDefinitionSet Data { get; }
        IEntryObservation Observation { get; }
        void Begin();
        void End();
        void Refresh();
        Task<bool> Drop(IPanelDrop data, IOperationProgress operationProgress, CancellationToken cancellationToken);
        TExport Import<TExport>() where TExport : IEntryProviderExport;
    }
}
