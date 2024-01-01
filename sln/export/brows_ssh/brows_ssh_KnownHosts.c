#include "brows_ssh_internal.h"

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
    assert(p->agent);
    return p->agent;
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
    LIBSSH2_KNOWNHOSTS* knownhosts = brows_ssh_KnownHosts_get_agent(p);
    int readfile = libssh2_knownhost_readfile(knownhosts, file, LIBSSH2_KNOWNHOST_FILE_OPENSSH);
    if (0 > readfile) {
        brows_LOG_ERROR("libssh2_knownhost_readfile > %d", readfile);
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
        brows_LOG_ERROR("libssh2_knownhost_checkp > %d", result);
        return brows_ssh_ERROR_LIBSSH2_KNOWNHOST_CHECKP;
    }
    if (known_host) {
        brows_ERROR err = brows_ssh_KnownHost_init(known_host, knownhost, result);
        if (err) return err;
    }
    return brows_ERROR_NONE;
}
