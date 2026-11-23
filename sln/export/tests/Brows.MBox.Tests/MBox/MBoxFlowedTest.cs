using System;
using System.Linq;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the decoding of text that uses the <c>format=flowed</c> parameter.
/// </summary>
/// <remarks>
/// The tests follow RFC 3676. A flowed line ends with a space, and the line break of a soft
/// line break is removed when the paragraph is joined. The trailing space is only removed when
/// <c>delsp=yes</c> is declared, so the joined lines are separated by a space only in that case.
/// </remarks>
[TestFixture]
internal sealed class MBoxFlowedTest {
    /// <summary>
    /// The header fields of a flowed message.
    /// </summary>
    private static readonly string[] FlowedHeaders = [
        "Content-Type: text/plain; charset=utf-8; format=flowed",
    ];

    /// <summary>
    /// The header fields of a flowed message that deletes the space of the soft line breaks.
    /// </summary>
    private static readonly string[] DelSpHeaders = [
        "Content-Type: text/plain; charset=utf-8; format=flowed; delsp=yes",
    ];

    [Test]
    public void Body_KeepsAFixedLineAsItIs() {
        var body = "One two\nthree four\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, FlowedHeaders));
        Assert.That(message.Body.Text, Is.EqualTo("One two\nthree four\n"));
    }

    [Test]
    public void Body_KeepsTheSpaceOfASoftLineBreakWhenDelSpIsNotDeclared() {
        var body = "One two \nthree four\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, FlowedHeaders));
        Assert.That(message.Body.Text, Is.EqualTo("One two three four\n"));
    }

    [Test]
    public void Body_DeletesTheSpaceOfASoftLineBreakWhenDelSpIsDeclared() {
        var body = "One two \nthree four\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, DelSpHeaders));
        Assert.That(message.Body.Text, Is.EqualTo("One twothree four\n"));
    }

    [Test]
    public void Body_KeepsTheOtherTrailingSpacesOfALine() {
        /*
         * Only the space that is immediately before the line break is the marker of the soft
         * line break, so the other spaces belong to the content of the line.
         */
        var body = "One two  \nthree four\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, DelSpHeaders));
        Assert.That(message.Body.Text, Is.EqualTo("One two three four\n"));
    }

    [Test]
    public void Body_RemovesTheStuffingSpaceOfALine() {
        var body = " One two\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, FlowedHeaders));
        Assert.That(message.Body.Text, Is.EqualTo("One two\n"));
    }

    [Test]
    public void Body_KeepsTheQuoteDepthOfTheParagraph() {
        var body = "> One two \n> three four\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, DelSpHeaders));
        Assert.That(message.Body.Text, Is.EqualTo(">One twothree four\n"));
    }

    [Test]
    public void Body_EndsAParagraphWhenTheQuoteDepthChanges() {
        var body = "> One two \n> three four\nPlain text\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, DelSpHeaders));
        Assert.That(message.Body.Text, Is.EqualTo(">One twothree four\nPlain text\n"));
    }

    [Test]
    public void Body_KeepsTheSignatureSeparatorOnItsOwnLine() {
        var body = "One two \nthree four\n-- \nThe signature\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, DelSpHeaders));
        Assert.That(message.Body.Text, Is.EqualTo("One twothree four\n-- \nThe signature\n"));
    }

    [Test]
    public void Body_KeepsTheBlankLinesOfTheBody() {
        var body = "One two\n\nThree four\n";
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, FlowedHeaders));
        Assert.That(message.Body.Text, Is.EqualTo("One two\n\nThree four\n"));
    }

    [Test]
    public void Body_KeepsATextThatIsNotFlowedAsItIs() {
        var body = "One two\nthree four\n";
        var headers = new[] {
            "Content-Type: text/plain; charset=utf-8",
        };
        var message = MBoxFixture.Message(MBoxFixture.Mailbox(body, headers));
        Assert.That(message.Body.Text, Is.EqualTo("One two\nthree four\n"));
    }
}
