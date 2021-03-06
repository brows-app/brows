using System;

namespace Brows.Data {
    public abstract class DataFile {
        public abstract string Extension { get; }
        public abstract void Save(object data, Type type, string path);
        public abstract object Load(Type type, string path);
    }
}
