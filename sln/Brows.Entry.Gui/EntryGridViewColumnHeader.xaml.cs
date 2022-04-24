using System.Windows;

namespace Brows {
    using Globalization;

    partial class EntryGridViewColumnHeader {
        private ITranslation Translate =>
            _Translate ?? (
            _Translate = Global.Translation);
        private ITranslation _Translate;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            if (e.Property == KeyProperty) {
                Content = Translate.Value($"Entry_Header_{e.NewValue}");
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

        public EntryGridViewColumnHeader() {
            InitializeComponent();
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
