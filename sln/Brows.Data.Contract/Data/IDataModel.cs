using System.ComponentModel;

namespace Brows.Data {
    public interface IDataModel : INotifyPropertyChanged {
        IDataStore Store { get; }
    }
}
