using Brows.Config;
using Brows.Exports;
using System;
using System.IO;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Edit : FileSystemCommand<Edit.Parameter> {
        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) return false;
            if (active.HasFileSystemSelection(out var selection) == false) {
                return false;
            }
            return context.Operate(async (progress, token) => {
                var worked = false;
                var config = await Configure.File<FileSystemConfig>().Load(token);
                var editors = config?.Editor ?? new();
                foreach (var info in selection) {
                    var editor = default(string);
                    var extension = info is FileInfo file ? file.Extension : null;
                    if (extension != null) {
                        editor = config.Editor.TryGetValue(extension, out var extensionEditor)
                            ? extensionEditor
                            : null;
                        if (editor == null) {
                            var key = config.Editor.Keys.LastOrDefault(k => k
                                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Contains(extension, StringComparer.OrdinalIgnoreCase));
                            if (key != null) {
                                editor = config.Editor[extension] = config.Editor[key];
                            }
                        }
                    }
                    if (editor == null) {
                        editor = config.Editor.TryGetValue("*", out var defaultEditor)
                            ? defaultEditor
                            : null;
                    }
                    if (editor == null) {
                        continue;
                    }
                    var qualified = Path.IsPathFullyQualified(editor);
                    if (qualified == false) {
                        var locatedPath = default(string);
                        var locatedTask = LocateProgram?.Work(editor, set: result => locatedPath = result, token);
                        if (locatedTask != null) {
                            var located = await locatedTask;
                            if (located) {
                                editor = locatedPath;
                            }
                        }
                    }
                    var task = OpenFileWith?.Work(info.FullName, editor, token);
                    if (task != null) {
                        worked |= await task;
                    }
                }
                return worked;
            });
        }

        public ILocateProgram LocateProgram { get; set; }
        public IOpenFileWith OpenFileWith { get; set; }

        public class Parameter {
        }
    }
}
