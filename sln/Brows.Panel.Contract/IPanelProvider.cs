using System.Collections;
using System.Collections.Specialized;

namespace Brows {
    using Gui;

    public interface IPanelProvider {
        Image Icon { get; }
        IPanelID PanelID { get; }
        IBookmark Bookmark { get; }
        string DataKeyLookup(string alias);
        IEnumerable DataKeyLookup(params string[] alias);
        NameValueCollection DataKeyAlias();
    }
}
