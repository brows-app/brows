using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Gui;

    public interface IPanelProvider {
        Image Icon { get; }
        IPanelID PanelID { get; }
        IBookmark Bookmark { get; }
        string DataKeyLookup(string alias);
        IReadOnlySet<string> DataKeyPossible(string part);
        IReadOnlyDictionary<string, IReadOnlySet<string>> DataKeyAlias();
        ValueTask<bool> CaseSensitive(CancellationToken cancellationToken);
    }
}
