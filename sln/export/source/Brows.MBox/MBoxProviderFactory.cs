using Brows.Composition;
using Brows.Panels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Creates the provider of a mailbox file.
/// </summary>
internal sealed class MBoxProviderFactory : ProviderFactory<MBoxProvider> {
    private readonly MBoxConfig _Config;

    public MBoxProviderFactory() {
    }

    internal MBoxProviderFactory(MBoxConfig config) {
        _Config = config;
    }

    /// <summary>
    /// Gets or sets the service that corrects the case of the name of a file.
    /// </summary>
    public ICaseCorrectFile CaseCorrectFile {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the translation of the application.
    /// </summary>
    public ITranslationExport Translation {
        get;
        set;
    }

    protected sealed override async Task<MBoxProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
        if (string.IsNullOrWhiteSpace(id)) {
            return null;
        }
        if (MBoxPath.IsMailbox(id) == false) {
            return null;
        }
        var path = await Task.Run(cancellationToken: token, function: () => {
            var file = new FileInfo(id);
            if (file.Exists == false) {
                return null;
            }
            return file.FullName;
        });
        if (path == null) {
            return null;
        }
        var caseCorrectFile = CaseCorrectFile;
        if (caseCorrectFile != null) {
            var correctCase = default(string);
            var corrected = await caseCorrectFile.Work(path, result => correctCase = result, token);
            var correctCaseIsAvailable = corrected && string.IsNullOrWhiteSpace(correctCase) == false;
            if (correctCaseIsAvailable) {
                path = correctCase;
            }
        }
        return new MBoxProvider(this, path, _Config);
    }
}
