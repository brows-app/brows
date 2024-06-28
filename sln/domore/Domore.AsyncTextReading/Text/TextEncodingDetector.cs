using System;
using System.Buffers;
using System.Text;

namespace Domore.Text {
    internal sealed class TextEncodingDetector {
        private const bool ThrowOnInvalid = true;

        private byte[] Slice(ReadOnlySequence<byte> sequence) {
            switch (sequence.Length) {
                case 2:
                    return sequence.Slice(0, 2).ToArray();
                case 3:
                    return sequence.Slice(0, 3).ToArray();
                case >= 4:
                    return sequence.Slice(0, 4).ToArray();
                default:
                    return Array.Empty<byte>();
            }
        }

        public bool TryDetect(ReadOnlySequence<byte> sequence, out Encoding encoding, out int preambleLength) {
            var slice = Slice(sequence);
            var length = slice.Length;
            if (length >= 2) {
                if (slice[0] == 254 && slice[1] == 255) {
                    encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: ThrowOnInvalid);
                    preambleLength = 2;
                    return true;
                }
                if (slice[0] == 255 && slice[1] == 254) {
                    if (length >= 4 && slice[2] == 0 && slice[3] == 0) {
                        encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: ThrowOnInvalid);
                        preambleLength = 4;
                        return true;
                    }
                    else {
                        encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: ThrowOnInvalid);
                        preambleLength = 2;
                        return true;
                    }
                }
                if (length >= 3 && slice[0] == 239 && slice[1] == 187 && slice[2] == 191) {
                    encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: ThrowOnInvalid);
                    preambleLength = 3;
                    return true;
                }
                if (length >= 4) {
                    if (slice[0] == 0 && slice[1] == 0 && slice[2] == 254 && slice[3] == 255) {
                        encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: ThrowOnInvalid);
                        preambleLength = 4;
                        return true;
                    }
                    encoding = null;
                    preambleLength = 0;
                    return true;
                }
            }
            encoding = null;
            preambleLength = 0;
            return false;
        }
    }
}
