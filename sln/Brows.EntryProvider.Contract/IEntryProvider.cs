using System.Collections.Generic;

namespace Brows {
    public interface IEntryProvider : IPanelProvider {
        string ParentID { get; }
        IReadOnlySet<string> DataKeyDefaults { get; }
        IReadOnlySet<string> DataKeyOptions { get; }
        IReadOnlyDictionary<string, IEntryDataConverter> DataKeyConverters { get; }
        IComponentResourceKey DataKeyResolver { get; }
        IOperator Operator(IOperatorDeployment deployment);
        string CreatedID(string createdName);
        void Begin(IEntryProviderTarget target);
        void End();
        void Refresh();
    }
}
