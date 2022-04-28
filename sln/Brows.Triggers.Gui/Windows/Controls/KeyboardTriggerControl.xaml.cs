using System.Windows;
using System.Windows.Documents;

namespace Brows.Windows.Controls {
    using Triggers;

    partial class KeyboardTriggerControl {
        private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            TextBlock.Inlines.Clear();

            var newValue = e.NewValue;
            if (newValue != null) {
                var keyboardGesture = newValue is KeyboardGesture kg
                    ? kg
                    : newValue is KeyboardTrigger kt
                        ? kt.Gesture
                        : default(KeyboardGesture?);
                if (keyboardGesture.HasValue) {
                    var s = keyboardGesture.ToString();
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
