#include "brows_ssh_internal.h"

struct brows_ssh_KnownHost {
    brows_ssh_KnownHost_Status status;
    char* key_base64;
    char* name_plain;
};

const char* brows_ssh_KnownHost_get_key_base64(brows_ssh_KnownHost* p) {
    assert(p);
    return p->key_base64;
}

const char* brows_ssh_KnownHost_get_name_plain(brows_ssh_KnownHost* p) {
    assert(p);
    return p->name_plain;
}

brows_ssh brows_ssh_KnownHost_Status brows_ssh_KnownHost_get_status(brows_ssh_KnownHost* p) {
    assert(p);
    return p->status;
}

brows_ERROR brows_ssh_KnownHost_init(brows_ssh_KnownHost* p, struct libssh2_knownhost* s, int check) {
    assert(p);
    p->key_base64 = NULL;
    p->name_plain = NULL;
    switch (check) {
    case LIBSSH2_KNOWNHOST_CHECK_NOTFOUND:
        p->status = brows_ssh_KnownHost_Status_NotFound;
        break;
    case LIBSSH2_KNOWNHOST_CHECK_MATCH:
        p->status = brows_ssh_KnownHost_Status_Match;
        break;
    case LIBSSH2_KNOWNHOST_CHECK_MISMATCH:
        p->status = brows_ssh_KnownHost_Status_Mismatch;
        break;
    case LIBSSH2_KNOWNHOST_CHECK_FAILURE:
    default:
        p->status = brows_ssh_KnownHost_Status_Error;
        break;
    }
    if (s) {
        if (s->key) {
            size_t len = strnlen_s(s->key, 1000);
            p->key_base64 = calloc(len + 1, sizeof(char));
            if (!p->key_base64) {
                brows_LOG_ERROR("calloc (key) > %s", "NULL");
                brows_ssh_KnownHost_destroy(p);
                return brows_ssh_ERROR_CALLOC;
            }
            errno_t err = strncpy_s(p->key_base64, len + 1, s->key, len);
            if (err) {
                brows_LOG_ERROR("strncpy_s (key) > %" PRId32, err);
                brows_ssh_KnownHost_destroy(p);
                return brows_ssh_ERROR_STRNCPY_S;
            }
        }
        if (s->name) {
            size_t len = strnlen_s(s->name, 1000);
            p->name_plain = calloc(len + 1, sizeof(char));
            if (!p->name_plain) {
                brows_LOG_ERROR("calloc (name) > %s", "NULL");
                brows_ssh_KnownHost_destroy(p);
                return brows_ssh_ERROR_CALLOC;
            }
            errno_t err = strncpy_s(p->name_plain, len + 1, s->name, len);
            if (err) {
                brows_LOG_ERROR("strncpy_s (name) > %" PRId32, err);
                brows_ssh_KnownHost_destroy(p);
                return brows_ssh_ERROR_STRNCPY_S;
            }
        }
    }
    return brows_ERROR_NONE;
}

brows_ssh_KnownHost* brows_ssh_KnownHost_create(void) {
    brows_ssh_KnownHost* p = calloc(1, sizeof(brows_ssh_KnownHost));
    if (p) {
        p->status = brows_ssh_KnownHost_Status_Uninitialized;
        p->key_base64 = NULL;
        p->name_plain = NULL;
    }
    return p;
}

void brows_ssh_KnownHost_destroy(brows_ssh_KnownHost* p) {
    if (p) {
        if (p->key_base64) {
            free(p->key_base64);
        }
        if (p->name_plain) {
            free(p->name_plain);
        }
        free(p);
    }
}
