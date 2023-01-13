using System.Windows;
using System.Windows.Documents;

namespace Brows.Windows.Controls {
    partial class KeyboardTriggerControl {
        private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            TextBlock.Inlines.Clear();

            var newValue = e.NewValue;
            if (newValue != null) {
                if (newValue is PressGesture press) {
                    var s = press.ToString();
                    var keys = s.Split('+');
                    foreach (var key in keys) {
                        if (TextBlock.Inlines.Count > 0) {
                            TextBlock.Inlines.Add(new Run { Text = "+" });
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

        public KeyboardTriggerControl() {
            DataContextChanged += This_DataContextChanged;
            InitializeComponent();
        }
    }
}
