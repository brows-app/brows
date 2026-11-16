/*
 * The 7-Zip interface ids are defined in this translation unit only. Every
 * other translation unit includes the same headers without INITGUID, which
 * turns the definitions into declarations.
 */
#define INITGUID
#include "brows_7z_internal.h"

/*
 * The class id of the 7z archive handler. The archive handlers of 7-Zip all
 * share the same class id and are told apart by the id in the fifth byte of
 * the last part of the guid. The id of the 7z format is 7.
 */
EXTERN_C const GUID CLSID_Format7z = {
    0x23170F69, 0x40C1, 0x278A,
    { 0x10, 0x00, 0x00, 0x01, 0x10, 0x07, 0x00, 0x00 }
};
