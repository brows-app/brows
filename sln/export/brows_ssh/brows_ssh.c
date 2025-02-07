#include "brows_ssh_internal.h"
#include <inttypes.h>

brows_ERROR brows_ssh_init(void) {
    int rc = libssh2_init(0);
    if (rc) {
        brows_LOG_ERROR("libssh2_init error > %" PRId32, rc);
        return brows_ssh_ERROR_LIBSSH2_INIT;
    }
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_exit(void) {
    libssh2_exit();
    return brows_ERROR_NONE;
}

const char* brows_ssh_name(brows_ERROR error) {
    switch (error) {
        case brows_ERROR_SSH_AUTHENTICATION_FAILED: return "AUTHENTICATION_FAILED";
        case brows_ERROR_SSH_PASSWORD_EXPIRED:      return "PASSWORD_EXPIRED";
        case brows_ERROR_SSH_PUBLIC_KEY_UNVERIFIED: return "PUBLIC_KEY_UNVERIFIED";
    }
    return NULL;
}
