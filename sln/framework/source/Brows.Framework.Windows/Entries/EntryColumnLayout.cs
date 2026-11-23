using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Entries;

internal sealed class EntryColumnLayout {
    private static double Finite(double value) {
        return double.IsFinite(value) ? Math.Max(0, value) : 0;
    }

    internal static double[] Allocate(IReadOnlyList<Column> columns, double available, double scale = 1) {
        var widths = new double[columns.Count];
        if (!double.IsFinite(available)) {
            return widths;
        }
        available = Math.Max(0, available);
        var hasPositiveRequest = false;
        var automatic = new List<int>();
        var minimum = new double[columns.Count];
        for (var i = 0; i < columns.Count; i++) {
            var column = columns[i];
            if (column.Requested is double requested) {
                widths[i] = Finite(requested);
                hasPositiveRequest |= widths[i] > 0;
                available -= widths[i];
                continue;
            }
            var floor = automatic.Count == 0 ? 120 : Math.Max(48, Math.Min(Finite(column.Header), 96));
            minimum[i] = Math.Min(Finite(column.Preferred), floor);
            automatic.Add(i);
        }
        if (automatic.Count == 0) {
            return widths;
        }
        available = Math.Max(0, available);
        var sum = automatic.Sum(i => minimum[i]);
        var needsHiding = automatic.Count > 1 && sum > available;
        while (needsHiding) {
            var last = automatic[^1];
            sum -= minimum[last];
            automatic.RemoveAt(automatic.Count - 1);
            needsHiding = automatic.Count > 1 && sum > available;
        }
        var primary = automatic[0];
        if (sum > available) {
            widths[primary] = hasPositiveRequest ? minimum[primary] : available;
        }
        else {
            foreach (var i in automatic) {
                widths[i] = minimum[i];
            }
            var extra = available - sum;
            var ceiling = Math.Max(200, 0.25 * available);
            var targets = automatic.Skip(1)
                .Select(i => (Index: i, Width: Math.Max(minimum[i], Math.Min(Finite(columns[i].Preferred), ceiling))))
                .OrderBy(target => target.Width);
            foreach (var target in targets) {
                var addition = Math.Min(extra, target.Width - widths[target.Index]);
                widths[target.Index] += addition;
                extra -= addition;
            }
            widths[primary] += extra;
        }
        var validScale = double.IsFinite(scale) && scale > 0;
        scale = validScale ? scale : 1;
        var remainder = available;
        foreach (var i in automatic) {
            widths[i] = Math.Floor(widths[i] * scale) / scale;
            remainder -= widths[i];
        }
        widths[primary] += Math.Max(0, Math.Floor((remainder + 0.000001) * scale) / scale);
        return widths;
    }

    internal sealed record Column(double Preferred, double Header, double? Requested = null);
}
