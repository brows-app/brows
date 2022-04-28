using System;
using System.Collections.Generic;

namespace Brows {
    public abstract class CommandContextData : ICommandContextData {
        public abstract object Current { get; }

        public virtual string Input { get; }
        public virtual ICommandContextFlag Flag { get; protected set; }
        public ICommand Command { get; }

        public CommandContextData(ICommand command) {
            Command = command;
        }

        public virtual ICommandContextData Remove() {
            return this;
        }

        public virtual ICommandContextData Clear() {
            return this;
        }

        public virtual ICommandContextData Next() {
            return this;
        }

        public virtual ICommandContextData Previous() {
            return this;
        }

        public virtual ICommandContextData Enter() {
            return this;
        }
    }

    public abstract class CommandContextData<T> : CommandContextData {
        protected abstract CommandContextData<T> Create(int index, IList<T> list);

        protected CommandContextData(ICommand command, int index, IList<T> list) : base(command) {
            Index = index;
            List = list ?? throw new ArgumentNullException(nameof(list));
        }

        protected virtual void Removed(T item) {
        }

        protected virtual void Cleared(IEnumerable<T> items) {
        }

        public override object Current =>
            Item;

        public int Index { get; }
        public IList<T> List { get; }

        public T Item =>
            _Item ?? (
            _Item = List.Count > Index
                ? List[Index]
                : default(T));
        private T _Item;

        public override ICommandContextData Remove() {
            var i = Index;
            var item = Item;
            List.RemoveAt(i);
            Removed(item);
            while (List.Count <= i) {
                i--;
            }
            if (i >= 0) {
                return Create(i, List);
            }
            return null;
        }

        public override ICommandContextData Clear() {
            var items = new List<T>(List);
            List.Clear();
            Cleared(items);
            return null;
        }

        public override ICommandContextData Next() {
            var next = Index + 1;
            if (List.Count > next) {
                return Create(next, List);
            }
            return Create(0, List);
        }

        public override ICommandContextData Previous() {
            var prev = Index - 1;
            if (prev >= 0) {
                return Create(prev, List);
            }
            return Create(List.Count - 1, List);
        }
    }
}
