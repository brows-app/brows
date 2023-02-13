using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO.Extensions {
    using Resources;
    using Text;

    [TestFixture]
    public class DecodeTextExtensionTest {
        private class Builder : DecodedTextBuilder {
            private readonly StringBuilder StringBuilder = new();

            protected override Task Add(ReadOnlyMemory<char> memory, CancellationToken cancellationToken) {
                StringBuilder.Append(new string(memory.Span));
                return Task.CompletedTask;
            }

            protected override Task Clear(CancellationToken cancellationToken) {
                StringBuilder.Clear();
                return Task.CompletedTask;
            }

            public string Text =>
                StringBuilder.ToString();
        }

        private static object[] ResourceNames(string kind, object[] p) {
            return Resource.Names()
                .Where(name => name.StartsWith(kind))
                .Select(name => new[] { name }.Concat(p).ToArray())
                .ToArray();
        }

        private async Task DecodeText_Success(string resource, Encoding encoding, DecodedTextOptions options) {
            using (var r = new Resource(resource)) {
                await r.Save(encoding);
                var file = r.File();
                var builder = new Builder();
                var decoded = await file.DecodeText(builder, options, CancellationToken.None);
                Assert.That(decoded.Success, Is.True);
                Assert.That(decoded.Encoding, Is.EqualTo(encoding.EncodingName));

                var resourceText = await r.Text();
                var resourceTextLength = resourceText.Length;
                Assert.That(decoded.TextLength, Is.EqualTo(resourceTextLength));

                var decodedText = decoded.Text();
                Assert.That(decodedText, Is.EqualTo(resourceText));
            }
        }

        private async Task DecodeText_Success_UTF8(string resource, bool bom, DecodedTextOptions options) {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom, throwOnInvalidBytes: true);
            await DecodeText_Success(resource, encoding, options);
        }

        private async Task DecodeText_Success_UTF16(string resource, bool bigEndian, bool bom, DecodedTextOptions options) {
            var encoding = new UnicodeEncoding(bigEndian, bom);
            await DecodeText_Success(resource, encoding, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF8_WithDefaultOptions(string resource, bool bom) {
            var options = default(DecodedTextOptions);
            await DecodeText_Success_UTF8(resource, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true, true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false, true } })]
        public async Task DecodeText_Success_UTF16_WithDefaultOptions(string resource, bool bigEndian, bool bom) {
            var options = default(DecodedTextOptions);
            await DecodeText_Success_UTF16(resource, bigEndian, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF16_WithBigEndianness(string resource, bool bom) {
            var options = new DecodedTextOptions();
            options.Encoding = new List<string> { "utf-16BE" };
            await DecodeText_Success_UTF16(resource, bigEndian: true, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF8_WithSmallStreamBuffer(string resource, bool bom) {
            var options = new DecodedTextOptions();
            options.StreamBuffer.Size = 1;
            await DecodeText_Success_UTF8(resource, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF8_WithBigStreamBuffer(string resource, bool bom) {
            var options = new DecodedTextOptions();
            options.StreamBuffer.Size = 50000;
            await DecodeText_Success_UTF8(resource, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF8_WithSmallTextBuffer(string resource, bool bom) {
            var options = new DecodedTextOptions();
            options.TextBuffer.Size = 1;
            await DecodeText_Success_UTF8(resource, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF8_WithBigTextBuffer(string resource, bool bom) {
            var options = new DecodedTextOptions();
            options.TextBuffer.Size = 50000;
            await DecodeText_Success_UTF8(resource, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF8_WithUnsharedStreamBuffer(string resource, bool bom) {
            var options = new DecodedTextOptions();
            options.StreamBuffer.Shared = false;
            await DecodeText_Success_UTF8(resource, bom, options);
        }

        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { true } })]
        [TestCaseSource(nameof(ResourceNames), new object[] { "success_unicode", new object[] { false } })]
        public async Task DecodeText_Success_UTF8_WithUnsharedTextBuffer(string resource, bool bom) {
            var options = new DecodedTextOptions();
            options.TextBuffer.Shared = false;
            await DecodeText_Success_UTF8(resource, bom, options);
        }
    }
}
