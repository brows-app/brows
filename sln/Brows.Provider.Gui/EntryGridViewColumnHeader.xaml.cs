using System.Collections.Generic;
using System.Windows;

namespace Brows {
    partial class EntryGridViewColumnHeader {
        private static ITranslation Translate =>
            _Translate ?? (
            _Translate = Translation.Global);
        private static ITranslation _Translate;

        private IEnumerable<string> ResourceKeys() {
            var key = Key;
            var resolver = Resolver;
            if (resolver != null) {
                yield return resolver.For($"{key}");
            }
            yield return $"EntryData_{key}";
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            if (e.Property == KeyProperty || e.Property == DisplayProperty) {
                var display = Display;
                if (display != null) {
                    Content = display;
                }
                else {
                    foreach (var key in ResourceKeys()) {
                        var content = Translate.Value(key);
                        if (content != null) {
                            Content = content;
                            break;
                        }
                    }
                    if (Content == null) {
                        Content = Key;
                    }
                }
            }
            base.OnPropertyChanged(e);
        }

        public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
            name: nameof(Key),
            propertyType: typeof(string),
            ownerType: typeof(EntryGridViewColumnHeader));

        public static readonly DependencyProperty DisplayProperty = DependencyProperty.Register(
            name: nameof(Display),
            propertyType: typeof(string),
            ownerType: typeof(EntryGridViewColumnHeader));

        public string Key {
            get => GetValue(KeyProperty) as string;
            set => SetValue(KeyProperty, value);
        }

        public string Display {
            get => GetValue(DisplayProperty) as string;
            set => SetValue(DisplayProperty, value);
        }

        public IExportResourceKey Resolver { get; }

        public EntryGridViewColumnHeader(IExportResourceKey resolver) {
            Resolver = resolver;
            InitializeComponent();
        }

        public EntryGridViewColumnHeader() : this(null) {
        }

        public void Sorting(EntrySortDirection? direction) {
            const string upArrow = " ▲"; // "🠕" 
            const string downArrow = " ▼"; //"🠗"
            var headerArrow = direction == null
                ? ""
                : direction == EntrySortDirection.Ascending
                    ? upArrow
                    : downArrow;
            var
            header = Content?.ToString() ?? "";
            header = header.Replace(upArrow, "");
            header = header.Replace(downArrow, "");
            header = header + headerArrow;
            Content = header;
        }
    }
}
