using System;

namespace Brows {
    public static class FileProtocolUri {
        public static Uri Extend(Uri uri, string[] paths, bool includeTrailingSlash) {
            var bld = new UriBuilder(uri);
            var path = bld.Path.TrimEnd('/');
            foreach (var part in paths) {
                path = (path + '/' + part).TrimEnd('/');
            }
            bld.Path = includeTrailingSlash
                ? path + '/'
                : path;
            return bld.Uri;
        }
    }
}
