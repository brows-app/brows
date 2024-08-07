﻿using System;
using System.IO.Compression;

namespace Brows.IO.Compression {
    internal static class ZipArchiveExtension {
        public static void Delete(this ZipArchive zipArchive, string entryName) {
            ArgumentNullException.ThrowIfNull(zipArchive);
            for (; ; ) {
                var entry = zipArchive.GetEntry(entryName);
                if (entry == null) {
                    break;
                }
                entry.Delete();
            }
        }
    }
}
