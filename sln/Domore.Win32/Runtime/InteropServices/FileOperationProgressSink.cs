namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public class FileOperationProgressSink : IFileOperationProgressSink {
        private const HRESULT S_OK = HRESULT.S_OK;

        public virtual HRESULT StartOperations() {
            return S_OK;
        }

        public virtual HRESULT FinishOperations(uint hrResult) {
            return S_OK;
        }

        public virtual HRESULT PreRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName) {
            return S_OK;
        }

        public virtual HRESULT PostRenameItem(uint dwFlags, IShellItem psiItem, string pszNewName, uint hrRename, IShellItem psiNewlyCreated) {
            return S_OK;
        }

        public virtual HRESULT PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName) {
            return S_OK;
        }

        public virtual HRESULT PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, uint hrMove, IShellItem psiNewlyCreated) {
            return S_OK;
        }

        public virtual HRESULT PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName) {
            return S_OK;
        }

        public virtual HRESULT PostCopyItem(uint flags, IShellItem psiItem, IShellItem psiDestinationFolder, string pszNewName, uint copyResult, IShellItem psiNewlyCreated) {
            return S_OK;
        }

        public virtual HRESULT PreDeleteItem(uint dwFlags, IShellItem psiItem) {
            return S_OK;
        }

        public virtual HRESULT PostDeleteItem(uint dwFlags, IShellItem psiItem, uint hrDelete, IShellItem psiNewlyCreated) {
            return S_OK;
        }

        public virtual HRESULT PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName) {
            return S_OK;
        }

        public virtual HRESULT PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, string pszNewName, string pszTemplateName, uint dwFileAttributes, uint hrNew, IShellItem psiNewItem) {
            return S_OK;
        }

        public virtual HRESULT UpdateProgress(uint iWorkTotal, uint iWorkSoFar) {
            return S_OK;
        }

        public HRESULT ResetTimer() {
            return S_OK;
        }

        public HRESULT PauseTimer() {
            return S_OK;
        }

        public HRESULT ResumeTimer() {
            return S_OK;
        }
    }
}
