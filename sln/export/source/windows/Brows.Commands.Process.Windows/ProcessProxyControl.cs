using Brows.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Brows;

internal sealed class ProcessProxyControl : Control {
    private static readonly DependencyPropertyKey ProcessDateTimeFormatKey = DependencyProperty.RegisterReadOnly(
        name: nameof(ProcessDateTimeFormat),
        propertyType: typeof(string),
        ownerType: typeof(ProcessProxyControl),
        typeMetadata: new PropertyMetadata(
            defaultValue: null));

    private void OnProcessProxyChanged(IProcessProxy oldValue, IProcessProxy newValue) {
        string getDateTimeFormat() {
            var now = DateTime.UtcNow;
            var then = newValue?.StartTime?.ToUniversalTime();
            var span = now - then;
            if (span > TimeSpan.FromDays(1)) {
                return "yyyy-MM-dd HH:mm:ss";
            }
            return "HH:mm:ss";
        }
        ProcessDateTimeFormat = getDateTimeFormat();
    }

    public static readonly DependencyProperty ProcessDateTimeFormatProperty =
        ProcessDateTimeFormatKey.DependencyProperty;

    public string ProcessDateTimeFormat {
        get => GetValue(ProcessDateTimeFormatProperty) as string;
        private set => SetValue(ProcessDateTimeFormatKey, value);
    }

    public static readonly DependencyProperty ProcessProxyProperty = DependencyProperty.Register(
        name: nameof(ProcessProxy),
        propertyType: typeof(IProcessProxy),
        ownerType: typeof(ProcessProxyControl),
        typeMetadata: new PropertyMetadata(
            defaultValue: null,
            propertyChangedCallback: (s, e) => {
                if (s is ProcessProxyControl self) {
                    self.OnProcessProxyChanged(
                        oldValue: e.OldValue as IProcessProxy,
                        newValue: e.NewValue as IProcessProxy);
                }
            }));

    public IProcessProxy ProcessProxy {
        get => GetValue(ProcessProxyProperty) as IProcessProxy;
        set => SetValue(ProcessProxyProperty, value);
    }
}
