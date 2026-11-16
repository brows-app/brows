using Brows.Requests;
using Brows.Security;
using System.Security;

namespace Brows.Panels.PanelSecrets;

internal sealed class SecretUsernameAndPassword : PanelSecret<ISecretUsernameAndPasswordController>, ISecretUsernameAndPassword {
    internal sealed override byte[] Encode() {
        return SecurityData.EncodeSecret(username: Username, password: Password);
    }

    internal sealed override void Decode(byte[] data) {
        var decoded = SecurityData.DecodeSecret(data);
        Username = decoded.Username;
        Password = decoded.Password;
    }

    protected sealed override void Submit() {
        Username = Controller?.SecretUsername;
        Password = Controller?.SecretPassword?.Copy();
        Password?.MakeReadOnly();
        Controller?.Clear();
    }

    protected sealed override void Cancel() {
        Username = null;
        Password = null;
        Controller?.Clear();
    }

    public string Username { get; private set; }
    public SecureString Password { get; private set; }

    public SecretUsernameAndPassword(IRequestFactory request) : base(request) {
    }
}
