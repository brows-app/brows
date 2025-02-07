#ifndef BROWS_URL_INTERNAL
#define BROWS_URL_INTERNAL

#include <assert.h>
#include <malloc.h>
#include <stdint.h>
#include <string.h>
#include "brows_url.h"
#include "curl/curl.h"

#define brows_url_internal                      brows_EXTERN_C
#define brows_url_LOG(SEVERITY, FORMAT, ...)    brows_LOG(SEVERITY, "brows_url: " FORMAT, __VA_ARGS__)
#define brows_url_DEBUG(FORMAT, ...)            brows_url_LOG(brows_LogLevel_debug, FORMAT, __VA_ARGS__)
#define brows_url_INFO(FORMAT, ...)             brows_url_LOG(brows_LogLevel_info, FORMAT, __VA_ARGS__)
#define brows_url_WARN(FORMAT, ...)             brows_url_LOG(brows_LogLevel_warn, FORMAT, __VA_ARGS__)
#define brows_url_ERROR(FORMAT, ...)            brows_url_LOG(brows_LogLevel_error, FORMAT, __VA_ARGS__)
#define brows_url_CRITICAL(FORMAT, ...)         brows_url_LOG(brows_LogLevel_critical, FORMAT, __VA_ARGS__)

#endif
