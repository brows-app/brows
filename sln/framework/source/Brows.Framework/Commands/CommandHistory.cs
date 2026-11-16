using Domore.Conf.Cli;
using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal sealed class CommandHistory {
    private readonly Comparer ContentComparer = new();
    private readonly List<ICommandLine> List = [];

    public void Add(ICommandLine history) {
        /*
         * Re-adding an entry removes it from its old position and inserts it
         * at the front, so re-executed commands sort most-recent-first. A
         * hash set would reuse its old slot instead of moving it.
         */
        List.RemoveAll(item => ContentComparer.Equals(item, history));
        List.Insert(0, history);
    }

    public IEnumerable<ICommandLine> AsEnumerable() {
        return List;
    }

    private sealed class Comparer : IEqualityComparer<ICommandLine> {
        private readonly CliComparer InputComparer = new();
        private readonly StringComparer ConfComparer = StringComparer.Ordinal;

        public bool Equals(ICommandLine x, ICommandLine y) {
            if (x == null && y == null) {
                return true;
            }
            if (x == null || y == null) {
                return false;
            }
            x.HasInput(out var xi);
            y.HasInput(out var yi);
            var inputEqual = InputComparer.Equals(xi, yi);
            if (inputEqual == false) {
                return false;
            }
            x.HasConf(out var xc);
            y.HasConf(out var yc);
            var confEqual = ConfComparer.Equals(xc, yc);
            if (confEqual == false) {
                return false;
            }
            return true;
        }

        public int GetHashCode(ICommandLine obj) {
            if (obj == null) return EqualityComparer<object>.Default.GetHashCode(obj);
            var inp = obj.HasInput(out var i) ? InputComparer.GetHashCode(i) : 0;
            var cnf = obj.HasConf(out var c) ? ConfComparer.GetHashCode(c) : 0;
            return HashCode.Combine(inp, cnf);
        }
    }
}
