using System;

namespace Brows.Triggers;

/// <summary>
/// Identifies a user gesture that can be compared with another gesture.
/// </summary>
public interface IGesture : IEquatable<IGesture> {
}
