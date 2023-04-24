using System;

namespace Domore.Runtime.InteropServices.ComTypes {
    public static class IID {
        public const string IExtractImage = "BB2E617C-0920-11d1-9A0B-00C04FC2D6C1";
        public const string IFileOperation = "947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8";
        public const string IFileOperationProgressSink = "04b0f1a7-9490-44bc-96e1-4296a31252e2";
        public const string IInitializeWithFile = "b7d14566-0509-4cce-a71f-0a554233bd9b";
        public const string IInitializeWithItem = "7f73be3f-fb79-493c-a6c7-7ee14e245841";
        public const string IInitializeWithStream = "b824b49d-22ac-4161-ac8a-9916e8fa3f7f";
        public const string IOperationsProgressDialog = "0C9FB851-E5C9-43EB-A370-F0677B13874C";
        public const string ISharedBitmap = "091162a4-bc96-411f-aae8-c5122cd03363";
        public const string IShell = "286E6F1B-7113-4355-9562-96B7E9D64C54";
        public const string IShellItem = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
        public const string IShellItemImageFactory = "bcc18b79-ba16-442f-80c4-8a59c30c463b";
        public const string IThumbnailCache = "F676C15D-596A-4ce2-8234-33996F445DB1";
        public const string IShellIconOverlayIdentifier = "0c6c4200-c589-11d0-999a-00c04fd655e1";
        public const string IPropertyDescription = "6F79D558-3E96-4549-A1D1-7D75D2288814";
        public const string IPropertyDescriptionList = "1f9fc1d0-c39b-4b26-817f-011967d3440e";
        public const string IPropertyStore = "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99";
        public const string IPropertySystem = "ca724e8a-c3e6-442b-88a4-6fb0db8035a3";
        public const string IObjectWithSite = "fc4801a3-2ba9-11cf-a229-00aa003d7352";
        public const string IQueryAssociations = "c46ca590-3c3f-11d2-bee6-0000f805ca57";
        public const string IPreviewHandler = "8895b1c6-b41f-4c1c-a562-0d564250836f";
        public const string IPreviewHandlerFrame = "fec87aaf-35f9-447a-adb7-20234491401a";
        public const string IPreviewHandlerVisuals = "196bf9a5-b346-4ef0-aa1e-5dcdb76768b1";

        public static class Managed {
            public static readonly Guid IShellItem = new Guid(IID.IShellItem);
            public static readonly Guid IPreviewHandler = new Guid(IID.IPreviewHandler);
            public static readonly Guid IQueryAssociations = new Guid(IID.IQueryAssociations);
            public static readonly Guid IPropertyDescription = new Guid(IID.IPropertyDescription);
            public static readonly Guid IPropertyDescriptionList = new Guid(IID.IPropertyDescriptionList);
            public static readonly Guid IPropertyStore = new Guid(IID.IPropertyStore);
            public static readonly Guid IPropertySystem = new Guid(IID.IPropertySystem);
        }
    }
}
