using System;
using System.IO;
using System.Linq;

namespace Brows.Data {
    internal class BookmarkDataFile : DataFile {
        public override string Extension => "txt";

        public override void Save(object data, Type type, string path) {
            var model = (BookmarkCollection)data;
            var items = string.Join(Environment.NewLine, model
                .Items
                .Select(i => $"{i.Key} > {i.Value}"));
            File.WriteAllText(path, items);
        }

        public override object Load(Type type, string path) {
            var lines = File.ReadAllLines(path);
            var items = lines
                .Select(line => line.Trim())
                .Where(line => line != "")
                .Select(line => line.Split('>'))
                .Where(pair => pair.Length == 2)
                .Select(pair => BookmarkItem.Create(pair[0], pair[1]))
                .ToList();
            return new BookmarkCollection(items);
        }
    }
}
