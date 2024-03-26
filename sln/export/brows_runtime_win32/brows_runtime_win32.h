#ifndef BROWS_RUNTIME_WIN32_H
#define BROWS_RUNTIME_WIN32_H

#include "brows_native.h"

#ifdef      brows_runtime_win32_IMPORT
#define     brows_runtime_win32                             brows_DLLIMPORT
#endif

#ifdef      brows_runtime_win32_EXPORT
#define     brows_runtime_win32                             brows_DLLEXPORT
#endif

#ifndef     brows_runtime_win32
#define     brows_runtime_win32                             brows_EXTERN_C
#endif

brows_runtime_win32 brows_ERROR                             brows_runtime_win32_init(void);
brows_runtime_win32 brows_ERROR                             brows_runtime_win32_exit(void);

#endif
