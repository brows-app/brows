using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO.Compression {
    using Formats;

    public abstract class CompressionFormat : ICompressionFormat {
        protected abstract string Extension { get; }

        protected abstract Task Create(ICompressionCreate state, CancellationToken cancellationToken);

        public static ICompressionFormat Create(string format) {
            var f = string.Join("", format?.Trim()?.Split() ?? new string[] { });
            if (f == "") {
                return new Zip();
            }
            var fIs = new Func<string, bool>(s => f.Equals(s, StringComparison.OrdinalIgnoreCase));
            if (fIs(nameof(Zip))) {
                return new Zip();
            }
            throw new ArgumentException(paramName: nameof(format), message: $"{nameof(format)} > {format}");
        }

        string ICompressionFormat.Extension =>
            Extension;

        Task ICompressionFormat.Create(ICompressionCreate state, CancellationToken cancellationToken) {
            return Create(state, cancellationToken);
        }
    }
}
