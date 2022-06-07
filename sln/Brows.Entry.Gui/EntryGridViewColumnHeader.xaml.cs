using System.Collections.Generic;
using System.Windows;

namespace Brows {
    using Translation;

    partial class EntryGridViewColumnHeader {
        private static ITranslation Translate =>
            _Translate ?? (
            _Translate = Global.Translation);
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
            if (e.Property == KeyProperty) {
                foreach (var key in ResourceKeys()) {
                    var content = Translate.Value(key);
                    if (content != null) {
                        Content = content;
                        break;
                    }
                }
            }
            base.OnPropertyChanged(e);
        }

        public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
            name: nameof(Key),
            propertyType: typeof(string),
            ownerType: typeof(EntryGridViewColumnHeader));

        public string Key {
            get => (string)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        public IComponentResourceKey Resolver { get; }

        public EntryGridViewColumnHeader(IComponentResourceKey resolver) {
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
