using Brows.Windows;
using Brows.Windows.Controls;
using System.Windows;

namespace Brows.Entries;

public sealed class EntryStreamPreviewControl : EntryStreamGuiControl {
    private PreviewHandler PreviewHandlerControl;

    private void PreviewHandlerControl_Loaded(object sender, RoutedEventArgs e) {
        ChangeEntryStreamGui();
    }

    private void PreviewHandlerControl_PreviewHandlerLoadingChanged(object sender, RoutedEventArgs e) {
        if (sender is PreviewHandler control) {
            ViewModel?.State?.Preview?.Change(loading: control.PreviewHandlerLoading);
        }
    }

    private void PreviewHandlerControl_PreviewHandlerSuccessChanged(object sender, RoutedEventArgs e) {
        if (sender is PreviewHandler control) {
            ViewModel?.State?.Preview?.Change(success: control.PreviewHandlerSuccess);
        }
    }

    private void PreviewHandlerControl_PreviewHandlerCLSIDChanged(
        object sender,
        PreviewHandlerCLSIDChangedEventArgs e) {
        var clsid = ViewModel?.Options?.PreviewCLSID;
        if (clsid is null) {
            return;
        }
        if (clsid.TryGetValue(e.Extension, out var extensionOverride)) {
            e.Override = extensionOverride;
        }
        else if (clsid.TryGetValue($"{e.CLSID}", out var clsidOverride)) {
            e.Override = clsidOverride;
        }
    }

    private void ChangeEntryStreamGui() {
        if (PreviewHandlerControl is not null) {
            PreviewHandlerControl.PreviewHandlerSource = ViewModel?.Source?.SourceFile;
        }
    }

    protected sealed override string EntryStreamViewName => "Preview";

    protected sealed override void OnViewModelChanged(IEntryStreamGui oldValue, IEntryStreamGui newValue) {
        ChangeEntryStreamGui();
        base.OnViewModelChanged(oldValue, newValue);
    }
    public override void OnApplyTemplate() {
        var oldControl = PreviewHandlerControl;
        if (oldControl is not null) {
            oldControl.Loaded -= PreviewHandlerControl_Loaded;
            oldControl.PreviewHandlerCLSIDChanged -= PreviewHandlerControl_PreviewHandlerCLSIDChanged;
            oldControl.PreviewHandlerLoadingChanged -= PreviewHandlerControl_PreviewHandlerLoadingChanged;
            oldControl.PreviewHandlerSuccessChanged -= PreviewHandlerControl_PreviewHandlerSuccessChanged;
        }
        base.OnApplyTemplate();
        PreviewHandlerControl = GetTemplateChild("PART_PreviewHandler") as PreviewHandler;
        if (PreviewHandlerControl is null) {
            return;
        }
        PreviewHandlerControl.Loaded += PreviewHandlerControl_Loaded;
        PreviewHandlerControl.PreviewHandlerCLSIDChanged += PreviewHandlerControl_PreviewHandlerCLSIDChanged;
        PreviewHandlerControl.PreviewHandlerLoadingChanged += PreviewHandlerControl_PreviewHandlerLoadingChanged;
        PreviewHandlerControl.PreviewHandlerSuccessChanged += PreviewHandlerControl_PreviewHandlerSuccessChanged;
        if (PreviewHandlerControl.IsLoaded) {
            ChangeEntryStreamGui();
        }
    }

}