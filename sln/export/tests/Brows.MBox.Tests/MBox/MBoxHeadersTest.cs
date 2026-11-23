using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Brows.MBox.Format;

namespace Brows.MBox;

/// <summary>
/// Tests the reading and the presentation of the header fields of a message.
/// </summary>
[TestFixture]
internal sealed class MBoxHeadersTest {
    [Test]
    public void Headers_PreserveTheOrderAndTheNamesOfTheFields() {
        var headers = new[] {
            "X-GM-THRID: 1512584914615853097",
            "Subject: A subject",
            "From: Someone <someone@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers.Names, Is.EqualTo(new[] { "X-GM-THRID", "Subject", "From" }));
    }

    [Test]
    public void Headers_KeepDuplicateFields() {
        var headers = new[] {
            "Received: from a",
            "Received: from b",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers.GetAll("Received"), Has.Count.EqualTo(2));
        Assert.That(message.Headers.GetAll("Received")[0].RawValue, Is.EqualTo("from a"));
        Assert.That(message.Headers.GetAll("Received")[1].RawValue, Is.EqualTo("from b"));
    }

    [Test]
    public void Headers_GetTheFirstFieldWithTheSpecifiedName() {
        var headers = new[] {
            "Received: from a",
            "Received: from b",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Received"].RawValue, Is.EqualTo("from a"));
    }

    [Test]
    public void Headers_MatchNamesWithoutCase() {
        var headers = new[] {
            "Subject: A subject",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["subject"], Is.Not.Null);
        Assert.That(message.Headers.Contains("SUBJECT"), Is.True);
    }

    [Test]
    public void Headers_ReturnNullForAMissingField() {
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n"));
        Assert.That(message.Headers["Subject"], Is.Null);
        Assert.That(message.Headers.Get("Subject"), Is.Null);
    }

    [Test]
    public void Headers_ReturnAnEmptyCollectionForAMissingField() {
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n"));
        Assert.That(message.Headers.GetAll("Subject"), Is.Empty);
    }

    [Test]
    public void Headers_UnfoldContinuationLinesToOneSpace() {
        var headers = new[] {
            "Subject: A subject that is",
            "\tfolded over two lines",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].RawValue, Is.EqualTo("A subject that is folded over two lines"));
    }

    [Test]
    public void Headers_TrimTheOuterWhitespaceOfAValue() {
        var headers = new[] {
            "Subject:    A subject   ",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].RawValue, Is.EqualTo("A subject"));
    }

    [Test]
    public void Headers_SeparateTheNameAtTheFirstColon() {
        var headers = new[] {
            "Subject: A subject: with a colon",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].Name, Is.EqualTo("Subject"));
        Assert.That(message.Headers["Subject"].RawValue, Is.EqualTo("A subject: with a colon"));
    }

    [Test]
    public void Headers_KeepAFieldWithoutAColonAndReportIt() {
        var headers = new[] {
            "A field without a colon",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["A field without a colon"].RawValue, Is.Empty);
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedHeader), Is.True);
    }

    [Test]
    public void Headers_IndexTheFieldsInTheOrderTheyWereStored() {
        var headers = new[] {
            "Subject: A subject",
            "From: Someone",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers.Items[0].Name, Is.EqualTo("Subject"));
        Assert.That(message.Headers.Items[0].RawIndex, Is.EqualTo(0));
        Assert.That(message.Headers.Items[1].Name, Is.EqualTo("From"));
        Assert.That(message.Headers.Items[1].RawIndex, Is.EqualTo(1));
    }

    [Test]
    public void Headers_DoNotExposeTheCollectionTheyWereBuiltFrom() {
        var fields = new List<MBoxHeader> {
            new("Subject", "A subject", "A subject", 0),
        };
        var headers = new MBoxHeaders(fields);
        fields.Add(new("From", "Someone", "Someone", 1));
        Assert.That(headers.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void Headers_DecodeAnEncodedWordSubject() {
        var headers = new[] {
            "Subject: =?utf-8?Q?A_subject?=",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].Value, Is.EqualTo("A subject"));
    }

    [Test]
    public void Headers_KeepTheRawValueOfADecodedField() {
        var headers = new[] {
            "Subject: =?utf-8?Q?A_subject?=",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].RawValue, Is.EqualTo("=?utf-8?Q?A_subject?="));
    }

    [Test]
    public void Headers_DecodeAnEncodedWordAddress() {
        var headers = new[] {
            "From: =?utf-8?Q?Some=20one?= <someone@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["From"].Value, Is.EqualTo("Some one <someone@example.com>"));
    }

    [Test]
    public void Headers_DoNotDecodeTheAddressSpecification() {
        var headers = new[] {
            "From: Someone <someone@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["From"].Value, Is.EqualTo("Someone <someone@example.com>"));
    }

    [Test]
    public void Headers_ElideWhitespaceBetweenAdjacentEncodedAddressWords() {
        var headers = new[] {
            "From: =?UTF-8?Q?John?= =?UTF-8?B?U21pdGg=?= <john@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["From"].Value, Is.EqualTo("JohnSmith <john@example.com>"));
    }

    [Test]
    public void Headers_ElideWhitespaceBetweenAdjacentEncodedAddressWordsAfterFolding() {
        var headers = new[] {
            "From: =?UTF-8?Q?John?=",
            " =?UTF-8?B?U21pdGg=?= <john@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["From"].Value, Is.EqualTo("JohnSmith <john@example.com>"));
    }

    [Test]
    public void Headers_PreserveWhitespaceEncodedInsideAnAddressWord() {
        var headers = new[] {
            "From: =?UTF-8?Q?John_?= =?UTF-8?Q?Smith?= <john@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["From"].Value, Is.EqualTo("John Smith <john@example.com>"));
    }

    [Test]
    public void Headers_ElideWhitespaceBetweenAdjacentNonLatinAddressWords() {
        var headers = new[] {
            "From: =?UTF-8?B?5pel?= =?UTF-8?B?5pys?= <jp@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["From"].Value, Is.EqualTo("日本 <jp@example.com>"));
    }

    [Test]
    public void Headers_ElideWhitespaceBetweenAdjacentEncodedWordsInAComment() {
        var headers = new[] {
            "From: Someone (=?UTF-8?Q?John?= =?UTF-8?Q?Smith?=) <john@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["From"].Value, Is.EqualTo("Someone (JohnSmith) <john@example.com>"));
    }

    [Test]
    public void Headers_KeepEncodedLookingTextInQuotedAddressPhrases() {
        var value = "\"=?UTF-8?Q?John?= =?UTF-8?Q?Smith?=\" <john@example.com>";
        var headers = new[] {
            "From: " + value,
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["From"].Value, Is.EqualTo(value));
    }

    [Test]
    public void Headers_KeepEncodedLookingTextInTheAddressSpecification() {
        var value = "Name <john=?UTF-8?Q?smith?=@example.com>";
        var headers = new[] {
            "From: " + value,
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["From"].Value, Is.EqualTo(value));
    }

    [Test]
    public void Headers_KeepAnEncodedWordWithInvalidAsciiBytesAndReportIt() {
        var headers = new[] {
            "Subject: =?US-ASCII?Q?=E9?=",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));

        Assert.That(message.Headers["Subject"].Value, Is.EqualTo("=?US-ASCII?Q?=E9?="));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Headers_DoNotDecodeAMessageId() {
        var headers = new[] {
            "Message-ID: <abc=??=def@example.com>",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Message-ID"].Value, Is.EqualTo("<abc=??=def@example.com>"));
    }

    [Test]
    public void Headers_DoNotDecodeAContentTypeParameter() {
        var headers = new[] {
            "Content-Type: text/plain; name=\"=?utf-8?Q?A_name?=\"",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Content-Type"].Value, Is.EqualTo("text/plain; name=\"=?utf-8?Q?A_name?=\""));
    }

    [Test]
    public void Headers_ElideTheWhitespaceBetweenAdjacentEncodedWords() {
        var headers = new[] {
            "Subject: =?utf-8?Q?A_?= =?utf-8?Q?subject?=",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].Value, Is.EqualTo("A subject"));
    }

    [Test]
    public void Headers_KeepAMalformedEncodedWordAsItIs() {
        var headers = new[] {
            "Subject: =?utf-8?Q?An_unterminated_word",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].Value, Is.EqualTo("=?utf-8?Q?An_unterminated_word"));
    }

    [Test]
    public void Headers_KeepAnOverlongEncodedWordAsItIs() {
        var overlong = "=?utf-8?Q?" + new string('A', 80) + "?=";
        var headers = new[] {
            $"Subject: {overlong}",
        };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers));
        Assert.That(message.Headers["Subject"].Value, Is.EqualTo(overlong));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.MalformedEncoding), Is.True);
    }

    [Test]
    public void Headers_ReportAHeaderCountThatIsOverTheLimit() {
        var headers = Enumerable
            .Range(0, 10)
            .Select(index => $"X-Field-{index}: value")
            .ToArray();
        var options = MBoxParserOptions.Default with { MaxHeaderCount = 3 };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers), 0, options);
        Assert.That(message.Headers.Items, Has.Count.EqualTo(3));
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.HeaderLimitReached), Is.True);
    }

    [Test]
    public void Headers_ReportAHeaderBlockThatIsOverTheLimit() {
        var headers = Enumerable
            .Range(0, 10)
            .Select(index => $"X-Field-{index}: value")
            .ToArray();
        var options = MBoxParserOptions.Default with { MaxHeaderBytes = 32 };
        var message = MBoxFixture.Headers(MBoxFixture.Mailbox("Body.\n", headers), 0, options);
        Assert.That(message.Diagnostics.Any(item => item.Code == MBoxDiagnosticCode.HeaderLimitReached), Is.True);
    }
}
