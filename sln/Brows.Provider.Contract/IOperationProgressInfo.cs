namespace Brows {
    public interface IOperationProgressInfo {
        void Name(string value);
        void Data(string value);
        void Data(string format, params object[] args);
    }
}
