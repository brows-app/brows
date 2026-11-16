using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Native;

[TestFixture]
internal sealed class NativeLoaderTest {
    private static string Dll =>
        Path.Combine(Environment.SystemDirectory, "kernel32.dll");

    [OneTimeSetUp]
    public async Task OneTimeSetUp() {
        /*
         * Every test loader awaits the static Brows loader first. Warm it once
         * so test calls reach their own load state synchronously and the
         * assertions below stay deterministic.
         */
        await new TestLoader(MissingDll).Loaded(CancellationToken.None);
    }

    private static string MissingDll =>
        Path.Combine(Environment.SystemDirectory, "brows_native_loader_test_missing.dll");

    [Test]
    public async Task Loaded_CanceledToken_ThrowsAndDoesNotPoisonFeature() {
        var loader = new TestLoader(Dll);

        Assert.That(
            async () => await loader.Loaded(new CancellationToken(canceled: true)),
            Throws.TypeOf<OperationCanceledException>());

        /*
         * The canceled call must not leave a canceled task in the one-shot
         * load cache. A later call, even with any token, must still load.
         */
        Assert.That(await loader.Loaded(CancellationToken.None), Is.True);
        Assert.That(loader.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public async Task Loaded_Fault_RetriesOnNextCall() {
        var loader = new TestLoader(MissingDll);

        Assert.That(await loader.Loaded(CancellationToken.None), Is.False);
        var firstError = loader.Error;
        Assert.That(firstError, Is.Not.Null);

        /*
         * The next call must attempt the load again, producing a fresh fault,
         * rather than re-serving the cached faulted task forever.
         */
        Assert.That(await loader.Loaded(CancellationToken.None), Is.False);
        Assert.That(loader.Error, Is.Not.Null);
        Assert.That(loader.Error, Is.Not.SameAs(firstError));
    }

    [Test]
    public async Task Loaded_SuccessfulRetry_ClearsPreviousError() {
        var error = new InvalidOperationException("The first initialization failed.");
        var loader = new TestLoader(Dll) { InitializationError = error };
        loader.ReleaseHandleLoaded.Set();
        try {
            Assert.That(await loader.Loaded(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)), Is.False);
            Assert.That(loader.Handle, Is.EqualTo(IntPtr.Zero));
            Assert.That(loader.Error, Is.SameAs(error));

            loader.InitializationError = null;

            Assert.That(await loader.Loaded(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(loader.Handle, Is.Not.EqualTo(IntPtr.Zero));
            Assert.That(loader.Error, Is.Null);
        }
        finally {
            await loader.Freed(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task Loaded_HandleNotPublishedUntilHandleLoadedCompletes() {
        var loader = new TestLoader(Dll);

        var loadTask = loader.Loaded(CancellationToken.None);
        Assert.That(loader.HandleLoadedEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);

        /*
         * While HandleLoaded is still running, the module handle must not be
         * published. Callers must not P/Invoke an uninitialized library.
         */
        Assert.That(loader.Handle, Is.EqualTo(IntPtr.Zero));

        loader.ReleaseHandleLoaded.Set();
        Assert.That(await loadTask, Is.True);
        Assert.That(loader.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public async Task Loaded_ConcurrentCall_WaitsForHandleLoaded() {
        var loader = new TestLoader(Dll);

        var first = loader.Loaded(CancellationToken.None);
        Assert.That(loader.HandleLoadedEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);

        var second = loader.Loaded(CancellationToken.None);

        /*
         * While initialization is still running, the concurrent call must not
         * complete. Give it a bounded chance to finish early: under the fixed
         * code its body awaits the in-flight load, so this must time out.
         */
        Assert.That(SpinWait.SpinUntil(() => second.IsCompleted, TimeSpan.FromSeconds(2)), Is.False);

        loader.ReleaseHandleLoaded.Set();
        Assert.That(await first, Is.True);
        Assert.That(await second, Is.True);
    }

    [Test]
    public async Task Freed_LoadedLibrary_FreesIt() {
        var loader = new TestLoader(Dll);

        Assert.That(await loader.Loaded(CancellationToken.None), Is.True);
        await loader.Freed(CancellationToken.None);

        /*
         * After a free, the library is gone and the loader stays closed.
         */
        Assert.That(loader.Handle, Is.EqualTo(IntPtr.Zero));
        Assert.That(loader.Error, Is.Null);
        Assert.That(await loader.Loaded(CancellationToken.None), Is.False);
    }

    [Test]
    public async Task Freed_InFlightLoad_FreesAfterItFinishes() {
        var loader = new TestLoader(Dll);
        loader.ReleaseHandleFreeing.Reset();
        Task freeing = null;
        try {
            var loading = loader.Loaded(CancellationToken.None);
            Assert.That(loader.HandleLoadedEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);

            /*
             * Freeing must not enter or complete while initialization is gated.
             */
            freeing = loader.Freed(CancellationToken.None);
            Assert.That(loader.HandleFreeingEntered.Wait(TimeSpan.FromMilliseconds(100)), Is.False);
            Assert.That(freeing.IsCompleted, Is.False);
            Assert.That(loader.Handle, Is.EqualTo(IntPtr.Zero));

            loader.ReleaseHandleLoaded.Set();
            Assert.That(loader.HandleFreeingEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);

            /*
             * Hold the free callback so either continuation can run first while
             * the published handle remains valid until Loaded returns.
             */
            Assert.That(await loading.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(loader.Handle, Is.Not.EqualTo(IntPtr.Zero));
            Assert.That(freeing.IsCompleted, Is.False);

            loader.ReleaseHandleFreeing.Set();
            await freeing.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(loader.Handle, Is.EqualTo(IntPtr.Zero));
            Assert.That(await loader.Loaded(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)), Is.False);
        }
        finally {
            loader.ReleaseHandleLoaded.Set();
            loader.ReleaseHandleFreeing.Set();
            freeing ??= loader.Freed(CancellationToken.None);
            await freeing.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task Freed_LoadFailed_DoesNotFreeAndKeepsError() {
        var loader = new TestLoader(MissingDll);

        Assert.That(await loader.Loaded(CancellationToken.None), Is.False);
        var error = loader.Error;
        Assert.That(error, Is.Not.Null);

        /*
         * With no load task left behind by the failed load, Freed has nothing
         * to free and must not clobber the reported error or close the loader;
         * a later load may yet succeed.
         */
        await loader.Freed(CancellationToken.None);

        Assert.That(loader.Handle, Is.EqualTo(IntPtr.Zero));
        Assert.That(loader.Error, Is.SameAs(error));
        Assert.That(await loader.Loaded(CancellationToken.None), Is.False);
    }

    private sealed class TestLoader : NativeLoader {
        internal Exception InitializationError { get; set; }
        internal ManualResetEventSlim HandleLoadedEntered { get; } = new(false);
        internal ManualResetEventSlim ReleaseHandleLoaded { get; } = new(false);
        internal ManualResetEventSlim HandleFreeingEntered { get; } = new(false);
        internal ManualResetEventSlim ReleaseHandleFreeing { get; } = new(true);

        protected override void HandleFreeing() {
            HandleFreeingEntered.Set();
            if (ReleaseHandleFreeing.Wait(TimeSpan.FromSeconds(5)) == false) {
                throw new TimeoutException("The test did not release the free callback.");
            }
        }

        protected override void HandleLoaded() {
            HandleLoadedEntered.Set();
            ReleaseHandleLoaded.Wait(TimeSpan.FromSeconds(10));
            if (InitializationError is not null) {
                throw InitializationError;
            }
        }

        public TestLoader(string dll) : base(dll) {
        }
    }
}
