namespace Brows {
    public interface IOperationProgress {
        IOperationProgressInfo Info { get; }
        IOperationProgressTarget Target { get; }
        long Get();
        void Add(long value);
        void Set(long value);
        IOperable Child(string name);
    }
}
