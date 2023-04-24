using System;

namespace Domore.Runtime.Win32 {
    public struct LOGFONTW {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;

        char lfFaceName0;
        char lfFaceName1;
        char lfFaceName2;
        char lfFaceName3;
        char lfFaceName4;
        char lfFaceName5;
        char lfFaceName6;
        char lfFaceName7;
        char lfFaceName8;
        char lfFaceName9;
        char lfFaceName10;
        char lfFaceName11;
        char lfFaceName12;
        char lfFaceName13;
        char lfFaceName14;
        char lfFaceName15;
        char lfFaceName16;
        char lfFaceName17;
        char lfFaceName18;
        char lfFaceName19;
        char lfFaceName20;
        char lfFaceName21;
        char lfFaceName22;
        char lfFaceName23;
        char lfFaceName24;
        char lfFaceName25;
        char lfFaceName26;
        char lfFaceName27;
        char lfFaceName28;
        char lfFaceName29;
        char lfFaceName30;
        char lfFaceName31;

        public string lfFaceName {
            get {
                char[] charArray = new char[] { lfFaceName0, lfFaceName1, lfFaceName2, lfFaceName3, lfFaceName4, lfFaceName5, lfFaceName6, lfFaceName7, lfFaceName8, lfFaceName9, lfFaceName10, lfFaceName11, lfFaceName12, lfFaceName13, lfFaceName14, lfFaceName15, lfFaceName16, lfFaceName17, lfFaceName18, lfFaceName19, lfFaceName20, lfFaceName21, lfFaceName22, lfFaceName23, lfFaceName24, lfFaceName25, lfFaceName26, lfFaceName27, lfFaceName28, lfFaceName29, lfFaceName30, lfFaceName31 };
                int length;
                for (length = 0; length < charArray.Length; ++length)
                    if (charArray[length] == '\0')
                        break;
                if (length == charArray.Length)
                    return null;
                return new string(charArray, 0, length);
            }
            set {
                int length = value.Length;
                if (length > 31)
                    throw new ArgumentOutOfRangeException("value", "The string is too long.");
                lfFaceName0 = length >= 0 ? value[0] : '\0';
                lfFaceName1 = length >= 1 ? value[1] : '\0';
                lfFaceName2 = length >= 2 ? value[2] : '\0';
                lfFaceName3 = length >= 3 ? value[3] : '\0';
                lfFaceName4 = length >= 4 ? value[4] : '\0';
                lfFaceName5 = length >= 5 ? value[5] : '\0';
                lfFaceName6 = length >= 6 ? value[6] : '\0';
                lfFaceName7 = length >= 7 ? value[7] : '\0';
                lfFaceName8 = length >= 8 ? value[8] : '\0';
                lfFaceName9 = length >= 9 ? value[9] : '\0';
                lfFaceName10 = length >= 10 ? value[10] : '\0';
                lfFaceName11 = length >= 11 ? value[11] : '\0';
                lfFaceName12 = length >= 12 ? value[12] : '\0';
                lfFaceName13 = length >= 13 ? value[13] : '\0';
                lfFaceName14 = length >= 14 ? value[14] : '\0';
                lfFaceName15 = length >= 15 ? value[15] : '\0';
                lfFaceName16 = length >= 16 ? value[16] : '\0';
                lfFaceName17 = length >= 17 ? value[17] : '\0';
                lfFaceName18 = length >= 18 ? value[18] : '\0';
                lfFaceName19 = length >= 19 ? value[19] : '\0';
                lfFaceName20 = length >= 20 ? value[20] : '\0';
                lfFaceName21 = length >= 21 ? value[21] : '\0';
                lfFaceName22 = length >= 22 ? value[22] : '\0';
                lfFaceName23 = length >= 23 ? value[23] : '\0';
                lfFaceName24 = length >= 24 ? value[24] : '\0';
                lfFaceName25 = length >= 25 ? value[25] : '\0';
                lfFaceName26 = length >= 26 ? value[26] : '\0';
                lfFaceName27 = length >= 27 ? value[27] : '\0';
                lfFaceName28 = length >= 28 ? value[28] : '\0';
                lfFaceName29 = length >= 29 ? value[29] : '\0';
                lfFaceName30 = length >= 30 ? value[30] : '\0';
                lfFaceName31 = '\0';
            }
        }
    }
}
