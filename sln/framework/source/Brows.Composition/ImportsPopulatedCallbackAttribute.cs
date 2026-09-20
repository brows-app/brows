using System;

namespace Brows;

/// <summary>
/// Marks a method to be called after imports are populated.
/// Methods marked with this attribute should be instance 
/// methods that accept 0 (zero) parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class ImportsPopulatedCallbackAttribute : Attribute {
}
