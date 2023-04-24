namespace Brows {
    public interface IExport {
    }

    public interface IExport<out TTarget> : IExport {
    }
}
