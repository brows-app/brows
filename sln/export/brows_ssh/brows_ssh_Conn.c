#include "brows_ssh_internal.h"

#pragma comment(lib, "Ws2_32.lib")

struct brows_ssh_Conn {
    brows_ssh_Host                  host;
    brows_ssh_HostFamily            host_family;
    brows_ssh_Port                  port;
    char*                           username;
    char*                           fingerprint;
    const char*                     fingerprint_hash_func;
    brows_ssh_FingerprintSize       fingerprint_size;
    SOCKET                          socket;
    LIBSSH2_SESSION*                session;
};

static brows_ssh_Conn* brows_ssh_Conn_init(brows_ssh_Conn* p) {
    if (p) {
        p->fingerprint = NULL;
        p->fingerprint_size = 0;
        p->fingerprint_hash_func = NULL;
        p->host = 0;
        p->host_family = 0;
        p->port = 0;
        p->session = NULL;
        p->socket = INVALID_SOCKET;
        p->username = NULL;
    }
    return p;
}

static brows_ERROR brows_ssh_Conn_set_string(const char* source, char** dest) {
    if (!dest) {
        return brows_ERROR_NONE;
    }
    if (*dest) {
        free(*dest);
        *dest = NULL;
    }
    if (!source) {
        *dest = NULL;
        return brows_ERROR_NONE;
    }
    size_t count = strlen(source) + 1;
    char* d = calloc(count, sizeof(char));
    if (!d) {
        return brows_ssh_ERROR_CALLOC;
    }
    errno_t error = strcpy_s(d, count, source);
    if (error) {
        return brows_ssh_ERROR_STRCPY_S;
    }
    *dest = d;
    return brows_ERROR_NONE;
}

static brows_ERROR brows_ssh_Conn_set_fingerprint(brows_ssh_Conn* p, brows_ssh_Fingerprint v) {
    assert(p);
    if (p->fingerprint) {
        free(p->fingerprint);
        p->fingerprint = NULL;
    }
    if (!v) {
        return brows_ERROR_NONE;
    }
    int32_t size = p->fingerprint_size;
    char* fingerprint = calloc(size, sizeof(char));
    if (!fingerprint) {
        return brows_ssh_ERROR_CALLOC;
    }
    errno_t err = memcpy_s(fingerprint, size, v, size);
    if (err) {
        brows_LOG_ERROR("memcpy_s > %d", err);
        return brows_ERROR_SSH_MEMCPY_S;
    }
    p->fingerprint = fingerprint;
    return brows_ERROR_NONE;
}

static brows_ERROR brows_ssh_Conn_set_fingerprint_size(brows_ssh_Conn* p, brows_ssh_FingerprintSize v) {
    assert(p);
    p->fingerprint_size = v;
    return brows_ERROR_NONE;
}

static brows_ERROR brows_ssh_Conn_set_fingerprint_hash_func(brows_ssh_Conn* p, const char* v) {
    assert(p);
    p->fingerprint_hash_func = v;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_wait_socket(brows_ssh_Conn* p, brows_Canceler* cancel) {
    assert(p);

    libssh2_socket_t socket = p->socket;
    if (INVALID_SOCKET == socket) {
        return brows_ERROR_SSH_CONN_SOCKET_INVALID;
    }
    LIBSSH2_SESSION* session = p->session;
    if (!session) {
        return brows_ERROR_SSH_CONN_SESSION_IS_NULL;
    }
    fd_set fd = { 0 };
    fd_set* fd_read = NULL;
    fd_set* fd_write = NULL;

    struct timeval timeout = { 0 };
    timeout.tv_sec = 2;
    timeout.tv_usec = 500000;

    FD_ZERO(&fd);
    FD_SET(socket, &fd);

    int dir = libssh2_session_block_directions(session);
    if (dir & LIBSSH2_SESSION_BLOCK_INBOUND) {
        fd_read = &fd;
    }
    if (dir && LIBSSH2_SESSION_BLOCK_OUTBOUND) {
        fd_write = &fd;
    }
    for (;;) {
        int result = select((int)(socket + 1), fd_read, fd_write, NULL, &timeout);
        if (result == SOCKET_ERROR) {
            brows_LOG_ERROR("brows_ssh_Conn_wait_socket select error > %d", WSAGetLastError());
            return brows_ERROR_brows_ssh_Conn_wait_socket_error;
        }
        if (result != 0) {
            return brows_ERROR_NONE;
        }
        brows_LOG_DEBUG("%s", "brows_ssh_Conn_wait_socket timeout");
        if (brows_canceled(cancel)) {
            brows_LOG_INFO("%s", "brows_ssh_Conn_wait_socket canceled");
            return brows_ERROR_CANCELED;
        }
    }
}

brows_ssh_Conn* brows_ssh_Conn_create(void) {
    return brows_ssh_Conn_init(calloc(1, sizeof(brows_ssh_Conn)));
}

void brows_ssh_Conn_destroy(brows_ssh_Conn* p) {
    if (p) {
        if (p->session) {
            for (;;) {
                int session_free_err = libssh2_session_free(p->session);
                if (0 > session_free_err) {
                    if (LIBSSH2_ERROR_EAGAIN == session_free_err) {
                        brows_LOG_DEBUG("libssh2_session_free > %d", session_free_err);
                        continue;
                    }
                    brows_LOG_ERROR("libssh2_session_free > %d", session_free_err);
                }
                break;
            }
        }
        free(p->fingerprint);
        free(p->username);
        free(p);
    }
}

brows_ERROR brows_ssh_Conn_connect(brows_ssh_Conn* p, brows_Canceler* cancel) {
    assert(p);

    if (p->socket != INVALID_SOCKET) {
        return brows_ERROR_SSH_CONN_SOCKET_EXISTS;
    }
    if (p->session != NULL) {
        return brows_ERROR_SSH_CONN_SESSION_EXISTS;
    }
    brows_ssh_Port port = p->port;
    if (port < 0 || port > UINT16_MAX) {
        return brows_ssh_ERROR_PORT_OUT_OF_RANGE;
    }
    if (port == 0) {
        port = 22;
    }
    brows_ssh_HostFamily host_family = p->host_family;
    if (host_family < 0 || host_family > UINT16_MAX) {
        return brows_ssh_ERROR_HOST_FAMILY_OUT_OF_RANGE;
    }
    brows_ssh_Host host = p->host;
    if (host < 0 || host > UINT32_MAX) {
        return brows_ssh_ERROR_HOST_OUT_OF_RANGE;
    }
    WSADATA wsadata;
    int wsastartup_err = WSAStartup(MAKEWORD(2, 0), &wsadata);
    if (wsastartup_err) {
        brows_LOG_ERROR("WSAStartup > %d", wsastartup_err);
        return brows_ERROR_WSASTARTUP;
    }

    p->socket = socket(p->host_family, SOCK_STREAM, 0);
    if (INVALID_SOCKET == p->socket) {
        brows_LOG_ERROR("socket > %d", WSAGetLastError());
        brows_ssh_Conn_close(p, cancel);
        return brows_ERROR_SOCKET;
    }

    struct sockaddr_in sin = { 0 };
    sin.sin_family = host_family;
    sin.sin_port = htons(port);
    sin.sin_addr.s_addr = (ULONG)host;

    int connected = connect(p->socket, (struct sockaddr*)(&sin), sizeof(struct sockaddr_in));
    if (connected == SOCKET_ERROR) {
        brows_LOG_ERROR("connect > %d", WSAGetLastError());
        brows_ssh_Conn_close(p, cancel);
        return brows_ERROR_CONNECT;
    }

    p->session = libssh2_session_init();
    if (!p->session) {
        brows_LOG_ERROR("libssh2_session_init > %s", "NULL");
        brows_ssh_Conn_close(p, cancel);
        return brows_ERROR_LIBSSH2_SESSION_INIT;
    }
    libssh2_session_set_blocking(p->session, 0);

    brows_ssh_Conn_WAIT(p, int, < 0, 1, libssh2_session_handshake, p->session, p->socket);

    int32_t fingerprint_size = 32;
    const char* fingerprint_hash_func = "SHA256";
    const char* fingerprint = libssh2_hostkey_hash(p->session, LIBSSH2_HOSTKEY_HASH_SHA256);
    if (!fingerprint) {
        fingerprint_size = 20;
        fingerprint_hash_func = "SHA1";
        fingerprint = libssh2_hostkey_hash(p->session, LIBSSH2_HOSTKEY_HASH_SHA1);
    }
    if (!fingerprint) {
        fingerprint_size = 16;
        fingerprint_hash_func = "MD5";
        fingerprint = libssh2_hostkey_hash(p->session, LIBSSH2_HOSTKEY_HASH_MD5);
    }
    if (!fingerprint) {
        brows_LOG_ERROR("libssh2_hostkey_hash > %s", "NULL");
        brows_ssh_Conn_close(p, cancel);
        return brows_ERROR_LIBSSH2_HOSTKEY_HASH;
    }
    return
        brows_ssh_Conn_set_fingerprint_hash_func(p, fingerprint_hash_func) ||
        brows_ssh_Conn_set_fingerprint_size(p, fingerprint_size) ||
        brows_ssh_Conn_set_fingerprint(p, fingerprint);
}

brows_ERROR brows_ssh_Conn_auth_by_key_file(brows_ssh_Conn* p, const char* public_key_file, const char* private_key_file, const char* passphrase, brows_Canceler* cancel) {
    assert(p);
    brows_ssh_Conn_WAIT(p, int, < 0, 0, libssh2_userauth_publickey_fromfile, p->session, p->username, public_key_file, private_key_file, passphrase);
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_auth_by_password(brows_ssh_Conn* p, const char* password, brows_Canceler* cancel) {
    assert(p);
    brows_ssh_Conn_WAIT(p, int, < 0, 0, libssh2_userauth_password, p->session, p->username, password);
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_auth_success(brows_ssh_Conn* p) {
    assert(p);
    int authenticated = libssh2_userauth_authenticated(p->session);
    if (authenticated) {
        return brows_ERROR_NONE;
    }
    return brows_ERROR_SSH_NOT_AUTHENTICATED;
}

brows_ERROR brows_ssh_Conn_close(brows_ssh_Conn* p, brows_Canceler* cancel) {
    if (p) {
        if (p->session) {
            for (;;) {
                int session_disconnect_err = libssh2_session_disconnect(p->session, "brows_ssh_Conn_close");
                if (0 > session_disconnect_err) {
                    if (LIBSSH2_ERROR_EAGAIN == session_disconnect_err) {
                        brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p, cancel);
                        if (wait_socket_err) {
                            brows_LOG_ERROR("brows_ssh_Conn_wait_socket > %d", wait_socket_err);
                            break;
                        }
                        brows_LOG_DEBUG("libssh2_session_disconnect > %d", session_disconnect_err);
                        continue;
                    }
                    brows_LOG_ERROR("libssh2_session_disconnect > %d", session_disconnect_err);
                }
                break;
            }
            for (;;) {
                int session_free_err = libssh2_session_free(p->session);
                if (0 > session_free_err) {
                    if (LIBSSH2_ERROR_EAGAIN == session_free_err) {
                        brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p, cancel);
                        if (wait_socket_err) {
                            brows_LOG_ERROR("brows_ssh_Conn_wait_socket > %d", wait_socket_err);
                            break;
                        }
                        brows_LOG_DEBUG("libssh2_session_free > %d", session_free_err);
                        continue;
                    }
                    brows_LOG_ERROR("libssh2_session_free > %d", session_free_err);
                }
                break;
            }
        }
        if (p->socket != INVALID_SOCKET) {
            int closesocket_err = closesocket(p->socket);
            if (closesocket_err == SOCKET_ERROR) {
                brows_LOG_ERROR("closesocket > %d", WSAGetLastError());
            }
        }
        int cleanup_err = WSACleanup();
        if (cleanup_err == SOCKET_ERROR) {
            brows_LOG_ERROR("WSACleanup > %d", WSAGetLastError());
        }
        p->socket = INVALID_SOCKET;
        p->session = NULL;
    }
    return brows_ERROR_NONE;
}

LIBSSH2_SESSION* brows_ssh_Conn_get_session(brows_ssh_Conn* p) {
    assert(p);
    return p->session;
}

brows_ssh_Fingerprint brows_ssh_Conn_get_fingerprint(brows_ssh_Conn* p) {
    assert(p);
    return p->fingerprint;
}

brows_ssh_FingerprintSize brows_ssh_Conn_get_fingerprint_size(brows_ssh_Conn* p) {
    assert(p);
    return p->fingerprint_size;
}

const char* brows_ssh_Conn_get_fingerprint_hash_func(brows_ssh_Conn* p) {
    assert(p);
    return p->fingerprint_hash_func;
}

brows_ssh_Host brows_ssh_Conn_get_host(brows_ssh_Conn* p) {
    assert(p);
    return p->host;
}

brows_ssh_HostFamily brows_ssh_Conn_get_host_family(brows_ssh_Conn* p) {
    assert(p);
    return p->host_family;
}

brows_ssh_Port brows_ssh_Conn_get_port(brows_ssh_Conn* p) {
    assert(p);
    return p->port;
}

brows_ssh_Username brows_ssh_Conn_get_username(brows_ssh_Conn* p) {
    assert(p);
    return p->username;
}

brows_ERROR brows_ssh_Conn_set_host(brows_ssh_Conn* p, brows_ssh_Host v) {
    assert(p);
    p->host = v;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_set_host_family(brows_ssh_Conn* p, brows_ssh_HostFamily v) {
    assert(p);
    p->host_family = v;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_set_port(brows_ssh_Conn* p, brows_ssh_Port v) {
    assert(p);
    p->port = v;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_set_username(brows_ssh_Conn* p, brows_ssh_Username v) {
    assert(p);
    return brows_ssh_Conn_set_string(v, &p->username);
}
