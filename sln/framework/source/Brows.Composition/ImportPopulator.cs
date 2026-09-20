using Brows.Composition;
using Brows.Composition.Exports;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brows;

/// <summary>
/// Instances of this type populate target objects with appropriate instances
/// of <see cref="IExport"/>.
/// </summary>
internal sealed class ImportPopulator {
    private static readonly ConcurrentDictionary<PropertyInfo, bool> ImportRequiredAttribute = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> ImportCollectionFindMethod = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> ImportCollectionListMethod = [];
    private static readonly HashSet<Type> ImportCollectionTypes = [
        typeof(IEnumerable<>),
        typeof(IReadOnlyList<>),
        typeof(IReadOnlyCollection<>)];

    private IEnumerable<PropertyInfo> GetTargetProperties() {
        var targetProperties = TargetType.GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var targetProperty in targetProperties) {
            var indexParameters = targetProperty.GetIndexParameters();
            if (indexParameters is not null && indexParameters.Length > 0) {
                continue;
            }
            var property = targetProperty;
            for (; ; ) {
                if (property is null) {
                    break;
                }
                /*
                 * We're looking for the property that has both a setter and a getter
                 * (neither has to be public). If a derived type is being populated,
                 * and the base type does not expose the setter of a property, then
                 * that property of the derived type can't be used to set a value on 
                 * an instance of the derived type; you have to use the base type's
                 * property.
                 */
                var setMethod = property.GetSetMethod(nonPublic: true);
                var getMethod = property.GetGetMethod(nonPublic: true);
                if (getMethod is not null && setMethod is not null) {
                    yield return property;
                    break;
                }
                try {
                    property = property.ReflectedType?.BaseType?.GetProperty(property.Name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                catch (AmbiguousMatchException) {
                    /*
                     * This will be thrown when using the `new` keyword for properties
                     * (and maybe in other cases, too).
                     */
                    break;
                }
            }
        }
    }

    private IEnumerable<PropertyInfo> GetTargetImports() {
        foreach (var targetProperty in TargetProperties) {
            var propertyType = targetProperty.PropertyType;
            var isImport = typeof(IExport).IsAssignableFrom(propertyType);
            if (isImport) {
                yield return targetProperty;
            }
            var isSpecial = typeof(IImportAgent) == propertyType;
            if (isSpecial) {
                yield return targetProperty;
            }
        }
    }

    private IEnumerable<PropertyInfo> GetTargetImportCollections() {
        foreach (var targetProperty in TargetProperties) {
            var propertyType = targetProperty.PropertyType;
            var isGeneric = propertyType.IsConstructedGenericType;
            if (isGeneric) {
                var genericTypeDef = propertyType.GetGenericTypeDefinition();
                var maybeImportCollection = ImportCollectionTypes.Contains(genericTypeDef);
                if (maybeImportCollection) {
                    var genericArg = propertyType.GetGenericArguments()[0];
                    var isImportCollection = typeof(IExport).IsAssignableFrom(genericArg);
                    if (isImportCollection) {
                        /*
                         * This property's type is something like the following:
                         * - IEnumerable<IAmAnExport>
                         * - IReadOnlyList<IAmADifferentExport>
                         * 
                         * We'll eventually populate the property with a collection
                         * of the desired type.
                         */
                        yield return targetProperty;
                    }
                }
            }
        }
    }

    /// <summary>
    /// These are simple, un-indexed properties with both a getter and a setter.
    /// These are the only members that will be populated.
    /// </summary>
    /// <remarks>
    /// A getter is required so we can check the current value of the property.
    /// We don't populate (i.e. overwrite) properties that have values.
    /// </remarks>
    private IReadOnlyList<PropertyInfo> TargetProperties => field ??=
        [.. GetTargetProperties()];

    /// <summary>
    /// These are properties whose type extends <see cref="IExport"/>.
    /// These properties may be populated with imported instances of
    /// exports.
    /// </summary>
    private IReadOnlyList<PropertyInfo> TargetImports => field ??=
        [.. GetTargetImports()];

    /// <summary>
    /// These are properties whose type is a collection of instances of
    /// <see cref="IExport"/>. These properties may be populated with
    /// collections of exports.
    /// </summary>
    private IReadOnlyList<PropertyInfo> TargetLists => field ??=
        [.. GetTargetImportCollections()];

    private IReadOnlyList<MethodInfo> Callbacks => field ??=
        [.. ExportExtension.GetImportsPopulatedCallbacks(TargetType)];

    private static bool IsImportRequired(PropertyInfo property) {
        if (null == property) throw new ArgumentNullException(nameof(property));
        return ImportRequiredAttribute.GetOrAdd(property, property => {
            var attribute = property
                .GetCustomAttributes()
                .OfType<ImportPopulationAttribute>()
                .FirstOrDefault();
            return attribute?.ImportRequired ?? false;
        });
    }

    private static bool ShouldPopulate(object target, PropertyInfo property, out bool required) {
        if (null == property) throw new ArgumentNullException(nameof(property));
        var requiredEx = default(ImportRequiredException);
        var currentValue = default(object);
        try {
            currentValue = property.GetValue(target);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is ImportRequiredException innerEx) {
            requiredEx = innerEx;
        }
        var r = required = requiredEx is not null || IsImportRequired(property);
        if (r) {
            return true;
        }
        return currentValue is null;
    }

    /// <summary>
    /// Gets the target type of the populator.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Creates a new instance of a populator.
    /// </summary>
    /// <param name="targetType">The type of objects to be populated.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="targetType"/> is null.</exception>
    public ImportPopulator(Type targetType) {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    /// <summary>
    /// Populates the target with items in the collection.
    /// </summary>
    /// <param name="target">The target object to be populated.</param>
    /// <param name="from">The source collection that contains items from which the properties of
    /// <paramref name="target"/> are populated.</param>
    /// <param name="agent">The import agent.</param>
    /// <exception cref="ArgumentNullException">Thrown if either <paramref name="target"/> or <paramref name="from"/> is null.</exception>
    public void Populate(object target, ImportCollection from, IImportAgent agent) {
        if (null == from) throw new ArgumentNullException(nameof(from));
        if (null == target) throw new ArgumentNullException(nameof(target));
        foreach (var targetImport in TargetImports) {
            void setValue(object value) {
                var flag = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var name = targetImport.Name;
                var type = targetImport.DeclaringType;
                var prop = type.GetProperty(name, flag);
                prop.SetValue(target, value);
            }
            if (targetImport.PropertyType == typeof(IImportAgent)) {
                setValue(agent);
                continue;
            }
            var shouldPopulate = ShouldPopulate(target, targetImport, out var required);
            if (shouldPopulate) {
                var exportType = targetImport.PropertyType;
                var exportFunc = ImportCollectionFindMethod.GetOrAdd(
                    exportType,
                    exportType => typeof(ImportCollection)
                        .GetMethod(nameof(from.Find), Type.EmptyTypes)
                        .MakeGenericMethod(exportType));
                var export = exportFunc.Invoke(from, null);
                if (export is null) {
                    if (required) {
                        throw new ImportRequiredException(target: target, propertyName: targetImport.Name);
                    }
                }
                else {
                    setValue(export);
                }
            }
        }
        foreach (var targetList in TargetLists) {
            var shouldPopulate = ShouldPopulate(target, targetList, out _);
            if (shouldPopulate) {
                var exportType = targetList.PropertyType.GetGenericArguments()[0];
                var exportFunc = ImportCollectionListMethod.GetOrAdd(
                    exportType,
                    exportType => typeof(ImportCollection)
                        .GetMethod(nameof(from.List), Type.EmptyTypes)
                        .MakeGenericMethod(exportType));
                var exports = exportFunc.Invoke(from, null);
                targetList.SetValue(target, exports);
            }
        }
        foreach (var callback in Callbacks) {
            callback.Invoke(target, null);
        }
    }
}
