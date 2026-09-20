using Brows.Composition;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Brows.Windows;

public sealed class AppComponentCollection : IEnumerable<AppComponent> {
    private readonly IReadOnlyList<AppComponent> Agent;

    private AppComponentCollection(IEnumerable<AppComponent> collection) {
        ArgumentNullException.ThrowIfNull(collection);
        Agent = [.. collection.Where(item => item is not null)];
    }

    public static AppComponentCollection From(IImport imported) {
        ArgumentNullException.ThrowIfNull(imported);
        return new AppComponentCollection(imported
            .List<IExport>()
            .Select(item => item?.GetType()?.Assembly)
            .Where(assembly => assembly is not null)
            .Distinct()
            .Select(assembly => new AppComponent(assembly)));
    }

    public IEnumerable<ResourceDictionary> ResourceDictionaries {
        get {
            foreach (var component in Agent) {
                foreach (var dictionary in component.ResourceDictionaries) {
                    yield return dictionary;
                }
            }
        }
    }

    public IEnumerator<AppComponent> GetEnumerator() {
        return Agent.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
