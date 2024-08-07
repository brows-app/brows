﻿using System.Collections.Generic;
using System.Windows.Controls;

namespace Brows.Windows {
    public sealed class AppComponentDataTemplateSelectorCollection {
        private readonly Dictionary<string, DataTemplateSelector> Dictionary = [];

        public DataTemplateSelector this[string kind] {
            get {
                lock (Dictionary) {
                    if (Dictionary.TryGetValue(kind, out var value) == false) {
                        Dictionary[kind] = value = new AppComponentDataTemplateSelector(kind);
                    }
                    return value;
                }
            }
        }
    }
}
