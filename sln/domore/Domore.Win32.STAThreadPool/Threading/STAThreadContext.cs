using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Domore.Threading {
    internal sealed class STAThreadContext {
        private static readonly ILog Log = Logging.For(typeof(STAThreadContext));

        private SynchronizationContext Sync;
        private Task<SynchronizationContext> SyncTask;

        private async Task<SynchronizationContext> Start() {
            if (Log.Info()) {
                Log.Info(Name + " " + nameof(Start));
            }
            return await Task.Run(() => {
                using (var threadIdle = new ManualResetEventSlim()) {
                    var sync = default(SynchronizationContext);
                    var thread = new Thread(() => {
                        try {
                            EventHandler idle = default;
                            Application.Idle += idle = (s, e) => {
                                if (Log.Info()) {
                                    Log.Info(Name + " " + nameof(Application.Idle));
                                }
                                try {
                                    sync = SynchronizationContext.Current;
                                    Application.Idle -= idle;
                                    threadIdle.Set();
                                }
                                catch (Exception ex) {
                                    if (Log.Error()) {
                                        Log.Error(Name, nameof(Application.Idle), ex);
                                    }
                                }
                            };
                            Application.Run();
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(Name, nameof(Application.Run), ex);
                            }
                        }
                    });
                    thread.IsBackground = true;
                    thread.Name = Name;
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    threadIdle.Wait();
                    return sync;
                }
            });
        }

        public string Name { get; }

        public STAThreadContext(string name) {
            Name = name;
        }

        public async ValueTask<SynchronizationContext> Ready() {
            if (Sync == null) {
                if (SyncTask == null) {
                    SyncTask = Start();
                }
                Sync = await SyncTask;
            }
            return Sync;
        }

        public void Exit() {
            if (Log.Info()) {
                Log.Info(Name + " " + nameof(Exit));
            }
            var sync = Sync;
            if (sync != null) {
                sync.Post(state: null, d: _ => {
                    try {
                        Application.ExitThread();
                    }
                    catch (Exception ex) {
                        if (Log.Error()) {
                            Log.Error(Name, nameof(Application.ExitThread), ex);
                        }
                    }
                });
            }
        }

        public sealed override string ToString() {
            return Name;
        }
    }
}
