using Domore.Conf.Cli;
using Domore.Conf.Converters;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class DrawImageParameter {
        [CliArgument]
        public string Output { get; set; }

        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool? Overwrite { get; set; }
        public int? Rotate { get; set; }
        public int? JpegQuality { get; set; }

        [CliDisplay(false)]
        public bool? CopyMetadata { get; set; }

        [CliDisplay(false)]
        [ConfListItems(Separator = ";")]
        public List<string> TransferMetadata {
            get => _TransferMetadata ?? (_TransferMetadata = new());
            set => _TransferMetadata = value;
        }
        private List<string> _TransferMetadata;
    }
}
