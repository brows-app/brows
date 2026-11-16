using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

public static class FileProtocolUri {
    /*
     * Returns the URI with any password replaced by asterisks, for logging.
     */
    internal static string Redact(Uri uri) {
        if (uri is null) {
            return null;
        }
        var hasPassword = uri.UserInfo.Contains(':', StringComparison.Ordinal);
        if (hasPassword is false) {
            return uri.ToString();
        }
        return new UriBuilder(uri) { Password = "****" }.Uri.ToString();
    }

    /// <summary>
    /// Appends literal slash-delimited path segments to an absolute file protocol URI.
    /// </summary>
    /// <param name="uri">The absolute base URI.</param>
    /// <param name="paths">The literal path segments to append.</param>
    /// <param name="includeTrailingSlash">Whether the result path ends in a slash.</param>
    /// <returns>The extended URI.</returns>
    /// <exception cref="ArgumentException">A path contains a dot segment.</exception>
    public static Uri Extend(Uri uri, string[] paths, bool includeTrailingSlash) {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(paths);
        EnsureAbsolute(uri, nameof(uri));
        var bld = new UriBuilder(uri);
        var ftpServerAbsoluteRoot = IsFtp(uri) && string.Equals(bld.Path, "//", StringComparison.Ordinal);
        var path = ftpServerAbsoluteRoot ? "//" : bld.Path.TrimEnd('/');
        foreach (var part in paths) {
            ArgumentNullException.ThrowIfNull(part, nameof(paths));
            var segments = part.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var hasDotSegment = segments.Any(segment => segment is "." or "..");
            if (hasDotSegment) {
                throw new ArgumentException("A path cannot contain a dot segment.", nameof(paths));
            }
            var escapedPart = EscapePathPart(part);
            if (escapedPart.Length > 0) {
                var pathIsServerAbsoluteRoot = IsFtp(uri) && string.Equals(path, "//", StringComparison.Ordinal);
                path = pathIsServerAbsoluteRoot
                    ? "//" + escapedPart
                    : (path + '/' + escapedPart).TrimEnd('/');
                ftpServerAbsoluteRoot = false;
            }
        }
        var includeTrailingSeparator = includeTrailingSlash && !ftpServerAbsoluteRoot;
        bld.Path = includeTrailingSeparator
            ? path + '/'
            : path;
        return bld.Uri;
    }

    /// <summary>
    /// Gets the containing directory URI, or <see langword="null"/> when the URI is at its root.
    /// </summary>
    /// <param name="uri">The absolute URI whose parent is requested.</param>
    /// <returns>The parent URI, or <see langword="null"/> at the root.</returns>
    public static Uri Parent(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        EnsureAbsolute(uri, nameof(uri));
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Length == 0) {
            return null;
        }
        var separator = path.LastIndexOf('/');
        var parentPath = separator <= 0
            ? "/"
            : path[..(separator + 1)];
        return new UriBuilder(uri) { Path = parentPath }.Uri;
    }

    /// <summary>
    /// Gets the slash-delimited path from a base URI to a descendant URI.
    /// </summary>
    /// <param name="baseUri">The absolute base URI.</param>
    /// <param name="uri">The absolute descendant URI.</param>
    /// <returns>The decoded descendant path relative to the base URI.</returns>
    /// <exception cref="ArgumentException">
    /// The URIs do not identify the same authority or the URI is not a descendant of the base URI,
    /// or a path segment decodes into a separator or dot segment.
    /// </exception>
    public static string RelativePath(Uri baseUri, Uri uri) {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(uri);
        EnsureAbsolute(baseUri, nameof(baseUri));
        EnsureAbsolute(uri, nameof(uri));

        var sameAuthority = Uri.Compare(
            baseUri,
            uri,
            UriComponents.SchemeAndServer,
            UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
        var sameUserInfo = string.Equals(baseUri.UserInfo, uri.UserInfo, StringComparison.Ordinal);
        if (!sameAuthority || !sameUserInfo) {
            throw new ArgumentException("The URI must use the same authority as the base URI.", nameof(uri));
        }

        var ftpPathsUseDifferentRootModes = IsFtp(baseUri) &&
                                            IsFtp(uri) &&
                                            IsServerAbsolutePath(baseUri) != IsServerAbsolutePath(uri);
        if (ftpPathsUseDifferentRootModes) {
            throw new ArgumentException("The URI must use the same FTP path mode as the base URI.", nameof(uri));
        }

        var baseIsServerAbsoluteRoot = IsFtp(baseUri) &&
                                       string.Equals(baseUri.AbsolutePath, "//", StringComparison.Ordinal);
        var basePath = baseIsServerAbsoluteRoot
            ? "//"
            : baseUri.AbsolutePath.TrimEnd('/');
        var uriPath = uri.AbsolutePath;
        if (string.Equals(basePath, uriPath.TrimEnd('/'), StringComparison.Ordinal)) {
            return "";
        }

        if (baseIsServerAbsoluteRoot) {
            return DecodePath(uriPath[2..], nameof(uri));
        }

        var prefix = basePath.Length == 0
            ? "/"
            : basePath + "/";
        if (!uriPath.StartsWith(prefix, StringComparison.Ordinal)) {
            throw new ArgumentException("The URI must be a descendant of the base URI.", nameof(uri));
        }
        return DecodePath(uriPath[prefix.Length..], nameof(uri));
    }

    /// <summary>
    /// Combines a base URI and a literal relative protocol path into an absolute protocol path.
    /// </summary>
    /// <param name="uri">The absolute base URI.</param>
    /// <param name="relativePath">A slash-delimited literal path relative to the base URI.</param>
    /// <returns>The decoded absolute path, using forward slashes.</returns>
    /// <exception cref="ArgumentException">
    /// The relative path is rooted, empty, or contains a dot segment, or the base URI path
    /// contains an encoded separator or dot segment.
    /// </exception>
    public static string CombinePath(Uri uri, string relativePath) {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(relativePath);
        EnsureAbsolute(uri, nameof(uri));
        _ = DecodePath(uri.AbsolutePath, nameof(uri));
        var isRooted = relativePath.StartsWith("/", StringComparison.Ordinal);
        if (isRooted) {
            throw new ArgumentException("The path must be relative.", nameof(relativePath));
        }

        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var hasNoParts = parts.Length == 0;
        var hasDotSegment = parts.Any(part => part is "." or "..");
        if (hasNoParts || hasDotSegment) {
            throw new ArgumentException("The path must contain ordinary relative path segments.", nameof(relativePath));
        }
        var extended = Extend(uri, parts, includeTrailingSlash: false);
        return DecodePath(extended.AbsolutePath, nameof(relativePath));
    }

    internal static bool SameResource(Uri first, Uri second) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        EnsureAbsolute(first, nameof(first));
        EnsureAbsolute(second, nameof(second));
        return SameAuthority(first, second) &&
               PathSegments(first).SequenceEqual(PathSegments(second), StringComparer.Ordinal);
    }

    internal static bool CouldBeSameResource(Uri first, Uri second) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        EnsureAbsolute(first, nameof(first));
        EnsureAbsolute(second, nameof(second));
        if (SameResource(first, second)) {
            return true;
        }
        var authoritiesDiffer = !SameAuthority(first, second);
        if (authoritiesDiffer) {
            return false;
        }
        return HasDifferentFtpRootMode(first, second);
    }

    internal static bool IsSameOrDescendant(Uri ancestor, Uri descendant) {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(descendant);
        EnsureAbsolute(ancestor, nameof(ancestor));
        EnsureAbsolute(descendant, nameof(descendant));
        if (!SameAuthority(ancestor, descendant)) {
            return false;
        }
        var ancestorSegments = PathSegments(ancestor);
        var descendantSegments = PathSegments(descendant);
        return HasPathPrefix(ancestorSegments, descendantSegments);
    }

    internal static bool CouldBeSameOrDescendant(Uri ancestor, Uri descendant) {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(descendant);
        EnsureAbsolute(ancestor, nameof(ancestor));
        EnsureAbsolute(descendant, nameof(descendant));
        if (IsSameOrDescendant(ancestor, descendant)) {
            return true;
        }
        var authoritiesDiffer = !SameAuthority(ancestor, descendant);
        if (authoritiesDiffer) {
            return false;
        }
        return HasDifferentFtpRootMode(ancestor, descendant);
    }

    private static bool HasDifferentFtpRootMode(Uri first, Uri second) {
        /*
         * FTP paths with one leading slash are relative to the login directory; two are server-absolute.
         * Without the login working directory, different modes can still identify the same resource.
         */
        var bothAreFtp = IsFtp(first) && IsFtp(second);
        if (!bothAreFtp) {
            return false;
        }
        var firstSegments = PathSegments(first);
        var secondSegments = PathSegments(second);
        return !string.Equals(firstSegments[0], secondSegments[0], StringComparison.Ordinal);
    }

    private static bool HasPathPrefix(IReadOnlyList<string> prefix, IReadOnlyList<string> path) {
        if (prefix.Count > path.Count) {
            return false;
        }
        for (var i = 0; i < prefix.Count; i++) {
            if (!string.Equals(prefix[i], path[i], StringComparison.Ordinal)) {
                return false;
            }
        }
        return true;
    }

    private static bool SameAuthority(Uri first, Uri second) {
        return string.Equals(Scheme(first), Scheme(second), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.IdnHost, second.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               Port(first) == Port(second) &&
               string.Equals(UserName(first), UserName(second), StringComparison.Ordinal);
    }

    private static string Scheme(Uri uri) {
        return uri.Scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase)
            ? "ftp"
            : uri.Scheme;
    }

    private static bool IsFtp(Uri uri) {
        var isImplicitFtps = uri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase);
        return isImplicitFtps || Scheme(uri).Equals("ftp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsServerAbsolutePath(Uri uri) {
        return IsFtp(uri) && uri.AbsolutePath.StartsWith("//", StringComparison.Ordinal);
    }

    private static int Port(Uri uri) {
        if (uri.Port >= 0) {
            return uri.Port;
        }
        return uri.Scheme.ToLowerInvariant() switch {
            "sftp" => 22,
            "ftps" => 990,
            "ftpes" => 21,
            _ => uri.Port
        };
    }

    private static string UserName(Uri uri) {
        var userInfo = uri.UserInfo;
        var passwordSeparator = userInfo.IndexOf(':');
        var escapedUserName = passwordSeparator < 0
            ? userInfo
            : userInfo[..passwordSeparator];
        var isSftp = uri.Scheme.Equals("sftp", StringComparison.OrdinalIgnoreCase);
        var userNameIsMissing = string.IsNullOrWhiteSpace(escapedUserName);
        var useDefaultSftpUserName = isSftp && userNameIsMissing;
        if (useDefaultSftpUserName) {
            return Environment.UserName;
        }
        return Uri.UnescapeDataString(escapedUserName);
    }

    private static string[] PathSegments(Uri uri) {
        var path = uri.AbsolutePath;
        var leadingSlashes = 0;
        var hasSlashAtCurrentPosition = leadingSlashes < path.Length && path[leadingSlashes] == '/';
        while (hasSlashAtCurrentPosition) {
            leadingSlashes++;
            hasSlashAtCurrentPosition = leadingSlashes < path.Length && path[leadingSlashes] == '/';
        }
        var pathStartsWithMultipleSlashes = leadingSlashes > 1;
        var isSftp = uri.Scheme.Equals("sftp", StringComparison.OrdinalIgnoreCase);
        var relativePath = path[leadingSlashes..];
        var sftpPathHasMultipleLeadingSlashes = isSftp && pathStartsWithMultipleSlashes;
        if (sftpPathHasMultipleLeadingSlashes) {
            leadingSlashes = 1;
        }
        while (relativePath.EndsWith("/", StringComparison.Ordinal)) {
            relativePath = relativePath[..^1];
        }
        var segments = relativePath.Length == 0
            ? Array.Empty<string>()
            : relativePath.Split('/', isSftp
                ? StringSplitOptions.RemoveEmptyEntries
                : StringSplitOptions.None);
        return new[] { new string('/', leadingSlashes) }
            .Concat(segments)
            .Select(Uri.UnescapeDataString)
            .ToArray();
    }

    private static string EscapePathPart(string part) {
        var segments = part
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);
        return string.Join('/', segments);
    }

    private static string DecodePath(string path, string parameterName) {
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++) {
            var segment = Uri.UnescapeDataString(segments[i]);
            var isDotSegment = segment is "." or "..";
            var hasSlash = segment.Contains('/');
            if (isDotSegment || hasSlash) {
                throw new ArgumentException(
                    "A path segment cannot decode into a separator or dot segment.",
                    parameterName);
            }
            segments[i] = segment;
        }
        return string.Join('/', segments);
    }

    private static void EnsureAbsolute(Uri uri, string parameterName) {
        if (!uri.IsAbsoluteUri) {
            throw new ArgumentException("The URI must be absolute.", parameterName);
        }
        var isFtp = IsFtp(uri);
        var ftpPathHasUnsupportedRootPrefix = isFtp &&
                                              uri.AbsolutePath.StartsWith("///", StringComparison.Ordinal);
        if (ftpPathHasUnsupportedRootPrefix) {
            throw new ArgumentException(
                "FTP paths cannot have three or more leading slashes.",
                parameterName);
        }
    }
}
