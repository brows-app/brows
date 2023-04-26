using System;

namespace Brows {
    public interface IOperation {
        event EventHandler RelevantChanged;

        string Name { get; }
        int Depth { get; }
        long Target { get; }
        long Progress { get; }
        bool Progressing { get; }
        double ProgressPercent { get; }
        bool Relevant { get; }
        bool Complete { get; }
        Exception Error { get; }
    }
}
