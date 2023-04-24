using Domore.Logs;
using Domore.Runtime.Win32;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Brows {
    public static class Win32File {
        private static readonly ILog Log = Logging.For(typeof(Win32File));

        private static void Execute(string file, string parameters, string verb) {
            if (Log.Info()) {
                Log.Info((verb ?? nameof(Open)) + " > " + file + (parameters == null ? "" : (" > " + parameters)));
            }
            var info = new SHELLEXECUTEINFOW {
                cbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFOW>(),
                fMask = (uint)(SEE_MASK.NOASYNC | SEE_MASK.INVOKEIDLIST | SEE_MASK.FLAG_NO_UI | SEE_MASK.FLAG_LOG_USAGE),
                lpDirectory = Path.GetDirectoryName(file),
                lpFile = file,
                lpParameters = parameters,
                lpVerb = verb,
                nShow = (int)SW.SHOW,
            };
            var success = shell32.ShellExecuteExW(ref info);
            if (success == false) {
                throw new Win32Exception();
            }
        }

        public static void Open(string file) {
            Execute(file, parameters: null, verb: null);
        }

        public static void Open(string file, string with) {
            Execute(file: with, parameters: $"\"{file}\"", verb: null);
        }

        public static void Properties(string file) {
            Execute(file, parameters: null, verb: "properties");
        }
    }
}
