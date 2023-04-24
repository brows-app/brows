﻿using Domore.IO;
using Domore.Windows.Controls;
using System;
using System.IO;
using System.Windows;

namespace Brows {
    partial class EntryStreamTextControl {
        private void PreviewTextControl_PreviewTextLoadingChanged(object sender, RoutedEventArgs e) {
            var control = sender as PreviewTextControl;
            if (control != null) {
                EntryStreamGui?.State?.Text?.Change(loading: control.PreviewTextLoading);
            }
        }

        private void PreviewTextControl_PreviewTextSuccessChanged(object sender, RoutedEventArgs e) {
            var control = sender as PreviewTextControl;
            if (control != null) {
                EntryStreamGui?.State?.Text?.Change(success: control.PreviewTextSuccess);
            }
        }

        protected override void OnEntryStreamGuiChanged(DependencyPropertyChangedEventArgs e) {
            PreviewTextControl.PreviewTextOptions = EntryStreamGui?.Options?.DecodedTextOptions;
            PreviewTextControl.PreviewTextSourceLengthMax = EntryStreamGui?.Options?.TextSourceLengthMax;
            PreviewTextControl.PreviewTextSource = EntryStreamGui == null
                ? null :
                new StreamText(EntryStreamGui.Source);
            base.OnEntryStreamGuiChanged(e);
        }

        public EntryStreamTextControl() {
            InitializeComponent();
        }

        private class StreamText : IStreamText {
            public bool StreamValid => Source.StreamValid;
            public long StreamLength => Source.StreamLength;

            public IEntryStreamSource Source { get; }

            public StreamText(IEntryStreamSource source) {
                Source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public IDisposable StreamReady() {
                return Source.StreamReady();
            }

            Stream IStreamText.StreamText() {
                return Source.Stream();
            }
        }
    }
}