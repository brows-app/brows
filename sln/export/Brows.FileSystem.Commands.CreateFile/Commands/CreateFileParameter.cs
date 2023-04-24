using Domore.Conf.Cli;

namespace Brows.Commands {
    internal sealed class CreateFileParameter {
        [CliArgument]
        [CliRequired]
        public string Name {
            get => _Name ?? (_Name = "");
            set => _Name = value;
        }
        private string _Name;

        public bool? Open { get; set; }
    }
}
