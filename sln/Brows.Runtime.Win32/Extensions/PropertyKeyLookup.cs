using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brows.Extensions {
    internal class PropertyKeyLookup {
        public static readonly IReadOnlyDictionary<string, PropertyKeyProxy> Name = typeof(PropertyKey)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(PropertyKey))
            .ToDictionary(field => field.Name, field => new PropertyKeyProxy(field.Name, (PropertyKey)field.GetValue(typeof(PropertyKey))));

        public static readonly IReadOnlyDictionary<Tuple<Guid, int>, PropertyKeyProxy> ID = Name
            .Select(n => n.Value)
            .DistinctBy(p => p.ID)
            .ToDictionary(p => p.ID, p => p);

        public static readonly IReadOnlyList<string> Names = new List<string>(Name.Keys);
    }
}
