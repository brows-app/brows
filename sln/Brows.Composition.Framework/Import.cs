using Brows.Composition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brows {
    internal sealed class Import : IImport {
        private readonly Dictionary<Type, Import> CacheOf = new();
        private readonly Dictionary<Type, Import> CacheFor = new();
        private readonly Dictionary<Type, object> CacheGet = new();
        private readonly Dictionary<Type, IReadOnlyList<object>> CacheList = new();
        private readonly Dictionary<Type, Dictionary<PropertyInfo, object>> CachePopulate = new();

        private Import Of(Type type) {
            var cache = CacheOf;
            if (cache.TryGetValue(type, out var value) == false) {
                cache[type] = value = new Import(Export
                    .Where(export => export.Implements(type))
                    .ToList());
            }
            return value;
        }

        private Import For(Type type) {
            var cache = CacheFor;
            if (cache.TryGetValue(type, out var value) == false) {
                cache[type] = value = new Import(Export
                    .Where(export => export.Targets(type))
                    .ToList());
            }
            return value;
        }

        private object Get(Type type) {
            var cache = CacheGet;
            if (cache.TryGetValue(type, out var value) == false) {
                cache[type] = value = Of(type)
                    .Export
                    .Select(export => export.Instance)
                    .Where(instance => instance != null)
                    .FirstOrDefault();
            }
            return value;
        }

        private IReadOnlyList<object> List(Type type) {
            var cache = CacheList;
            if (cache.TryGetValue(type, out var value) == false) {
                cache[type] = value = Of(type)
                    .Export
                    .Select(export => export.Instance)
                    .Where(instance => instance != null)
                    .ToList();
            }
            return value;
        }

        public IReadOnlyList<Export> Export { get; }

        public Import(IReadOnlyList<Export> export) {
            Export = export ?? throw new ArgumentNullException(nameof(export));
        }

        public void Populate(object obj) {
            if (obj != null) {
                var type = obj.GetType();
                var cache = CachePopulate;
                if (cache.TryGetValue(type, out var value) == false) {
                    cache[type] = value = type.GetProperties()
                        .Where(p =>
                            p.CanWrite &&
                            p.GetIndexParameters().Length == 0 &&
                            p.PropertyType.IsAssignableTo(typeof(IExport)))
                        .ToDictionary(
                            p => p,
                            p => For(type).Get(p.PropertyType) ?? Get(p.PropertyType));
                }
                foreach (var item in value) {
                    item.Key.SetValue(obj, item.Value);
                }
            }
        }

        public void Populate() {
            Export.ToList().ForEach(item => {
                Populate(item.Instance);
            });
        }

        IImport IImport.For(Type type) => For(type);
        TService IImport.Get<TService>() => (TService)Get(typeof(TService));
        IReadOnlyList<TService> IImport.List<TService>() => List(typeof(TService))
            .Cast<TService>()
            .ToList();
    }
}
