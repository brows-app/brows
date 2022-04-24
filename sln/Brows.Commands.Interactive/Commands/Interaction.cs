using System;

namespace Brows.Commands {
    public class Interaction {
        public int IndicatorCount {
            get {
                if (_IndicatorCount == null) {
                    _IndicatorCount = 0;
                    foreach (var c in Input) {
                        if (c == Indicator) {
                            _IndicatorCount++;
                        }
                        else {
                            break;
                        }
                    }
                }
                return _IndicatorCount.Value;
            }
        }
        private int? _IndicatorCount;

        public string Directive {
            get {
                if (_Directive == null) {
                    _Directive = Input.Substring(IndicatorCount);
                }
                return _Directive;
            }
        }
        private string _Directive;

        public bool Indicated =>
            IndicatorCount > 0;

        public string Input { get; }
        public char Indicator { get; }

        public Interaction(string input, char indicator) {
            if (null == input) throw new ArgumentNullException(nameof(input));
            Input = input;
            Indicator = indicator;
        }
    }
}
