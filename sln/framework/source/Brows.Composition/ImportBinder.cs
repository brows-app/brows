using Brows.Composition;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

internal sealed class ImportBinder {
    private IEnumerable<object> Enumerate(Type importType) {
        if (null == importType) throw new ArgumentNullException(nameof(importType));
        foreach (var import in Imports) {
            if (importType.IsAssignableFrom(import.GetType())) {
                yield return import;
            }
        }
    }

    private object ToArray(Type elementType) {
        var src = Enumerate(elementType).ToArray();
        var dest = (Array)Activator.CreateInstance(elementType.MakeArrayType(), src.Length);
        Array.Copy(src, dest, src.Length);
        return dest;
    }

    private object ToList(Type itemType) {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        var imports = Enumerate(itemType);
        foreach (var import in imports) {
            list.Add(import);
        }
        return list;
    }

    public IReadOnlyList<IExport> Imports { get; }

    public ImportBinder(IEnumerable<IExport> imports) {
        Imports = imports
            ?.Where(import => import is not null)
            ?.ToList() ?? [];
    }

    public object Bind(Type type) {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (type.IsArray) {
            if (type.GetArrayRank() == 1) {
                var elementType = type.GetElementType();
                var elements = ToArray(elementType);
                return elements;
            }
        }
        if (typeof(IEnumerable).IsAssignableFrom(type)) {
            var genericType = type.GetGenericTypeDefinition();
            var genericTypesAllowed = new[] {
                typeof(IEnumerable<>),
                typeof(ICollection<>),
                typeof(IList<>),
                typeof(List<>),
                typeof(IReadOnlyCollection<>),
                typeof(IReadOnlyList<>)
            };
            if (genericTypesAllowed.Contains(genericType)) {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length == 1) {
                    var itemType = genericArgs[0];
                    if (typeof(IExport).IsAssignableFrom(itemType)) {
                        var items = ToList(itemType);
                        return items;
                    }
                }
            }
        }
        if (typeof(IExport).IsAssignableFrom(type)) {
            return
                Imports.FirstOrDefault(import => import.GetType() == type) ??
                Imports.FirstOrDefault(import => type.IsAssignableFrom(import.GetType()));
        }
        return null;
    }
}
