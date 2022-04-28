using System.Collections.Generic;
using System.Windows.Controls;

namespace Brows.Windows {
    public class AppComponentDataTemplateSelectorCollection {
        private readonly Dictionary<string, DataTemplateSelector> Dictionary = new Dictionary<string, DataTemplateSelector>();

        public DataTemplateSelector this[string kind] {
            get {
                if (Dictionary.TryGetValue(kind, out var value) == false) {
                    Dictionary[kind] = value = new AppComponentDataTemplateSelector(kind);
                }
                return value;
            }
        }
    }
}
