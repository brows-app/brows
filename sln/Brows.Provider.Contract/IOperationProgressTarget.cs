namespace Brows {
    public interface IOperationProgressTarget {
        long Get();
        void Add(long value);
        void Set(long value);
    }
}
