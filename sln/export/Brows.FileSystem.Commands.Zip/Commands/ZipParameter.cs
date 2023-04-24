using Domore.Conf;
using Domore.Conf.Cli;
using System.IO.Compression;

namespace Brows.Commands {
    internal sealed class ZipParameter {
        [CliArgument]
        public string Output { get; set; }

        [Conf("force")]
        public ZipDirection? Direction { get; set; }

        [Conf("compression")]
        public CompressionLevel? CompressionLevel { get; set; }

        public bool? Overwrite { get; set; }
    }
}
