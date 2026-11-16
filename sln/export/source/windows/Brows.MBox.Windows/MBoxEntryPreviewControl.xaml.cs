using System.Windows.Controls;

namespace Brows;

partial class MBoxEntryPreviewControl {
    /// <summary>
    /// Gets the text box that shows the body of the message.
    /// </summary>
    internal TextBox TextBox {
        get;
    }

    /// <summary>
    /// Gets the text block that shows the status of the preview.
    /// </summary>
    internal TextBlock StatusText {
        get;
    }

    /// <summary>
    /// Gets the controller that keeps the preview in sync with the observation of the provider.
    /// </summary>
    internal MBoxEntryPreviewController Controller {
        get;
    }

    public MBoxEntryPreviewControl() {
        InitializeComponent();
        TextBox = BodyTextBox;
        StatusText = StatusTextBlock;
        Controller = new MBoxEntryPreviewController(this);
    }
}
