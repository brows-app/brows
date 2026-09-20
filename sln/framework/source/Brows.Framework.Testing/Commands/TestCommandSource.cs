using System.Collections;
using System.Collections.Generic;

namespace Brows.Commands;

/// <summary>
/// An <see cref="ICommandSource"/> whose item and items are set by tests.
/// </summary>
public sealed class TestCommandSource : ICommandSource {
    /// <summary>
    /// The items of the source. The first item is used as <see cref="Item"/>
    /// unless one is set explicitly.
    /// </summary>
    public IList<object> Items { get; } = [];

    /// <summary>
    /// The item of the source. Defaults to the first of <see cref="Items"/>.
    /// </summary>
    public object Item {
        get {
            if (field != null) {
                return field;
            }
            foreach (var item in Items) {
                return item;
            }
            return null;
        }
        set;
    }

    IEnumerable ICommandSource.Items => Items;
}
