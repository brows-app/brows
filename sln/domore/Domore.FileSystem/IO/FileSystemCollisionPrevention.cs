using System.Collections.Generic;
using System.IO;

namespace Domore.IO {
    public abstract class FileSystemCollisionPrevention {
        public static FileSystemCollisionPrevention Default { get; } = new DefaultImplementation();

        public abstract string Rename(string path);

        private sealed class DefaultImplementation : FileSystemCollisionPrevention {
            public override string Rename(string path) {
                var fil = Path.GetFileName(path);
                var dir = path.Substring(0, path.Length - fil.Length);
                var ext = Path.GetExtension(fil);
                var name = Path.GetFileNameWithoutExtension(fil);
                if (name.EndsWith(')')) {
                    var d = new List<char>();
                    var parse = false;
                    for (var i = name.Length - 2; i > 0; i--) {
                        var c = name[i];
                        if (char.IsDigit(c)) {
                            d.Add(c);
                        }
                        else {
                            if (c == '(') {
                                var j = i - 1;
                                if (j > 0) {
                                    if (name[j] == ' ') {
                                        if (d.Count > 0) {
                                            parse = true;
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }
                    if (parse) {
                        d.Reverse();
                        var s = new string(d.ToArray());
                        var n = ulong.Parse(s);
                        var f = name.Substring(0, name.Length - d.Count - 3);
                        return $"{dir}{f} ({n + 1}){ext}";
                    }
                }
                return $"{dir}{name} (1){ext}";
            }
        }
    }
}
