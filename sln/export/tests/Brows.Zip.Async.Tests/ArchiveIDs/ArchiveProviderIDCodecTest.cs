using System.Collections.Generic;
using System.Text;

namespace Brows.Shared.Archives;

[TestFixture]
internal sealed class ArchiveProviderIDCodecTest {
    [Test]
    public void Format_EmitsCanonicalRootAndNestedIdentifiers() {
        var zipRoot = new ArchiveProviderID(
            ArchiveProviderIDFormat.Zip,
            @"C:\archives\files.zip",
            Array.Empty<string>(),
            "");
        var sevenZipNested = new ArchiveProviderID(
            ArchiveProviderIDFormat.SevenZip,
            @"C:\archives\files.7z",
            new[] { "inner.7z", "deep/another.7z" },
            "a>b");
        var tarRoot = new ArchiveProviderID(
            ArchiveProviderIDFormat.Tar,
            @"C:\archives\files.tar",
            Array.Empty<string>(),
            "");
        var parsedZipRoot = ArchiveProviderIDCodec.Parse(ArchiveProviderIDCodec.Format(zipRoot));
        var parsedSevenZipNested = ArchiveProviderIDCodec.Parse(ArchiveProviderIDCodec.Format(sevenZipNested));
        var parsedTarRoot = ArchiveProviderIDCodec.Parse(ArchiveProviderIDCodec.Format(tarRoot));

        using (Assert.EnterMultipleScope()) {
            Assert.That(ArchiveProviderIDCodec.Format(zipRoot),
                Is.EqualTo(@"zip:1|f=C:\archives\files.zip|d="));
            Assert.That(ArchiveProviderIDCodec.Format(sevenZipNested),
                Is.EqualTo(@"7z:1|f=C:\archives\files.7z|n=inner.7z|n=deep/another.7z|d=a%3Eb"));
            Assert.That(ArchiveProviderIDCodec.Format(tarRoot),
                Is.EqualTo(@"tar:1|f=C:\archives\files.tar|d="));
            Assert.That(parsedZipRoot.NestedMembers, Is.Empty);
            Assert.That(parsedZipRoot.Directory, Is.Empty);
            Assert.That(parsedSevenZipNested.NestedMembers,
                Is.EqualTo(new[] { "inner.7z", "deep/another.7z" }));
            Assert.That(parsedSevenZipNested.Directory, Is.EqualTo("a>b"));
            Assert.That(parsedTarRoot.FileName, Is.EqualTo(@"C:\archives\files.tar"));
        }
    }

    [Test]
    public void Format_EncodesReservedAndUnicodeCharactersAndParsesOnce() {
        var originalDirectory = "space + %3E|=\" \u03A9";
        var location = new ArchiveProviderID(
            ArchiveProviderIDFormat.Zip,
            @"C:\archives\files.zip",
            new[] { "folder/a>b.zip" },
            originalDirectory);

        var formatted = ArchiveProviderIDCodec.Format(location);
        var parsed = ArchiveProviderIDCodec.Parse(formatted);

        using (Assert.EnterMultipleScope()) {
            Assert.That(formatted,
                Is.EqualTo(
                    @"zip:1|f=C:\archives\files.zip|n=folder/a%3Eb.zip|d=space%20%2B%20%253E%7C%3D%22%20%CE%A9"));
            Assert.That(parsed.Directory, Is.EqualTo(originalDirectory));
            Assert.That(parsed.NestedMembers, Is.EqualTo(new[] { "folder/a>b.zip" }));
        }
    }

    [Test]
    public void Parse_AcceptsEitherEscapeCaseAndPreservesLiteralPercentSequences() {
        const string encoded = @"zip:1|f=C:\archives\files%2ezip|d=literal%253E";

        var parsed = ArchiveProviderIDCodec.Parse(encoded);

        using (Assert.EnterMultipleScope()) {
            Assert.That(parsed.FileName, Is.EqualTo(@"C:\archives\files.zip"));
            Assert.That(parsed.Directory, Is.EqualTo("literal%3E"));
            Assert.That(ArchiveProviderIDCodec.Format(parsed),
                Is.EqualTo(@"zip:1|f=C:\archives\files.zip|d=literal%253E"));
        }
    }

    [TestCase("zip:2|f=C:\\archives\\files.zip|d=")]
    [TestCase("tar:2|f=C:\\archives\\files.tar|d=")]
    [TestCase("rar:1|f=C:\\archives\\files.zip|d=")]
    [TestCase(@"C:\archives\files.zip>folder")]
    [TestCase("zip:1|f=C:\\archives\\files.zip")]
    [TestCase("zip:1|n=inner.zip|d=")]
    [TestCase("zip:1|f=|d=")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|f=other.zip|d=")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=|d=")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=folder|n=inner.zip")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|x=value|d=")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|n=|d=")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|n=inner.zip|d=|")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=bad%")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=bad%G0")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=bad%C3%28")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=bad%00")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=raw+plus")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=raw>delimiter")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=raw space")]
    [TestCase("zip:1|f=C:\\archives\\files.zip|d=\u03A9")]
    public void TryParse_ReturnsFalseForMalformedOrUnsupportedIdentifiers(string value) {
        var parsed = ArchiveProviderIDCodec.TryParse(value, out var location);

        using (Assert.EnterMultipleScope()) {
            Assert.That(parsed, Is.False);
            Assert.That(location, Is.Null);
        }
    }

    [Test]
    public void HasArchiveProviderPrefix_RecognizesMalformedAndMismatchedProviderIDs() {
        using (Assert.EnterMultipleScope()) {
            Assert.That(ArchiveProviderIDCodec.HasArchiveProviderPrefix("zip:malformed"), Is.True);
            Assert.That(ArchiveProviderIDCodec.HasArchiveProviderPrefix("7z:99|f=x|d="), Is.True);
            Assert.That(ArchiveProviderIDCodec.HasArchiveProviderPrefix("tar:malformed"), Is.True);
            Assert.That(ArchiveProviderIDCodec.HasArchiveProviderPrefix(@"C:\archives\files.zip"), Is.False);
            Assert.That(ArchiveProviderIDCodec.HasArchiveProviderPrefix(@"C:\archives\files.zip>folder"), Is.False);
        }
    }

    [Test]
    public void Format_RejectsNulAndInvalidUtf16() {
        var nulLocation = new ArchiveProviderID(
            ArchiveProviderIDFormat.Zip,
            @"C:\" + "\0files.zip",
            Array.Empty<string>(),
            "");
        var invalidUtf16Location = new ArchiveProviderID(
            ArchiveProviderIDFormat.Zip,
            @"C:\" + (char)0xD800 + "files.zip",
            Array.Empty<string>(),
            "");

        using (Assert.EnterMultipleScope()) {
            Assert.That(() => ArchiveProviderIDCodec.Format(nulLocation), Throws.ArgumentException);
            Assert.That(
                () => ArchiveProviderIDCodec.Format(invalidUtf16Location),
                Throws.TypeOf<EncoderFallbackException>());
        }
    }

    [Test]
    public void ArchiveProviderID_CopiesNestedMembersAndUsesOrdinalSequenceEquality() {
        var members = new List<string> { "inner.7z", "folder/deep.7z" };
        var original = new ArchiveProviderID(ArchiveProviderIDFormat.SevenZip, @"C:\files.7z", members, "docs");
        var equivalent = new ArchiveProviderID(
            ArchiveProviderIDFormat.SevenZip,
            @"C:\files.7z",
            new[] { "inner.7z", "folder/deep.7z" },
            "docs");
        var differentOrder = new ArchiveProviderID(
            ArchiveProviderIDFormat.SevenZip,
            @"C:\files.7z",
            new[] { "folder/deep.7z", "inner.7z" },
            "docs");
        var differentCase = new ArchiveProviderID(
            ArchiveProviderIDFormat.SevenZip,
            @"C:\files.7z",
            new[] { "INNER.7z", "folder/deep.7z" },
            "docs");
        members[0] = "changed.7z";
        members.Add("another.7z");

        using (Assert.EnterMultipleScope()) {
            Assert.That(original.NestedMembers, Is.EqualTo(new[] { "inner.7z", "folder/deep.7z" }));
            Assert.That(original.Equals(equivalent), Is.True);
            Assert.That(original.GetHashCode(), Is.EqualTo(equivalent.GetHashCode()));
            Assert.That(original.Equals(differentOrder), Is.False);
            Assert.That(original.Equals(differentCase), Is.False);
            Assert.That(original.WithNestedMember("next.7z").NestedMembers,
                Is.EqualTo(new[] { "inner.7z", "folder/deep.7z", "next.7z" }));
            Assert.That(original.WithNestedMember("next.7z").Directory, Is.Empty);
            Assert.That(original.Directory, Is.EqualTo("docs"));
        }

        var readOnlyMembers = (IList<string>)original.NestedMembers;
        Assert.That(() => readOnlyMembers[0] = "mutated.7z", Throws.TypeOf<NotSupportedException>());
    }
}
