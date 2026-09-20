#ifndef BROWS_WIN32_H
#define BROWS_WIN32_H

#include "brows_framework.h"

#ifdef      brows_win32_IMPORT
#define     brows_win32                             brows_DLLIMPORT
#endif

#ifdef      brows_win32_EXPORT
#define     brows_win32                             brows_DLLEXPORT
#endif

#ifndef     brows_win32
#define     brows_win32                             brows_EXTERN_C
#endif

brows_win32 brows_ERROR                             brows_win32_init(void);
brows_win32 brows_ERROR                             brows_win32_exit(void);

#endif
