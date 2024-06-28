using System;
using System.Collections;
using System.Collections.ObjectModel;

namespace Brows.Gui.Collections {
    public sealed class SyncedCollection<T> : ObservableCollection<T>, ISyncedCollection {
        private readonly object Locker = new();

        public T Sync<T>(Func<T> accessMethod, bool writeAccess = true) {
            ArgumentNullException.ThrowIfNull(accessMethod);
            lock (Locker) {
                return accessMethod();
            }
        }

        public void Sync(Action accessMethod, bool writeAccess = true) {
            ArgumentNullException.ThrowIfNull(accessMethod);
            lock (Locker) {
                accessMethod();
            }
        }

        void ISyncedCollection.Sync(IEnumerable collection, object context, Action accessMethod, bool writeAccess) {
            Sync(accessMethod, writeAccess);
        }
    }
}
