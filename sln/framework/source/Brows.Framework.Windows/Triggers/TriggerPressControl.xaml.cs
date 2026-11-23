using Brows.Localization;
using System.Windows;
using System.Windows.Documents;

namespace Brows.Triggers;

partial class TriggerPressControl {
    private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        TextBlock.Inlines.Clear();

        var translation = Translation;
        if (translation is null) {
            return;
        }
        var newValue = e.NewValue;
        if (newValue != null) {
            if (newValue is PressGesture press) {
                var s = press.Display(translation);
                var d = press.Delimiter(translation);
                var keys = s.Split(d);
                foreach (var key in keys) {
                    if (TextBlock.Inlines.Count > 0) {
                        TextBlock.Inlines.Add(new Run { Text = d.ToString() });
                    }
                    TextBlock.Inlines.Add(new Run {
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        Text = key
                    });
                }
            }
        }
    }

    internal ITranslationExport Translation { get; set; }

    public TriggerPressControl() {
        Imports.Current.Populate(this);
        DataContextChanged += This_DataContextChanged;
        InitializeComponent();
    }
}
