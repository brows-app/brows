using System.ComponentModel;

namespace Brows.Entries;

public interface IEntryStreamGui : INotifyPropertyChanged {
    string View { get; }
    IEntryStreamSource Source { get; }
    IEntryStreamGuiState State { get; }
    IEntryStreamGuiOptions Options { get; }
}
