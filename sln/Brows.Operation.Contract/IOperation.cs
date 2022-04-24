using System;

namespace Brows {
    public interface IOperation {
        event EventHandler RelevantChanged;

        string Name { get; }
        string Description { get; }
        int Depth { get; }
        long Target { get; }
        long Progress { get; }
        bool Progressing { get; }
        double ProgressPercent { get; }
        bool Relevant { get; }
        Exception Error { get; }
    }
}
