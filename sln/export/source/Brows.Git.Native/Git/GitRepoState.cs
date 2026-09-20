namespace Brows.Git; 
public enum GitRepoState {
    None = 0,
    Merge = 1,
    Revert = 2,
    RevertSequence = 3,
    Cherrypick = 4,
    CherrypickSequence = 5,
    Bisect = 6,
    Rebase = 7,
    RebaseInteractive = 8,
    RebaseMerge = 9,
    ApplyMailbox = 10,
    ApplyMailboxOrRebase = 11
}
