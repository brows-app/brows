using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryProvider : IPanelProvider {
        string ParentID { get; }
        string Directory { get; }
        IReadOnlySet<string> DataKeyDefault { get; }
        IReadOnlySet<string> DataKeyOptions { get; }
        IReadOnlyDictionary<string, IEntryColumn> DataKeyColumns { get; }
        IReadOnlyDictionary<string, EntrySortDirection?> DataKeySorting { get; }
        IOperator Operator(IOperatorDeployment deployment);
        string CreatedID(string createdName);
        void Begin(IEntryProviderTarget target);
        void End();
        void Refresh();
        Task Init(CancellationToken cancellationToken);
    }
}
