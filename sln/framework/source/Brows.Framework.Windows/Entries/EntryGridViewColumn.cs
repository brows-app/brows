using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Entries;

internal sealed class EntryGridViewColumn : GridViewColumn {
    private bool Applying;

    internal EntryColumnSizing Owner { get; set; }
    internal SizingMode Mode { get; set; }
    internal double Hint { get; }
    internal double HeaderWidth { get; set; }
    internal double NaturalWidth { get; set; }
    internal double ContentWidth { get; set; }
    internal bool HasValue { get; set; }
    internal double Preferred => Math.Max(HeaderWidth, HasValue ? NaturalWidth : Hint);

    internal void SetWidth(double value) {
        Applying = true;
        try {
            Width = value;
        }
        finally {
            Applying = false;
        }
    }

    internal void Reset(bool restoreMode = false) {
        if (restoreMode) {
            Mode = SizingMode.Adaptive;
        }
        HeaderWidth = NaturalWidth = ContentWidth = 0;
        HasValue = false;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        var manual = e.Property == WidthProperty && !Applying;
        if (manual) {
            Mode = double.IsNaN(Width) ? SizingMode.Content : SizingMode.Fixed;
            Owner?.RequestFit(this, Mode == SizingMode.Content);
        }
    }

    public string DataKey { get; }

    public EntryGridViewColumn(string dataKey, double hint = double.NaN) {
        DataKey = dataKey;
        Hint = double.IsFinite(hint) ? Math.Max(0, hint) : 80;
        SetWidth(Hint);
    }

    internal enum SizingMode {
        Adaptive,
        Content,
        Fixed
    }
}
