using Domore.Logs;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Brows.Composition {
    internal sealed class Export {
        private static readonly ILog Log = Logging.For(typeof(Export));

        private object CreateInstance() {
            if (Log.Info()) {
                Log.Info(nameof(CreateInstance) + " > " + Type);
            }
            try {
                return Constructor.Invoke(null);
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(nameof(CreateInstance) + " > " + Type, ex);
                }
                return null;
            }
        }

        private static ConstructorInfo GetConstructor(Type type) {
            if (type.IsAbstract) {
                return null;
            }
            if (type.GenericTypeArguments?.Length != 0) {
                return null;
            }
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null) {
                return null;
            }
            if (constructor.ContainsGenericParameters) {
                return null;
            }
            return constructor;
        }

        private static Export FromExport(Type type, FileInfo file) {
            var match = Match(type, out var constructor);
            if (match == false) {
                return null;
            }
            return new Export(type, file, constructor: constructor);
        }

        public object Instance {
            get => _Instance ?? (_Instance = CreateInstance());
            set => _Instance = value;
        }
        private object _Instance;

        public Type Type { get; }
        public Type Target { get; }
        public FileInfo File { get; }
        public ConstructorInfo Constructor { get; }

        public Export(Type type, FileInfo file, ConstructorInfo constructor = null, object instance = null) {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            File = file; // ?? throw new ArgumentNullException(nameof(file));
            Constructor = constructor;
            Instance = instance;
            Target = Type
                .GetInterfaces()
                .Where(i => i.IsGenericType)
                .Where(i => i.GetGenericTypeDefinition() == typeof(IExport<>))
                .Select(i => i.GetGenericArguments().First())
                .FirstOrDefault();
        }

        public bool Implements(Type type) {
            return Type.IsAssignableTo(type);
        }

        public bool Targets(Type type) {
            var target = Target;
            return target == null
                ? false
                : target.IsAssignableFrom(type);
        }

        public sealed override string ToString() {
            var s = $"Export [{Type.Name}] from [{File?.Name}]";
            var target = Target;
            if (target != null) {
                s += $" targets [{target.Name}]";
            }
            var inst = _Instance;
            if (inst != null) {
                s += $" as [{inst}]";
            }
            return s;
        }

        public static bool Match(Type type, out ConstructorInfo constructor) {
            constructor = null;
            bool none() {
                if (Log.Debug()) {
                    Log.Debug($"!{nameof(Match)} > {type}");
                }
                return false;
            }
            if (type == null) {
                return none();
            }
            if (type.IsAssignableTo(typeof(IExport)) == false) {
                return none();
            }
            var c = constructor = GetConstructor(type);
            if (c == null) {
                return none();
            }
            if (Log.Info()) {
                Log.Info(
                    $"{nameof(Match)} > {type}",
                    $"{nameof(type.Assembly)} > {type.Assembly}",
                    $"{nameof(type.Assembly.Location)} > {type.Assembly?.Location}");
            }
            return true;
        }
    }
}
