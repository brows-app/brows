using NUnit.Framework;
using Brows.Composition;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class ImportsTest_Kill {
    private sealed class MyExport1 : IExport, IDisposable {
        public int DisposeCount => _DisposeCount;
        private int _DisposeCount;

        void IDisposable.Dispose() {
            Interlocked.Increment(ref _DisposeCount);
        }
    }

    [Test]
    public async Task Kill_DisposesDisposables() {
        var preKill = 0;
        var postKill = 0;
        await ImportSandbox.Clean(function: async () => {
            await Imports.Init(null, default);
            var obj = Imports.Current.Find<MyExport1>();
            preKill = obj.DisposeCount;
            Imports.Current.Kill();
            postKill = obj.DisposeCount;
        });
        using (Assert.EnterMultipleScope()) {
            Assert.That(preKill, Is.EqualTo(0));
            Assert.That(postKill, Is.EqualTo(1));
        }
    }

    private sealed class MyExport2 : IExport, IExportAndKill, IDisposable {
        public Action Kill { get; set; }
        public Action Dispose { get; set; }

        void IDisposable.Dispose() {
            Dispose?.Invoke();
        }

        void IExportAndKill.Kill() {
            Kill?.Invoke();
        }
    }

    [Test]
    public async Task Kill_CallsKillBeforeDisposeOnImports() {
        var killCount = 0;
        var disposeCount = 0;
        await ImportSandbox.Clean(function: async () => {
            await Imports.Init(null, default);
            var
            obj = Imports.Current.Find<MyExport2>();
            obj.Kill = () => {
                if (disposeCount == 0) {
                    killCount++;
                }
            };
            obj.Dispose = () => {
                if (killCount == 1) {
                    disposeCount++;
                }
            };
            Imports.Current.Kill();
        });
        using (Assert.EnterMultipleScope()) {
            Assert.That(killCount, Is.EqualTo(1));
            Assert.That(disposeCount, Is.EqualTo(1));
        }
    }
}
