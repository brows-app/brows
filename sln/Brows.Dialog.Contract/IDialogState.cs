using System;
using System.Threading.Tasks;

namespace Brows {
    public interface IDialogState {
        object Window { get; set; }
        IDialog Current { get; }
        Task<T> GetOrAddAsync<T>(Func<T, bool> predicate, Func<T> factory) where T : IAcknowledge;
    }
}
