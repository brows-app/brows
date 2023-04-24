﻿using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows {
    partial class EntryProviderGrid {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(object), typeof(EntryProviderGrid));
        public static readonly DependencyProperty IconTemplateProperty = DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(EntryProviderGrid));

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(object), typeof(EntryProviderGrid));
        public static readonly DependencyProperty TextTemplateProperty = DependencyProperty.Register(nameof(TextTemplate), typeof(DataTemplate), typeof(EntryProviderGrid));

        public object Icon {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public DataTemplate IconTemplate {
            get => GetValue(IconTemplateProperty) as DataTemplate;
            set => SetValue(IconTemplateProperty, value);
        }

        public object Text {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public DataTemplate TextTemplate {
            get => GetValue(TextTemplateProperty) as DataTemplate;
            set => SetValue(TextTemplateProperty, value);
        }

        public EntryProviderGrid() {
            InitializeComponent();
            EntryNameGrid.DataContext = this;
            DetailContentControl.ContentTemplateSelector = new DetailTemplateSelectorDefault(this);
        }

        private sealed class DetailTemplateSelectorDefault : DataTemplateSelector {
            public EntryProviderGrid Element { get; }

            public DetailTemplateSelectorDefault(EntryProviderGrid element) {
                Element = element ?? throw new ArgumentNullException(nameof(element));
            }

            public sealed override DataTemplate SelectTemplate(object item, DependencyObject container) {
                if (item == null) {
                    return null;
                }
                var key = item.GetType();
                var resource = Element.FindResource(key);
                var template = resource as DataTemplate;
                return template;
            }
        }
    }
}
