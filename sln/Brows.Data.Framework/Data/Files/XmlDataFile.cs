using System;
using System.IO;
using System.Xml.Serialization;

namespace Brows.Data.Files {
    using Logger;

    internal class XmlDataFile : DataFile {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(XmlDataFile)));
        private ILog _Log;

        public override string Extension => "xml";

        public override void Save(object data, Type type, string path) {
            if (Log.Info()) {
                Log.Info(
                    nameof(Save),
                    nameof(type) + " > " + type,
                    nameof(path) + " > " + path);
            }
            var serializer = new XmlSerializer(type);
            using (var writer = new StreamWriter(path)) {
                serializer.Serialize(writer, data);
            }
        }

        public override object Load(Type type, string path) {
            var pathExists = File.Exists(path);
            if (pathExists == false) return null;
            if (Log.Info()) {
                Log.Info(
                    nameof(Load),
                    nameof(type) + " > " + type,
                    nameof(path) + " > " + path);
            }
            var serializer = new XmlSerializer(type);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                return serializer.Deserialize(fs);
            }
        }
    }
}
