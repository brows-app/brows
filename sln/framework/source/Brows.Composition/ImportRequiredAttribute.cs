using System;

namespace Brows;

/// <summary>
/// Properties decorated with this attribute are required to have an associated import
/// when populated. If an appropriate import does not exist for the property, an exception
/// is thrown.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ImportRequiredAttribute : ImportPopulationAttribute {
    public ImportRequiredAttribute() : base(importRequired: true) {
    }
}
