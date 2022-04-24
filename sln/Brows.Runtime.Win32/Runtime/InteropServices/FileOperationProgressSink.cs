namespace Brows.Runtime.InteropServices {
    using ComTypes;
    using Logger;
    using Win32;

    internal class FileOperationProgressSink : IFileOperationProgressSink {
        private const HRESULT S_OK = HRESULT.S_OK;

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(FileOperationProgressSink)));
        private ILog _Log;

        public virtual HRESULT StartOperations() {
            if (Log.Info()) {
                Log.Info(nameof(StartOperations));
            }
            return S_OK;
        }

        public virtual HRESULT FinishOperations(uint hrResult) {
            if (Log.Info()) {
                Log.Info(
                    nameof(FinishOperations),
                    nameof(hrResult) + " > " + hrResult);
            }
            return S_OK;
        }

        public virtual HRESULT PreRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PreRenameItem),
                    nameof(pszNewName) + " > " + pszNewName);
            }
            return S_OK;
        }

        public virtual HRESULT PostRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName, uint hrRename, IShellItem psiNewlyCreated) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PostRenameItem),
                    nameof(pszNewName) + " > " + pszNewName,
                    nameof(hrRename) + " > " + hrRename);
            }
            return S_OK;
        }

        public virtual HRESULT PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PreMoveItem),
                    nameof(pszNewName) + " > " + pszNewName);
            }
            return S_OK;
        }

        public virtual HRESULT PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, uint hrMove, IShellItem psiNewlyCreated) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PostMoveItem),
                    nameof(pszNewName) + " > " + pszNewName);
            }
            return S_OK;
        }

        public virtual HRESULT PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PreCopyItem),
                    nameof(pszNewName) + " > " + pszNewName);
            }
            return S_OK;
        }

        public virtual HRESULT PostCopyItem(uint flags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, uint copyResult, IShellItem psiNewlyCreated) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PostCopyItem),
                    nameof(pszNewName) + " > " + pszNewName,
                    nameof(copyResult) + " > " + copyResult);
            }
            return S_OK;
        }

        public virtual HRESULT PreDeleteItem(uint dwFlags, IShellItem psiItem) {
            if (Log.Info()) {
                Log.Info(nameof(PreDeleteItem));
            }
            return S_OK;
        }

        public virtual HRESULT PostDeleteItem(uint dwFlags, IShellItem psiItem, uint hrDelete, IShellItem psiNewlyCreated) {
            if (Log.Info()) {
                Log.Info(nameof(PostDeleteItem));
            }
            return S_OK;
        }

        public virtual HRESULT PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PreNewItem),
                    nameof(pszNewName) + " > " + pszNewName);
            }
            return S_OK;
        }

        public virtual HRESULT PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName, string pszTemplateName, uint dwFileAttributes, uint hrNew, IShellItem psiNewItem) {
            if (Log.Info()) {
                Log.Info(
                    nameof(PostNewItem),
                    nameof(pszNewName) + " > " + pszNewName,
                    nameof(pszTemplateName) + " > " + pszTemplateName);
            }
            return S_OK;
        }

        public virtual HRESULT UpdateProgress(uint iWorkTotal, uint iWorkSoFar) {
            if (Log.Debug()) {
                Log.Debug(
                    nameof(UpdateProgress),
                    nameof(iWorkTotal) + " > " + iWorkTotal,
                    nameof(iWorkSoFar) + " > " + iWorkSoFar);
            }
            return S_OK;
        }

        public HRESULT ResetTimer() {
            if (Log.Info()) {
                Log.Info(nameof(ResetTimer));
            }
            return S_OK;
        }

        public HRESULT PauseTimer() {
            if (Log.Info()) {
                Log.Info(nameof(PauseTimer));
            }
            return S_OK;
        }

        public HRESULT ResumeTimer() {
            if (Log.Info()) {
                Log.Info(nameof(ResumeTimer));
            }
            return S_OK;
        }
    }
}
