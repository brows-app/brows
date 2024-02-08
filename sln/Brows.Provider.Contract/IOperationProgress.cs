namespace Brows {
    public interface IOperationProgress {
        OperationProgressKind Kind { get; }
        void Change(long? addProgress = null, long? setProgress = null, long? addTarget = null, long? setTarget = null, string name = null, string data = null, OperationProgressKind? kind = null);
        void Child(string name, OperationProgressKind kind, OperationDelegate task);

        public void Child(string name, OperationDelegate task) {
            Child(name, OperationProgressKind.None, task);
        }
    }
}
