using Domore.Logs;
using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.ComTypes;
using Domore.Runtime.Win32;
using Domore.Threading;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.IO {
    using Brows.Extensions;

    internal class Win32FilePropertyProvider : FilePropertyProvider {
        private static readonly ILog Log = Logging.For(typeof(Win32FilePropertyProvider));
        private static readonly Guid RIID = Guid.Parse(IID.IPropertyStore);

        private IEnumerable<IFileProperty> GetAllProperties(IPropertyStore ppv, CancellationToken cancellationToken) {
            if (null == ppv) throw new ArgumentNullException(nameof(ppv));
            var
            hr = ppv.GetCount(out var cProps);
            hr.ThrowOnError();
            cancellationToken.ThrowIfCancellationRequested();

            for (var i = 0U; i < cProps; i++) {
                hr = ppv.GetAt(i, out var pkey);
                hr.ThrowOnError();
                cancellationToken.ThrowIfCancellationRequested();

                var pv = default(PROPVARIANT);
                try {
                    hr = ppv.GetValue(ref pkey, ref pv);
                    hr.ThrowOnError();
                }
                catch (Exception ex) {
                    if (Log.Debug()) {
                        Log.Debug(ex);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();

                var value = pv.Value();
                if (value != null) {
                    using (var propertyDescription = new PropertyDescriptionWrapper(pkey.fmtid, pkey.pid)) {
                        yield return new FileProperty(propertyDescription.DisplayName, value /*propertyDescription.Display(pv)*/);
                    }
                }
            }
        }

        private IEnumerable<IFileProperty> GetNamedProperties(IPropertyStore ppv, IEnumerable<string> names, CancellationToken cancellationToken) {
            if (null == ppv) throw new ArgumentNullException(nameof(ppv));
            if (null == names) throw new ArgumentNullException(nameof(names));
            foreach (var name in names) {
                using (var propertyDescription = new PropertyDescriptionWrapper(name)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pv = default(PROPVARIANT);
                    var pkey = propertyDescription.PropertyKey;
                    try {
                        var
                        hr = ppv.GetValue(ref pkey, ref pv);
                        hr.ThrowOnError();
                    }
                    catch (Exception ex) {
                        if (Log.Debug()) {
                            Log.Debug(ex);
                        }
                    }
                    cancellationToken.ThrowIfCancellationRequested();

                    var value = pv.Value();
                    if (value != null) {
                        var fileProperty = new FileProperty(name, value /*propertyDescription.Display(pv)*/);
                        yield return fileProperty;
                    }
                }
            }
        }

        public override IReadOnlyList<string> PropertyKeys =>
            PropertyKeyLookup.Names;

        public STAThreadPool ThreadPool { get; }

        public Win32FilePropertyProvider(STAThreadPool threadPool) {
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public override IAsyncEnumerable<IFileProperty> GetProperties(string file, IEnumerable<string> keys, CancellationToken cancellationToken) {
            var ch = Channel.CreateUnbounded<IFileProperty>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            var writer = ch.Writer;
            var reader = ch.Reader;
            _ = ThreadPool.Work(name: nameof(GetProperties), cancellationToken: cancellationToken, work: async (cancellationToken) => {
                var error = default(Exception);
                try {
                    using (var wrapper = new PropertyStoreWrapper(file)) {
                        cancellationToken.ThrowIfCancellationRequested();

                        var ppv = wrapper.PropertyStore;
                        var enumerable = keys == null
                            ? GetAllProperties(ppv, cancellationToken)
                            : GetNamedProperties(ppv, keys, cancellationToken);
                        foreach (var item in enumerable) {
                            await writer.WriteAsync(item, cancellationToken);
                        }
                    }
                }
                catch (Exception ex) {
                    error = ex;
                }
                finally {
                    writer.Complete(error);
                }
            });
            return reader.ReadAllAsync(cancellationToken);
        }

        public override Task SetProperties(string file, IEnumerable<IFileProperty> properties, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
