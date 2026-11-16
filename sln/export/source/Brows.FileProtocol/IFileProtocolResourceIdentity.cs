using System;

namespace Brows;

internal interface IFileProtocolResourceIdentity {
    Uri GetResourceIdentity(Uri uri);
}
