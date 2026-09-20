using System;

namespace Brows;

/// <summary>
/// Marks a method to be called after imports are ready.
/// Methods marked with this attribute should be instance 
/// methods that accept 0 (zero) parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class ImportsReadyCallbackAttribute : Attribute {
}
