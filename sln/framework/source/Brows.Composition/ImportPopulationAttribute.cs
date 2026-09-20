using System;

namespace Brows;

/// <summary>
/// Decorates properties whose values are composed via imports.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public abstract class ImportPopulationAttribute : Attribute {
    public bool ImportRequired { get; }

    public ImportPopulationAttribute(bool importRequired) {
        ImportRequired = importRequired;
    }
}
