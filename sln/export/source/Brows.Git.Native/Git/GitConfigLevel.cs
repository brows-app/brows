namespace Brows.Git; 
public enum GitConfigLevel {
    ProgramData = 1,
    System = 2,
    XDG = 3,
    Global = 4,
    Local = 5,
    Worktree = 6,
    App = 7,
    Highest = -1
}
