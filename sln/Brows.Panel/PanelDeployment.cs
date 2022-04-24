namespace Brows {
    internal class PanelDeployment : IOperatorDeployment {
        public IPayload Payload { get; }
        public IPayloadTarget Target { get; }
        public IDialogState Dialog { get; }
        public IOperationCollection Operations { get; }
        public bool RemoveCompletedOperations { get; }

        public PanelDeployment(IPayload payload, IPayloadTarget target, IDialogState dialog, IOperationCollection operations, bool removeCompletedOperations) {
            Payload = payload;
            Target = target;
            Dialog = dialog;
            Operations = operations;
            RemoveCompletedOperations = removeCompletedOperations;
        }
    }
}
