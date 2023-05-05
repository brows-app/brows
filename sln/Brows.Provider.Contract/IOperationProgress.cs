namespace Brows {
    public interface IOperationProgress {
        void Change(long? addProgress = null, long? setProgress = null, long? addTarget = null, long? setTarget = null, string name = null, string data = null);
        void Child(string name, OperationDelegate task);
    }
}
