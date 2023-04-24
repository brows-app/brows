using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public sealed class PreviewHandlerWrapper : ComObjectWrapper<IPreviewHandler> {
        private IPreviewHandler PreviewHandler => ComObject();

        private static void Initialize(object previewHandler, string file) {
            if (previewHandler is IInitializeWithStream initializeWithStream) {
                var stream = default(IStream);
                try {
                    var hr = shlwapi.SHCreateStreamOnFileEx(
                        pszFile: file,
                        grfMode: STGM.READ | STGM.SHARE_DENY_NONE,
                        dwAttributes: 0,
                        fCreate: false,
                        pstmTemplate: null,
                        ppstm: ref stream);
                    hr.ThrowOnError();
                    hr = initializeWithStream.Initialize(stream, STGM.READ);
                    hr.ThrowOnError();
                    return;
                }
                catch (NotImplementedException) {
                }
                finally {
                    if (stream != null) {
                        Marshal.FinalReleaseComObject(stream);
                    }
                }
            }
            if (previewHandler is IInitializeWithFile initializeWithFile) {
                try {
                    var
                    hr = initializeWithFile.Initialize(file, STGM.READ);
                    hr.ThrowOnError();
                    return;
                }
                catch (NotImplementedException) {
                }
            }
            if (previewHandler is IInitializeWithItem initializeWithItem) {
                var ppv = default(IShellItem);
                try {
                    var iid = IID.Managed.IShellItem;
                    var
                    hr = shell32.SHCreateItemFromParsingName(file, IntPtr.Zero, ref iid, out ppv);
                    hr.ThrowOnError();
                    hr = initializeWithItem.Initialize(ppv, STGM.READ);
                    hr.ThrowOnError();
                    return;
                }
                catch (NotImplementedException) {
                }
                finally {
                    if (ppv != null) {
                        Marshal.FinalReleaseComObject(ppv);
                    }
                }
            }
            throw new InvalidOperationException(); // TODO: Improve this exception.
        }

        protected override IPreviewHandler Factory() {
            var ppv = default(IntPtr);
            var unk = default(object);
            try {
                var iid = IID.Managed.IPreviewHandler;
                var clsid = Clsid;
                try {
                    var hr = ole32.CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX.LOCAL_SERVER, ref iid, out ppv);
                    if (hr == HRESULT.CO_E_SERVER_EXEC_FAILURE) {
                        hr = ole32.CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX.LOCAL_SERVER, ref iid, out ppv);
                    }
                    hr.ThrowOnError();
                    unk = Marshal.GetUniqueObjectForIUnknown(ppv);
                }
                catch {
                    unk = Activator.CreateInstance(Type.GetTypeFromCLSID(clsid));
                }
                var previewHandler = unk as IPreviewHandler;
                if (previewHandler == null) {
                    throw new InvalidOperationException(); // TODO: Improve this exception.
                }
                return previewHandler;
            }
            catch {
                if (ppv != IntPtr.Zero) {
                    Marshal.Release(ppv);
                }
                if (unk != null) {
                    Marshal.FinalReleaseComObject(unk);
                }
                throw;
            }
        }

        public Guid Clsid { get; }

        public PreviewHandlerWrapper(Guid clsid) {
            Clsid = clsid;
        }

        public void Initialize(string file) {
            Initialize(PreviewHandler, file);
        }

        public void SetSite(object site) {
            var objectWithSite = ComObject() as IObjectWithSite;
            if (objectWithSite != null) {
                objectWithSite.SetSite(site).ThrowOnError();
            }
        }

        public void SetWindow(IntPtr hwnd, RECT rect) {
            var
            hr = PreviewHandler.SetWindow(hwnd, ref rect);
            hr.ThrowOnError();
        }

        public void DoPreview() {
            var
            hr = PreviewHandler.DoPreview();
            hr.ThrowOnError();
        }

        public void SetRect(RECT rect) {
            var
            hr = PreviewHandler.SetRect(ref rect);
            hr.ThrowOnError();
        }

        public void SetBackgroundColor(uint color) {
            var visuals = PreviewHandler as IPreviewHandlerVisuals;
            if (visuals != null) {
                visuals.SetBackgroundColor(color).ThrowOnError();
            }
        }

        public void SetTextColor(uint color) {
            var visuals = PreviewHandler as IPreviewHandlerVisuals;
            if (visuals != null) {
                visuals.SetTextColor(color).ThrowOnError();
            }
        }

        public void Unload() {
            var
            hr = PreviewHandler.Unload();
            hr.ThrowOnError();
        }
    }
}
