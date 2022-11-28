using System.Collections;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Gui;

    public interface IPanelProvider {
        Image Icon { get; }
        IPanelID PanelID { get; }
        IBookmark Bookmark { get; }
        string DataKeyLookup(string alias);
        IEnumerable DataKeyLookup(params string[] alias);
        NameValueCollection DataKeyAlias();
        Task<bool> CaseSensitive(CancellationToken cancellationToken);
    }
}
