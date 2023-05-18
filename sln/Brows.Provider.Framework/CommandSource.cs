using System;
using System.Collections;

namespace Brows {
    public sealed class CommandSource : ICommandSource {
        private ICommandSourceObject SourceObject =>
            _SourceObject ?? (
            _SourceObject = Object as ICommandSourceObject);
        private ICommandSourceObject _SourceObject;

        public object Item =>
            _Item ?? (
            _Item = SourceObject?.Instance);
        private object _Item;

        public IEnumerable Items =>
            _Items ?? (
            _Items = SourceObject?.Collection);
        private IEnumerable _Items;

        public object Object { get; }

        public Type Type =>
            _Type ?? (
            _Type = Object?.GetType());
        private Type _Type;

        public CommandSource(object @object) {
            Object = @object;
        }
    }
}
