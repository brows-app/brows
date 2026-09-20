using System;
using System.Collections.Generic;

namespace Brows.Composition.ImportAgents;

internal sealed class ImportNone : IImport {
    private void ThrowIfNotReady(bool flag) {
        if (flag) {
            throw new ImportsNotReadyException(this, "The imports will never be ready.");
        }
    }

    bool IImportAgent.Ready => false;

    IImportEnvironment IImport.Environment => null;

    T IImportAgent.Construct<T>() {
        throw new NotSupportedException();
    }

    T IImportAgent.Find<T>(bool throwIfNotFound, bool throwIfNotReady) {
        ThrowIfNotReady(throwIfNotReady);
        return default;
    }

    void IImport.Kill() {
    }

    IReadOnlyList<T> IImportAgent.List<T>(bool throwIfNotReady) {
        ThrowIfNotReady(throwIfNotReady);
        return [];
    }

    void IImportAgent.Populate(object obj, bool force) {
    }

    void IImport.ReadyCallback(Action action) {
    }
}
