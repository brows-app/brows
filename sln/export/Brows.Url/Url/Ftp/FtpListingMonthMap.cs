using System;
using System.Collections.Generic;

namespace Brows.Url.Ftp {
    internal sealed class FtpListingMonthMap {
        private readonly Dictionary<string, int> Agent;

        public FtpListingMonthMap() {
            Agent = new(StringComparer.OrdinalIgnoreCase);

            //English month names
            Agent["jan"] = 1;
            Agent["feb"] = 2;
            Agent["mar"] = 3;
            Agent["apr"] = 4;
            Agent["may"] = 5;
            Agent["jun"] = 6;
            Agent["june"] = 6;
            Agent["jul"] = 7;
            Agent["july"] = 7;
            Agent["aug"] = 8;
            Agent["sep"] = 9;
            Agent["sept"] = 9;
            Agent["oct"] = 10;
            Agent["nov"] = 11;
            Agent["dec"] = 12;

            //Numerical values for the month
            Agent["1"] = 1;
            Agent["01"] = 1;
            Agent["2"] = 2;
            Agent["02"] = 2;
            Agent["3"] = 3;
            Agent["03"] = 3;
            Agent["4"] = 4;
            Agent["04"] = 4;
            Agent["5"] = 5;
            Agent["05"] = 5;
            Agent["6"] = 6;
            Agent["06"] = 6;
            Agent["7"] = 7;
            Agent["07"] = 7;
            Agent["8"] = 8;
            Agent["08"] = 8;
            Agent["9"] = 9;
            Agent["09"] = 9;
            Agent["10"] = 10;
            Agent["11"] = 11;
            Agent["12"] = 12;

            //German month names
            Agent["mrz"] = 3;
            Agent["m\xe4r"] = 3;
            Agent["m\xe4rz"] = 3;
            Agent["mai"] = 5;
            Agent["juni"] = 6;
            Agent["juli"] = 7;
            Agent["okt"] = 10;
            Agent["dez"] = 12;

            //Austrian month names
            Agent["j\xe4n"] = 1;

            //French month names
            Agent["janv"] = 1;
            Agent["f\xe9" + "b"] = 1;
            Agent["f\xe9v"] = 2;
            Agent["fev"] = 2;
            Agent["f\xe9vr"] = 2;
            Agent["fevr"] = 2;
            Agent["mars"] = 3;
            Agent["mrs"] = 3;
            Agent["avr"] = 4;
            Agent["avril"] = 4;
            Agent["juin"] = 6;
            Agent["juil"] = 7;
            Agent["jui"] = 7;
            Agent["ao\xfb"] = 8;
            Agent["ao\xfbt"] = 8;
            Agent["aout"] = 8;
            Agent["d\xe9" + "c"] = 12;
            Agent["dec"] = 12;

            //Italian month names
            Agent["gen"] = 1;
            Agent["mag"] = 5;
            Agent["giu"] = 6;
            Agent["lug"] = 7;
            Agent["ago"] = 8;
            Agent["set"] = 9;
            Agent["ott"] = 10;
            Agent["dic"] = 12;

            //Spanish month names
            Agent["ene"] = 1;
            Agent["fbro"] = 2;
            Agent["mzo"] = 3;
            Agent["ab"] = 4;
            Agent["abr"] = 4;
            Agent["agto"] = 8;
            Agent["sbre"] = 9;
            Agent["obre"] = 9;
            Agent["nbre"] = 9;
            Agent["dbre"] = 9;

            //Polish month names
            Agent["sty"] = 1;
            Agent["lut"] = 2;
            Agent["kwi"] = 4;
            Agent["maj"] = 5;
            Agent["cze"] = 6;
            Agent["lip"] = 7;
            Agent["sie"] = 8;
            Agent["wrz"] = 9;
            Agent["pa\x9f"] = 10;
            Agent["pa\xbc"] = 10; // ISO-8859-2
            Agent["paz"] = 10; // ASCII
            Agent["pa\xc5\xba"] = 10; // UTF-8
            Agent["pa\x017a"] = 10; // some servers send this
            Agent["lis"] = 11;
            Agent["gru"] = 12;

            //Russian month names
            Agent["\xff\xed\xe2"] = 1;
            Agent["\xf4\xe5\xe2"] = 2;
            Agent["\xec\xe0\xf0"] = 3;
            Agent["\xe0\xef\xf0"] = 4;
            Agent["\xec\xe0\xe9"] = 5;
            Agent["\xe8\xfe\xed"] = 6;
            Agent["\xe8\xfe\xeb"] = 7;
            Agent["\xe0\xe2\xe3"] = 8;
            Agent["\xf1\xe5\xed"] = 9;
            Agent["\xee\xea\xf2"] = 10;
            Agent["\xed\xee\xff"] = 11;
            Agent["\xe4\xe5\xea"] = 12;

            //Dutch month names
            Agent["mrt"] = 3;
            Agent["mei"] = 5;

            //Portuguese month names
            Agent["out"] = 10;

            //Finnish month names
            Agent["tammi"] = 1;
            Agent["helmi"] = 2;
            Agent["maalis"] = 3;
            Agent["huhti"] = 4;
            Agent["touko"] = 5;
            Agent["kes\xe4"] = 6;
            Agent["hein\xe4"] = 7;
            Agent["elo"] = 8;
            Agent["syys"] = 9;
            Agent["loka"] = 10;
            Agent["marras"] = 11;
            Agent["joulu"] = 12;

            //Slovenian month names
            Agent["avg"] = 8;

            //Icelandic
            Agent["ma\x00ed"] = 5;
            Agent["j\x00fan"] = 6;
            Agent["j\x00fal"] = 7;
            Agent["\x00e1g"] = 8;
            Agent["n\x00f3v"] = 11;
            Agent["des"] = 12;

            //Lithuanian
            Agent["sau"] = 1;
            Agent["vas"] = 2;
            Agent["kov"] = 3;
            Agent["bal"] = 4;
            Agent["geg"] = 5;
            Agent["bir"] = 6;
            Agent["lie"] = 7;
            Agent["rgp"] = 8;
            Agent["rgs"] = 9;
            Agent["spa"] = 10;
            Agent["lap"] = 11;
            Agent["grd"] = 12;

            // Hungarian
            Agent["szept"] = 9;

            //There are more languages and thus month
            //names, but as long as nobody reports a
            //problem, I won't add them, there are way
            //too many languages

            // Some servers send a combination of month name and number,
            // Add corresponding numbers to the month names.
            Dictionary<string, int> combo = new();
            foreach (var iter in Agent) {
                // January could be 1 or 0, depends how the server counts
                combo[string.Format("{0}{1:00}", iter.Key, iter.Value)] = iter.Value;
                combo[string.Format("{0}{1:00}", iter.Key, iter.Value - 1)] = iter.Value;
                if (iter.Value < 10) {
                    combo[string.Format("{0}{1}", iter.Key, iter.Value)] = iter.Value;
                }
                else {
                    combo[string.Format("{0}{1}", iter.Key, iter.Value % 10)] = iter.Value;
                }
                if (iter.Value <= 10) {
                    combo[string.Format("{0}{1}", iter.Key, iter.Value - 1)] = iter.Value;
                }
                else {
                    combo[string.Format("{0}{1}", iter.Key, (iter.Value - 1) % 10)] = iter.Value;
                }
            }
            foreach (var pair in combo) {
                if (Agent.ContainsKey(pair.Key)) {
                }
                else {
                    Agent[pair.Key] = pair.Value;
                }
            }

            Agent["1"] = 1;
            Agent["2"] = 2;
            Agent["3"] = 3;
            Agent["4"] = 4;
            Agent["5"] = 5;
            Agent["6"] = 6;
            Agent["7"] = 7;
            Agent["8"] = 8;
            Agent["9"] = 9;
            Agent["10"] = 10;
            Agent["11"] = 11;
            Agent["12"] = 12;
        }

        public int? Find(string month) {
            return Agent.TryGetValue(month, out var value)
                ? value
                : null;
        }
    }
}
