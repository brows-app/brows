using System;

namespace Brows;

/// <summary>
/// Properties decorated with this attribute are NOT required to have an associated import
/// when populated. If an appropriate import does not exist for the property, no exception
/// is thrown.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ImportOptionalAttribute : ImportPopulationAttribute {
    public ImportOptionalAttribute() : base(importRequired: false) {
    }
}
