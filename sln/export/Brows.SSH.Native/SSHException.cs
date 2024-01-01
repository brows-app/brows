using Brows.SSH.Exceptions;
using System;

namespace Brows {
    public class SSHException : Exception {
        protected SSHException() {
        }

        public string Name { get; private set; }

        public static Exception Factory(int error) {
            SSHException instance(string name) {
                return
                    name == null ? null :
                    name == "AUTHENTICATION_FAILED" ? new AuthenticationFailedException() :
                    name == "PASSWORD_EXPIRED" ? new PasswordExpiredException() :
                    name == "PUBLIC_KEY_UNVERIFIED" ? new PublicKeyUnverifiedException() :
                    new SSHException();
            }
            var name = SSHNative.Name(error);
            var exception = instance(name);
            if (exception != null) {
                exception.Name = name;
            }
            return exception;
        }
    }
}
