using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Triggers {
    internal sealed class InputTriggerCollection : IInputTriggerCollection {
        private readonly IReadOnlyCollection<IInputTrigger> Agent;

        private InputTrigger[] MainRef => _MainRef ??= [MakeMain()];
        private InputTrigger[] _MainRef;

        private InputTrigger MakeMain() {
            var inputs = Agent.Where(item => string.IsNullOrWhiteSpace(item.Defined));
            var input = inputs.Where(item => !string.IsNullOrWhiteSpace(item.Input)).FirstOrDefault()?.Input;
            var alias = inputs
                .SelectMany(item => item.Options)
                .Except(new[] { input }, StringComparer.CurrentCultureIgnoreCase)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            var main = input ?? alias.FirstOrDefault();
            if (main == null) {
                return null;
            }
            return new InputTrigger(null, main, alias.ToArray());
        }

        public int Count =>
            Agent.Count;

        public IInputTrigger Main =>
            MainRef[0];

        public InputTriggerCollection(IEnumerable<IInputTrigger> items) {
            ArgumentNullException.ThrowIfNull(items);
            Agent = items
                .Where(item => item != null)
                .ToList();
        }

        public IEnumerator<IInputTrigger> GetEnumerator() {
            return Agent.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
