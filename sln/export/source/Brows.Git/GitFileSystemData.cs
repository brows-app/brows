using Brows.Exports;
using Brows.Git;
using System;
using System.Threading.Tasks;
using GITREPOHEAD = Brows.GitRepoHead;
using GITREPOSTATE = Brows.Git.GitRepoState;

namespace Brows;

internal static class GitFileSystemData {
    public sealed class GitRepoState : Definition<GITREPOSTATE?> {
        protected sealed override async Task<GITREPOSTATE?> GetValue(IFileSystemEntry entry,
                                                                     Action<GITREPOSTATE?> progress,
                                                                     CancellationToken token) {
            var repo = await DirectoryRepo(entry, token);
            if (repo == null) {
                return null;
            }
            return repo.State;
        }
    }

    public sealed class GitRepoHead : Definition<GITREPOHEAD> {
        protected sealed override async Task<GITREPOHEAD> GetValue(IFileSystemEntry entry,
                                                                   Action<GITREPOHEAD> progress,
                                                                   CancellationToken token) {
            var repo = await DirectoryRepo(entry, token);
            if (repo == null) {
                return null;
            }
            var head = await repo.Head(token);
            var status = await head.CompareRemote(token);
            return new(
                name: head.Name,
                status: status);
        }
    }

    public abstract class Definition<TValue> : EntryDataExport<IFileSystemEntry, TValue> {
        protected Task<GitRepo> DirectoryRepo(IFileSystemEntry entry, CancellationToken token) {
            if (token.IsCancellationRequested) {
                return Task.FromCanceled<GitRepo>(token);
            }
            if (entry is null) {
                return Task.FromResult(default(GitRepo));
            }
            if (entry.Kind != FileSystemEntryKind.Directory) {
                return Task.FromResult(default(GitRepo));
            }
            var key = $"{typeof(GitFileSystemData).Name}.{nameof(DirectoryRepo)}";
            var path = entry.Path;
            return entry.Task(
                key: key,
                token: token,
                factory: token => GitRepo.Find(path, search: false, token));
        }
    }
}
