namespace Brows {
    public interface IOperatorDeployment {
        IPayload Payload { get; }
        IPayloadTarget Target { get; }
        IDialogState Dialog { get; }
        IOperationCollection Operations { get; }
        bool RemoveCompletedOperations { get; }
    }
}
