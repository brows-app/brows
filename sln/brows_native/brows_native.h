#ifndef BROWS_NATIVE_H
#define BROWS_NATIVE_H

#include <inttypes.h>

#ifndef     brows_EXTERN_C
#ifdef      __cplusplus
#define     brows_EXTERN_C                      extern "C"
#else
#define     brows_EXTERN_C
#endif
#endif

#define     brows_DLLIMPORT                     brows_EXTERN_C __declspec(dllimport)
#define     brows_DLLEXPORT                     brows_EXTERN_C __declspec(dllexport)

#ifdef      brows_native_IMPORT
#define     brows_native                        brows_DLLIMPORT
#endif

#ifdef      brows_native_EXPORT
#define     brows_native                        brows_DLLEXPORT
#endif

#ifndef     brows_native
#define     brows_native                        brows_EXTERN_C
#endif

#ifndef     brows_INTERNAL
#define     brows_INTERNAL                      brows_EXTERN_C
#endif

#define     brows_ERROR                         int32_t
#define     brows_ERROR_NONE                    0
#define     brows_ERROR_CANCELED                1
#define     brows_ERROR_LOG_SIZE                -19
#define     brows_ERROR_LOG_ALLOC               -20
#define     brows_ERROR_LOG_PRINT               -21

#define     brows_LOG(SEVERITY, FORMAT, ...)    brows_log(SEVERITY, FORMAT, __VA_ARGS__)
#define     brows_LOG_DEBUG(FORMAT, ...)        brows_LOG(brows_LogLevel_debug, FORMAT, __VA_ARGS__)
#define     brows_LOG_INFO(FORMAT, ...)         brows_LOG(brows_LogLevel_info, FORMAT, __VA_ARGS__)
#define     brows_LOG_WARN(FORMAT, ...)         brows_LOG(brows_LogLevel_warn, FORMAT, __VA_ARGS__)
#define     brows_LOG_ERROR(FORMAT, ...)        brows_LOG(brows_LogLevel_error, FORMAT, __VA_ARGS__)
#define     brows_LOG_CRITICAL(FORMAT, ...)     brows_LOG(brows_LogLevel_critical, FORMAT, __VA_ARGS__)

enum brows_LogLevel {
            brows_LogLevel_none     = 0,
            brows_LogLevel_debug    = 1,
            brows_LogLevel_info     = 2,
            brows_LogLevel_warn     = 3,
            brows_LogLevel_error    = 4,
            brows_LogLevel_critical = 5
};

typedef     struct brows_Canceler               brows_Canceler;
typedef     enum brows_LogLevel                 brows_LogLevel;

struct brows_Canceler {
    int32_t(*canceled)(brows_Canceler*);
};

brows_native    brows_ERROR                     brows_init(void);
brows_native    brows_ERROR                     brows_log(brows_LogLevel, const char*, ...);
brows_native    void                            brows_logged(brows_ERROR(*)(brows_LogLevel, const char*));
brows_native    void                            brows_logging(int32_t(*)(brows_LogLevel));

brows_native    int32_t                         brows_canceled(brows_Canceler*);

#endif
