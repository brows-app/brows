using System;

namespace Brows.Cli {
    public class CommandTokenAttribute : Attribute {
        public string Name { get; set; }
        public bool Required { get; set; }
        public bool Aggregate { get; set; }
        public string DefaultValue { get; set; }
    }
}
