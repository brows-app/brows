namespace Domore.Runtime.Win32 {
    public enum FOFX : uint {
        NOSKIPJUNCTIONS = 0x00010000,
        PREFERHARDLINK = 0x00020000,
        SHOWELEVATIONPROMPT = 0x00040000,
        RECYCLEONDELETE = 0x00080000,
        EARLYFAILURE = 0x00100000,
        PRESERVEFILEEXTENSIONS = 0x00200000,
        KEEPNEWERFILE = 0x00400000,
        NOCOPYHOOKS = 0x00800000,
        NOMINIMIZEBOX = 0x01000000,
        MOVEACLSACROSSVOLUMES = 0x02000000,
        DONTDISPLAYSOURCEPATH = 0x04000000,
        DONTDISPLAYDESTPATH = 0x08000000,
        REQUIREELEVATION = 0x10000000,
        ADDUNDORECORD = 0x20000000
    }
}
