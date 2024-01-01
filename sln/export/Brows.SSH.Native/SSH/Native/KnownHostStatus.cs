namespace Brows.SSH.Native {
    public enum KnownHostStatus {
        Uninitialized = 0,
        NotFound = 1,
        Match = 2,
        Mismatch = 3,
        Error = 4
    }
}
