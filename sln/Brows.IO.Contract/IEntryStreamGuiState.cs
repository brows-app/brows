using System.ComponentModel;

namespace Brows {
    public interface IEntryStreamGuiState : INotifyPropertyChanged {
        IEntryStreamGuiView Text { get; }
        IEntryStreamGuiView Image { get; }
        IEntryStreamGuiView Media { get; }
        IEntryStreamGuiView Preview { get; }
    }
}
