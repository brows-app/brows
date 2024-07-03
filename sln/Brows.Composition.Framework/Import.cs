using Brows.Composition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brows {
    internal sealed class Import : IImport {
        private readonly Dictionary<Type, Import> CacheOf = [];
        private readonly Dictionary<Type, Import> CacheFor = [];
        private readonly Dictionary<Type, object> CacheGet = [];
        private readonly Dictionary<Type, IReadOnlyList<object>> CacheList = [];
        private readonly Dictionary<Type, Dictionary<PropertyInfo, object>> CachePopulate = [];

        private Import Of(Type type) {
            lock (CacheOf) {
                if (CacheOf.TryGetValue(type, out var value) == false) {
                    CacheOf[type] = value = new Import(Export
                        .Where(export => export.Implements(type))
                        .ToList());
                }
                return value;
            }
        }

        private Import For(Type type) {
            lock (CacheFor) {
                if (CacheFor.TryGetValue(type, out var value) == false) {
                    CacheFor[type] = value = new Import(Export
                        .Where(export => export.Targets(type))
                        .ToList());
                }
                return value;
            }
        }

        private object Get(Type type) {
            lock (CacheGet) {
                if (CacheGet.TryGetValue(type, out var value) == false) {
                    CacheGet[type] = value = Of(type)
                        .Export
                        .Select(export => export.Instance)
                        .Where(instance => instance != null)
                        .FirstOrDefault();
                }
                return value;
            }
        }

        private IReadOnlyList<object> List(Type type) {
            lock (CacheList) {
                if (CacheList.TryGetValue(type, out var value) == false) {
                    CacheList[type] = value = Of(type)
                        .Export
                        .Select(export => export.Instance)
                        .Where(instance => instance != null)
                        .ToList();
                }
                return value;
            }
        }

        public IReadOnlyList<Export> Export { get; }

        public Import(IReadOnlyList<Export> export) {
            Export = export ?? throw new ArgumentNullException(nameof(export));
        }

        public void Populate(object obj) {
            var type = obj?.GetType();
            if (type != null) {
                Dictionary<PropertyInfo, object> dict() {
                    lock (CachePopulate) {
                        if (CachePopulate.TryGetValue(type, out var value) == false) {
                            CachePopulate[type] = value = type.GetProperties()
                                .Where(p =>
                                    p.CanWrite &&
                                    p.GetIndexParameters().Length == 0 &&
                                    p.PropertyType.IsAssignableTo(typeof(IExport)))
                                .ToDictionary(
                                    p => p,
                                    p => For(type).Get(p.PropertyType) ?? Get(p.PropertyType));
                        }
                        return value;
                    }
                }
                foreach (var item in dict()) {
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
