#include "brows_native.h"
#include <assert.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>

static  brows_ERROR (*logged_handler)   (brows_LogLevel, const char*);
static  int32_t     (*logging_handler)  (brows_LogLevel);

void brows_logged(brows_ERROR(*handler)(brows_LogLevel, const char* message)) {
    logged_handler = handler;
}

void brows_logging(int32_t(*handler)(brows_LogLevel)) {
    logging_handler = handler;
}

brows_ERROR brows_log(brows_LogLevel severity, const char* format, ...) {
    if (!logged_handler) {
        return brows_ERROR_NONE;
    }
    if (logging_handler) {
        if (!logging_handler(severity)) {
            return brows_ERROR_NONE;
        }
    }

    va_list arg_list;
    va_list arg_copy;
    size_t buffer_size;
    char* buffer = NULL;
    brows_ERROR err = brows_ERROR_NONE;

    va_start(arg_list, format);
    va_copy(arg_copy, arg_list);

    int size = vsnprintf(NULL, 0, format, arg_list);
    if (size == 0) {
        goto cleanup;
    }
    if (size < 0) {
        err = brows_ERROR_LOG_SIZE;
        goto cleanup;
    }
    buffer_size = size + 1;
    buffer = calloc(buffer_size, sizeof(char));
    if (!buffer) {
        err = brows_ERROR_LOG_ALLOC;
        goto cleanup;
    }
    int written = vsnprintf(buffer, buffer_size, format, arg_copy);
    if (written < 0) {
        err = brows_ERROR_LOG_PRINT;
        goto cleanup;
    }
    err = logged_handler(severity, buffer);
cleanup:
    va_end(arg_copy);
    va_end(arg_list);
    free(buffer);
    return err;
}

brows_ERROR brows_init(void) {
    brows_LOG_INFO("%s", "brows native init");
    return brows_ERROR_NONE;
}

int32_t brows_canceled(brows_Canceler* p) {
    if (!p) {
        return 0;
    }
    if (!p->canceled) {
        return 0;
    }
    return p->canceled(p);
}
