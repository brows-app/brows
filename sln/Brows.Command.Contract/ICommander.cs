using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface ICommander {
        IClipboard Clipboard { get; }
        IPanelCollection Panels { get; }
        ICommandCollection Commands { get; }
        ICommandPalette Palette { get; }
        IOperationCollection Operations { get; }

        Task<bool> AddPanel(string id, CancellationToken cancellationToken);
        Task<bool> RemovePanel(IPanel panel, CancellationToken cancellationToken);
        Task<bool> ShowPalette(string input, CancellationToken cancellationToken);
        Task<bool> ShowPalette(string input, int selectedStart, int selectedLength, CancellationToken cancellationToken);
        Task<bool> SetTheme(string @base, string background, string foreground, CancellationToken cancellationToken);
        void Close();
        void Exit();
    }
}
