using System;
using TYPE = System.Type;

namespace Domore.Runtime.InteropServices.ComTypes {
    public static class CLSID {
        public const string FileOperation = "3AD05575-8857-4850-9277-11B85BDB8E09";
        public const string PropertySystem = "b8967f85-58ae-4f46-9fb2-5d7904798f4b";
        public const string Shell = "13709620-C279-11CE-A49E-444553540000";

        public static class Managed {
            public static readonly Guid FileOperation = new Guid(CLSID.FileOperation);
            public static readonly Guid PropertySystem = new Guid(CLSID.PropertySystem);
            public static readonly Guid Shell = new Guid(CLSID.Shell);

            public static class Type {
                public static readonly TYPE FileOperation = TYPE.GetTypeFromCLSID(Managed.FileOperation);
                public static readonly TYPE PropertySystem = TYPE.GetTypeFromCLSID(Managed.PropertySystem);
                public static readonly TYPE Shell = TYPE.GetTypeFromCLSID(Managed.Shell);
            }
        }
    }
}
