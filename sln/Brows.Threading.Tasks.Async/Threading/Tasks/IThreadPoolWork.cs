using System;
using System.Threading.Tasks;

namespace Brows.Threading.Tasks {
    public interface IThreadPoolWork {
        Task Run(Action action);
        Task<T> Run<T>(Func<T> func);
    }
}
