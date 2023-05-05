using Domore.Conf.Cli;

namespace Brows.Commands {
    internal sealed class DrawImageParameter {
        [CliArgument]
        public string Output { get; set; }

        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool? Overwrite { get; set; }
        public int? Rotate { get; set; }
        public int? JpegQuality { get; set; }
    }
}
