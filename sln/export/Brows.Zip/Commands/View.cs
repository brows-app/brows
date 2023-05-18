using Brows.Detail;
using System;

namespace Brows.Commands {
    internal sealed class View : ZipCommand<View.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.HasParameter(out var parameter)) return false;
            if (false == active.HasProvider(out ZipProvider provider)) {
                return false;
            }
            var toggle = parameter.Toggle;
            if (toggle.HasFlag(Views.Preview)) {
                provider.Detail[Views.Preview] = provider.Detail[Views.Preview] == null
                    ? new ZipEntryPreview(provider)
                    : null;
            }
            return true;
        }

        [Flags]
        public enum Views {
            None = 0,
            Navigation = 1,
            Preview = 2,
        }

        public sealed class Parameter {
            public Views Toggle { get; set; }
        }
    }
}
