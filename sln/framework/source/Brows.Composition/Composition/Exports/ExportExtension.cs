using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brows.Composition.Exports;

internal static class ExportExtension {
    private static IEnumerable<MethodInfo> GetCallbacks(Type objectType, Type attributeType) {
        static IEnumerable<Type> getHierarchy(Type type) {
            for (var t = type; t is not null; t = t.BaseType) {
                yield return t;
            }
        }
        var hierarchy = getHierarchy(objectType).Reverse();
        var methodFlags =
            BindingFlags.DeclaredOnly |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance;
        foreach (var type in hierarchy) {
            var methods = type
                .GetMethods(methodFlags)
                .Where(method => method.GetCustomAttribute(attributeType) is not null);
            foreach (var method in methods) {
                yield return method;
            }
        }
    }

    internal static IEnumerable<MethodInfo> GetImportsReadyCallbacks(Type objectType) =>
        GetCallbacks(objectType, typeof(ImportsReadyCallbackAttribute));

    internal static IEnumerable<MethodInfo> GetImportsPopulatedCallbacks(Type objectType) =>
        GetCallbacks(objectType, typeof(ImportsPopulatedCallbackAttribute));
}
