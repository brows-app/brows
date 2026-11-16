#include "brows_ssh_internal.h"
#include <stdio.h>

struct brows_ssh_KnownHosts {
    brows_ssh_Conn* conn;
    LIBSSH2_KNOWNHOSTS* agent;
};

static LIBSSH2_KNOWNHOSTS* brows_ssh_KnownHosts_get_agent(brows_ssh_KnownHosts* p) {
    assert(p);
    if (p->agent) {
        return p->agent;
    }
    LIBSSH2_SESSION* session = brows_ssh_Conn_get_session(p->conn);
    p->agent = libssh2_knownhost_init(session);
    if (!p->agent) {
        brows_LOG_ERROR("libssh2_knownhost_init > %s", "NULL");
    }
    return p->agent;
}

static LIBSSH2_KNOWNHOSTS* brows_ssh_KnownHosts_reset_agent(brows_ssh_KnownHosts* p) {
    assert(p);
    if (p->agent) {
        libssh2_knownhost_free(p->agent);
        p->agent = NULL;
    }
    return brows_ssh_KnownHosts_get_agent(p);
}

brows_ssh_KnownHosts* brows_ssh_KnownHosts_create(brows_ssh_Conn* conn) {
    brows_ssh_KnownHosts* p = calloc(1, sizeof(brows_ssh_KnownHosts));
    if (p) {
        p->conn = conn;
        p->agent = NULL;
    }
    return p;
}

void brows_ssh_KnownHosts_destroy(brows_ssh_KnownHosts* p) {
    if (p) {
        if (p->agent) {
            libssh2_knownhost_free(p->agent);
        }
        free(p);
    }
}

brows_ERROR brows_ssh_KnownHosts_check(brows_ssh_KnownHosts* p, const char* file, brows_ssh_KnownHostName name, brows_ssh_KnownHostPort port, brows_ssh_KnownHost* known_host) {
    assert(p);
    size_t len;
    int type;
    LIBSSH2_SESSION* session = brows_ssh_Conn_get_session(p->conn);
    const char* fingerprint = libssh2_session_hostkey(session, &len, &type);
    if (!fingerprint) {
        brows_LOG_ERROR("libssh2_session_hostkey > %s", "NULL");
        return brows_ssh_ERROR_LIBSSH2_SESSION_HOSTKEY;
    }
    LIBSSH2_KNOWNHOSTS* knownhosts = brows_ssh_KnownHosts_reset_agent(p);
    if (!knownhosts) {
        return brows_ERROR_SSH_libssh2_knownhost_init;
    }
    int readfile = libssh2_knownhost_readfile(knownhosts, file, LIBSSH2_KNOWNHOST_FILE_OPENSSH);
    if (0 > readfile) {
        brows_LOG_ERROR("libssh2_knownhost_readfile > %" PRId32, readfile);
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_READFILE;
    }
    struct libssh2_knownhost* knownhost = NULL;
    int result = libssh2_knownhost_checkp(
        knownhosts,
        name,
        port,
        fingerprint,
        len,
        LIBSSH2_KNOWNHOST_TYPE_PLAIN | LIBSSH2_KNOWNHOST_KEYENC_RAW,
        &knownhost);
    if (LIBSSH2_KNOWNHOST_CHECK_FAILURE == result) {
        brows_LOG_ERROR("libssh2_knownhost_checkp > %" PRId32, result);
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_CHECKP;
    }
    if (known_host) {
        brows_ERROR err = brows_ssh_KnownHost_init(known_host, knownhost, result);
        if (err) return err;
    }
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_KnownHosts_trust(brows_ssh_KnownHosts* p,
                                       const char* file,
                                       brows_ssh_KnownHostName name,
                                       brows_ssh_KnownHostPort port,
                                       int32_t replace) {
    assert(p);
    if (!file || !name) {
        return brows_ssh_ERROR_STRCPY_S;
    }

    size_t key_length = 0;
    int hostkey_type = LIBSSH2_HOSTKEY_TYPE_UNKNOWN;
    LIBSSH2_SESSION* session = brows_ssh_Conn_get_session(p->conn);
    const char* hostkey = libssh2_session_hostkey(session, &key_length, &hostkey_type);
    if (!hostkey) {
        return brows_ssh_ERROR_LIBSSH2_SESSION_HOSTKEY;
    }

    LIBSSH2_KNOWNHOSTS* knownhosts = brows_ssh_KnownHosts_reset_agent(p);
    if (!knownhosts) {
        return brows_ERROR_SSH_libssh2_knownhost_init;
    }
    int read_result = libssh2_knownhost_readfile(knownhosts, file, LIBSSH2_KNOWNHOST_FILE_OPENSSH);
    if (read_result < 0) {
        brows_LOG_ERROR("libssh2_knownhost_readfile > %" PRId32, read_result);
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_READFILE;
    }

    struct libssh2_knownhost* knownhost = NULL;
    int check_result = libssh2_knownhost_checkp(
        knownhosts,
        name,
        port,
        hostkey,
        key_length,
        LIBSSH2_KNOWNHOST_TYPE_PLAIN | LIBSSH2_KNOWNHOST_KEYENC_RAW,
        &knownhost);
    if (check_result == LIBSSH2_KNOWNHOST_CHECK_FAILURE) {
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_CHECKP;
    }
    if (check_result == LIBSSH2_KNOWNHOST_CHECK_MATCH) {
        return brows_ERROR_NONE;
    }
    if (check_result == LIBSSH2_KNOWNHOST_CHECK_MISMATCH && !replace) {
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_CHECKP;
    }
    if (knownhost) {
        int delete_result = libssh2_knownhost_del(knownhosts, knownhost);
        if (delete_result < 0) {
            brows_LOG_ERROR("libssh2_knownhost_del > %" PRId32, delete_result);
            return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_CHECKP;
        }
    }

    char host_name[1024] = { 0 };
    if (port == 22) {
        errno_t copy_result = strcpy_s(host_name, sizeof(host_name), name);
        if (copy_result) {
            return brows_ssh_ERROR_STRNCPY_S;
        }
    }
    else {
        int format_result = sprintf_s(host_name, sizeof(host_name), "[%s]:%" PRId32, name, port);
        if (format_result < 0) {
            return brows_ssh_ERROR_STRNCPY_S;
        }
    }

    int key_type_flag =
        hostkey_type == LIBSSH2_HOSTKEY_TYPE_RSA ? LIBSSH2_KNOWNHOST_KEY_SSHRSA :
        hostkey_type == LIBSSH2_HOSTKEY_TYPE_DSS ? LIBSSH2_KNOWNHOST_KEY_SSHDSS :
        hostkey_type == LIBSSH2_HOSTKEY_TYPE_ECDSA_256 ? LIBSSH2_KNOWNHOST_KEY_ECDSA_256 :
        hostkey_type == LIBSSH2_HOSTKEY_TYPE_ECDSA_384 ? LIBSSH2_KNOWNHOST_KEY_ECDSA_384 :
        hostkey_type == LIBSSH2_HOSTKEY_TYPE_ECDSA_521 ? LIBSSH2_KNOWNHOST_KEY_ECDSA_521 :
        hostkey_type == LIBSSH2_HOSTKEY_TYPE_ED25519 ? LIBSSH2_KNOWNHOST_KEY_ED25519 :
        LIBSSH2_KNOWNHOST_KEY_UNKNOWN;
    int add_result = libssh2_knownhost_addc(
        knownhosts,
        host_name,
        NULL,
        hostkey,
        key_length,
        NULL,
        0,
        LIBSSH2_KNOWNHOST_TYPE_PLAIN | LIBSSH2_KNOWNHOST_KEYENC_RAW | key_type_flag,
        NULL);
    if (add_result < 0) {
        brows_LOG_ERROR("libssh2_knownhost_addc > %" PRId32, add_result);
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_CHECKP;
    }

    int write_result = libssh2_knownhost_writefile(knownhosts, file, LIBSSH2_KNOWNHOST_FILE_OPENSSH);
    if (write_result < 0) {
        brows_LOG_ERROR("libssh2_knownhost_writefile > %" PRId32, write_result);
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_WRITEFILE;
    }
    return brows_ERROR_NONE;
}
