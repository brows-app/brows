using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class EntryThumbnailData : EntryData<object> {
        private static readonly Task<object> Result = Task.FromResult(default(object));

        protected sealed override Task<object> Access(CancellationToken cancellationToken) {
            return Result;
        }

        public EntryThumbnailData() : base(nameof(Entry.Thumbnail), CancellationToken.None) {
        }
    }
}
