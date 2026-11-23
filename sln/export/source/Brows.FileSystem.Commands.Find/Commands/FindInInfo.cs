using Domore.IO.Extensions;
using Domore.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class FindInInfo {
    private readonly DirectoryInfo Root;
    private readonly FindSettings Settings;
    private readonly Func<DecodedTextOptions> CreateDecoder;

    private async Task<FindEntryResult> FindFile(FileInfo file, CancellationToken token) {
        var rows = new List<FoundInInfo>();
        var matched = false;
        var tried = false;
        var errors = 0;
        var skipped = 0;
        var truncated = false;
        if (Settings.In.HasFlag(FindIn.FileName)) {
            tried = true;
            try {
                if (Settings.Matcher.Matches(file.Name)) {
                    matched = true;
                    rows.Add(new FoundInFileName(file, Root));
                }
            }
            catch (RegexMatchTimeoutException ex) {
                errors++;
                rows.Add(new FoundInFileError(file, Root, ex));
            }
        }
        token.ThrowIfCancellationRequested();
        if (Settings.In.HasFlag(FindIn.FileText)) {
            tried = true;
            var options = CreateDecoder();
            using var buffers = options.Disposable();
            try {
                var source = new FindFileTextSource(file, Settings.MaximumContentBytes);
                var decoded = await source.DecodeText(default(DecodedTextDelegate), options, token)
                    .ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (decoded?.Success == true) {
                    var count = 0;
                    var text = decoded.Text();
                    foreach (var match in Settings.Matcher.FindInText(text, Settings.MaximumPreviewLength, token)) {
                        if (count == Settings.MaximumMatchesPerFile) {
                            truncated = true;
                            break;
                        }
                        matched = true;
                        count++;
                        truncated |= match.MatchLength > Settings.MaximumPreviewLength;
                        rows.Add(new FoundInFileText(file, Root, match, Translation));
                    }
                }
            }
            catch (RegexMatchTimeoutException ex) {
                errors++;
                rows.Add(new FoundInFileError(file, Root, ex));
            }
            catch (FindContentLimitException) {
                skipped = 1;
            }
            catch (IOException ex) {
                errors = 1;
                rows.Add(new FoundInFileError(file, Root, ex));
            }
            catch (UnauthorizedAccessException ex) {
                errors = 1;
                rows.Add(new FoundInFileError(file, Root, ex));
            }
        }
        token.ThrowIfCancellationRequested();
        return new FindEntryResult(file, rows.ToArray(), tried, matched, errors, skipped, truncated);
    }

    private async Task<FindEntryResult> ProcessIncluded(FileSystemInfo info, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (info is FileInfo file) {
            return await FindFile(file, token).ConfigureAwait(false);
        }
        var findDirectory = info is DirectoryInfo && Settings.In.HasFlag(FindIn.DirectoryName);
        if (findDirectory) {
            try {
                var matched = Settings.Matcher.Matches(info.Name);
                token.ThrowIfCancellationRequested();
                FoundInInfo[] rows = matched ? [new FoundInDirectoryName((DirectoryInfo)info, Root)] : [];
                return new FindEntryResult(info, rows, Tried: true, Matched: matched);
            }
            catch (RegexMatchTimeoutException) {
                return new FindEntryResult(info, [], Tried: true, Errors: 1);
            }
        }
        return new FindEntryResult(info, []);
    }

    internal ITranslation Translation { get; init; }

    internal FindInInfo(DirectoryInfo root, Find.Parameter parameter) : this(root, new FindSettings(parameter)) {
    }

    internal FindInInfo(DirectoryInfo root, FindSettings settings, Func<DecodedTextOptions> createDecoder = null) {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        CreateDecoder = createDecoder ?? settings.CreateDecoder;
    }

    internal async Task<FindEntryResult> Process(FileSystemInfo info, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(info);
        token.ThrowIfCancellationRequested();
        foreach (var matcher in Settings.Include) {
            bool included;
            try {
                included = matcher.Matches(info.FullName);
            }
            catch (RegexMatchTimeoutException ex) {
                FoundInInfo[] rows = info is FileInfo file ? [new FoundInFileError(file, Root, ex)] : [];
                return new FindEntryResult(info, rows, Errors: 1);
            }
            if (included) {
                return await ProcessIncluded(info, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
        }
        if (Settings.Include.Count > 0) {
            return new FindEntryResult(info, []);
        }
        return await ProcessIncluded(info, token).ConfigureAwait(false);
    }
}
