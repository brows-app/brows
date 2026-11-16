using Brows.Requests;
using System.Runtime.InteropServices;
using System.Security;

namespace Brows.Panels.PanelSecrets;

internal sealed class SecretConfirmPassword : PanelSecret<ISecretConfirmPasswordController> {
    private static bool Equal(SecureString first, SecureString second) {
        if (first is null || second is null) {
            return false;
        }
        if (first.Length != second.Length) {
            return false;
        }
        var firstPointer = Marshal.SecureStringToGlobalAllocUnicode(first);
        var secondPointer = Marshal.SecureStringToGlobalAllocUnicode(second);
        try {
            var bytes = first.Length * 2;
            for (var i = 0; i < bytes; i++) {
                var firstByte = Marshal.ReadByte(firstPointer, i);
                var secondByte = Marshal.ReadByte(secondPointer, i);
                if (firstByte != secondByte) {
                    return false;
                }
            }
            return true;
        }
        finally {
            Marshal.ZeroFreeGlobalAllocUnicode(firstPointer);
            Marshal.ZeroFreeGlobalAllocUnicode(secondPointer);
        }
    }

    protected sealed override void Submit() {
        var password = Controller?.SecretPassword?.Copy();
        var confirm = Controller?.SecretConfirmPassword?.Copy();
        var confirmed = Equal(password, confirm);
        if (confirmed) {
            password?.MakeReadOnly();
            Password = password;
        }
        else {
            password?.Dispose();
        }
        confirm?.Dispose();
        Controller?.Clear();
    }

    protected sealed override void Cancel() {
        Password = null;
        Controller?.Clear();
    }

    public SecureString Password { get; private set; }

    public SecretConfirmPassword(IRequestFactory request) : base(request) {
    }
}
