using System.Windows;
using System.Windows.Documents;

namespace Brows {
    partial class TriggerPressControl {
        private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            TextBlock.Inlines.Clear();

            var newValue = e.NewValue;
            if (newValue != null) {
                if (newValue is PressGesture press) {
                    var s = press.Display();
                    var d = press.Delimiter();
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

        public TriggerPressControl() {
            DataContextChanged += This_DataContextChanged;
            InitializeComponent();
        }
    }
}
