using Brows.Requests;
using Brows.Security;
using System.Security;

namespace Brows.Panels.PanelSecrets;

internal sealed class SecretPassword : PanelSecret<ISecretPasswordController>, ISecretPassword {
    internal sealed override byte[] Encode() {
        return SecurityData.EncodeSecret(username: null, password: Password);
    }

    internal sealed override void Decode(byte[] data) {
        var decoded = SecurityData.DecodeSecret(data);
        Password = decoded.Password;
    }

    protected sealed override void Submit() {
        Password = Controller?.SecretPassword?.Copy();
        Password?.MakeReadOnly();
        Controller?.Clear();
    }

    protected sealed override void Cancel() {
        Password = null;
        Controller?.Clear();
    }

    public SecureString Password { get; private set; }

    public SecretPassword(IRequestFactory request) : base(request) {
    }
}
