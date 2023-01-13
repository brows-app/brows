using System.Collections.Generic;

namespace Brows.Config {
    internal class Key {
        public Dictionary<string, Cmd> Cmd {
            get => _Cmd ?? (_Cmd = new());
            set => _Cmd = value;
        }
        private Dictionary<string, Cmd> _Cmd;
    }
}
