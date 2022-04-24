using System;

namespace Brows.Dialogs {
    public class NewEntry : OkCancel {
        public Func<string, bool> PathExists {
            get => _PathExists ?? (_PathExists = _ => false);
            set => Change(ref _PathExists, value, nameof(PathExists), nameof(PathExistsFlag));
        }
        private Func<string, bool> _PathExists;

        public string PathExistsFlag => PathExists(Path)
            ? "Danger"
            : "Default";

        public string Path {
            get => _Path;
            set => Change(ref _Path, value, nameof(Path), nameof(PathExistsFlag));
        }
        private string _Path;
    }
}
