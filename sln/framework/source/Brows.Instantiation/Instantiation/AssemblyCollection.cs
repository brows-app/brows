using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Brows.Instantiation;

internal sealed class AssemblyCollection : IEnumerable<Assembly> {
    private readonly ReadOnlyCollection<Assembly> Agent;
    private readonly Lazy<ReadOnlyCollection<Type>> Factory;

    public ReadOnlyCollection<Type> Types => field ??= Factory.Value;

    public AssemblyCollection(IEnumerable<Assembly> items) {
        if (null == items) throw new ArgumentNullException(nameof(items));
        Agent = items.Where(item => item != null).ToList().AsReadOnly();
        Factory = new(
            mode: LazyThreadSafetyMode.PublicationOnly,
            valueFactory: () => Agent
                .SelectMany(assembly => assembly.GetTypes())
                .ToList()
                .AsReadOnly());
    }

    public IEnumerator<Assembly> GetEnumerator() {
        return Agent.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
