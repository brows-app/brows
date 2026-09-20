using System.Runtime.CompilerServices;

namespace Brows;

public sealed class ImportRequiredException : InvalidOperationException {
    private string MessageFactory() {
        var targetName = Target?.GetType()?.Name ?? "";
        var propertyName = PropertyName?.Trim() ?? "";
        if (propertyName == "") {
            if (targetName == "") {
                return "The import is required.";
            }
            return $"The import is required for target '{targetName}'.";
        }
        if (targetName == "") {
            return $"The import '{propertyName}' is required.";
        }
        return $"The import '{propertyName}' is required for target '{targetName}'.";
    }

    public sealed override string Message => field ??= MessageFactory();

    public object Target { get; }
    public string PropertyName { get; }

    public ImportRequiredException(object target = null, [CallerMemberName] string propertyName = null) {
        Target = target;
        PropertyName = propertyName;
    }
}
