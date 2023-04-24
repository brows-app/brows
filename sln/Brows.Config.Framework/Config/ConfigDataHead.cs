using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Brows.Config {
    internal static class ConfigDataHead {
        public static string For(string type, string id) {
            var enc = Encoding.UTF8;
            var source = enc.GetBytes(type).Append((byte)0).Concat(enc.GetBytes(id)).ToArray();
            var hash = SHA256.HashData(source);
            var s = Base32HexString.From(hash);
            return $".02@{s}";
        }

        private static class Base32HexString {
            private static char Char(byte value) {
                return value switch {
                    00 => '0',
                    01 => '1',
                    02 => '2',
                    03 => '3',
                    04 => '4',
                    05 => '5',
                    06 => '6',
                    07 => '7',
                    08 => '8',
                    09 => '9',
                    10 => 'A',
                    11 => 'B',
                    12 => 'C',
                    13 => 'D',
                    14 => 'E',
                    15 => 'F',
                    16 => 'G',
                    17 => 'H',
                    18 => 'I',
                    19 => 'J',
                    20 => 'K',
                    21 => 'L',
                    22 => 'M',
                    23 => 'N',
                    24 => 'O',
                    25 => 'P',
                    26 => 'Q',
                    27 => 'R',
                    28 => 'S',
                    29 => 'T',
                    30 => 'U',
                    31 => 'V',
                    _ => throw new ArgumentException(paramName: nameof(value), message: $"{nameof(value)} [{value}]")
                };
            }

            public static string From(byte[] inArray) {
                if (inArray is null) throw new ArgumentNullException(nameof(inArray));
                if (inArray.Length == 0) return "";
                var chars = (int)Math.Ceiling(inArray.Length / 5.0) * 8;
                var bits = (byte)5;
                var s = new char[chars];
                var c = default(byte);
                var i = 0;
                foreach (var b in inArray) {
                    c = (byte)(c | (b >> (8 - bits)));
                    s[i++] = Char(c);
                    if (bits < 4) {
                        c = (byte)((b >> (3 - bits)) & 31);
                        s[i++] = Char(c);
                        bits += 5;
                    }
                    bits -= 3;
                    c = (byte)((b << bits) & 31);
                }
                if (i != chars) {
                    s[i++] = Char(c);
                    while (i != chars) {
                        s[i++] = '=';
                    }
                }
                return new string(s);
            }
        }
    }
}
