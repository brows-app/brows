#include "brows_ssh_internal.h"

#pragma comment(lib, "Ws2_32.lib")

#define brows_ssh_Conn_WAIT_POLL_MS 100
#define brows_ssh_Conn_CLEANUP_TIMEOUT_MS 1000
/*
 * Terminal transport callbacks should make libssh2 consume cleanup state with
 * a non-EAGAIN result. Keep a finite cap in case that behavior changes.
 */
#define brows_ssh_Conn_ABORT_RETRY_COUNT 16

struct brows_ssh_Conn {
    int64_t                         host;
    int32_t                         host_family;
    uint8_t                         host_address[16];
    uint32_t                        host_address_length;
    uint32_t                        host_scope_id;
    int32_t                         port;
    char*                           username;
    char*                           fingerprint;
    const char*                     fingerprint_hash_func;
    brows_ssh_FingerprintSize       fingerprint_size;
    SOCKET                          socket;
    int32_t                         winsock_started;
    int32_t                         transport_aborted;
    volatile LONG                   abort_requested;
    LIBSSH2_SESSION*                session;
};

static brows_ssh_Conn* brows_ssh_Conn_init(brows_ssh_Conn* p) {
    if (p) {
        p->fingerprint = NULL;
        p->fingerprint_size = 0;
        p->fingerprint_hash_func = NULL;
        p->host = 0;
        p->host_family = 0;
        memset(p->host_address, 0, sizeof(p->host_address));
        p->host_address_length = 0;
        p->host_scope_id = 0;
        p->port = 0;
        p->session = NULL;
        p->socket = INVALID_SOCKET;
        p->winsock_started = 0;
        p->transport_aborted = 0;
        p->abort_requested = 0;
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
        brows_LOG_ERROR("memcpy_s > %" PRId32, err);
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

static brows_ERROR brows_ssh_Conn_wait_socket_internal(brows_ssh_Conn* p,
                                                       brows_Canceler* cancel,
                                                       ULONGLONG deadline) {
    assert(p);

    for (;;) {
        libssh2_socket_t socket = p->socket;
        if (INVALID_SOCKET == socket) {
            return brows_ERROR_SSH_CONN_SOCKET_INVALID;
        }
        LIBSSH2_SESSION* session = p->session;
        if (!session) {
            return brows_ERROR_SSH_CONN_SESSION_IS_NULL;
        }
        if (InterlockedCompareExchange(&p->abort_requested, 0, 0)) {
            brows_ssh_Conn_abort_transport(p);
            return brows_ERROR_SSH_CONN_WAIT_TIMEOUT;
        }
        if (brows_canceled(cancel)) {
            return brows_ERROR_CANCELED;
        }
        ULONGLONG now = GetTickCount64();
        if (deadline && now >= deadline) {
            return brows_ERROR_SSH_CONN_WAIT_TIMEOUT;
        }
        int dir = libssh2_session_block_directions(session);
        fd_set fd = { 0 };
        fd_set* fd_read = NULL;
        fd_set* fd_write = NULL;
        ULONGLONG wait_ms = brows_ssh_Conn_WAIT_POLL_MS;
        if (deadline) {
            ULONGLONG remaining = deadline - now;
            if (remaining < wait_ms) {
                wait_ms = remaining;
            }
        }
        struct timeval timeout = {
            (long)(wait_ms / 1000),
            (long)((wait_ms % 1000) * 1000)
        };

        FD_ZERO(&fd);
        FD_SET(socket, &fd);

        if (dir & LIBSSH2_SESSION_BLOCK_INBOUND) {
            fd_read = &fd;
        }
        if (dir & LIBSSH2_SESSION_BLOCK_OUTBOUND) {
            fd_write = &fd;
        }
        if (!dir) {
            fd_read = &fd;
            fd_write = &fd;
        }
        int result = select((int)(socket + 1), fd_read, fd_write, NULL, &timeout);
        if (result == SOCKET_ERROR) {
            brows_LOG_ERROR("brows_ssh_Conn_wait_socket select error > %" PRId32, WSAGetLastError());
            return brows_ERROR_brows_ssh_Conn_wait_socket_error;
        }
        if (result != 0) {
            return brows_ERROR_NONE;
        }
        if (brows_canceled(cancel)) {
            brows_LOG_INFO("%s", "brows_ssh_Conn_wait_socket canceled");
            return brows_ERROR_CANCELED;
        }
        if (deadline && GetTickCount64() >= deadline) {
            return brows_ERROR_SSH_CONN_WAIT_TIMEOUT;
        }
    }
}

brows_ERROR brows_ssh_Conn_wait_socket(brows_ssh_Conn* p, brows_Canceler* cancel) {
    return brows_ssh_Conn_wait_socket_internal(p, cancel, 0);
}

brows_ERROR brows_ssh_Conn_wait_socket_until(brows_ssh_Conn* p,
                                             brows_Canceler* cancel,
                                             ULONGLONG deadline) {
    return brows_ssh_Conn_wait_socket_internal(p, cancel, deadline);
}

static LIBSSH2_SEND_FUNC(brows_ssh_Conn_send_aborted) {
    (void)socket;
    (void)buffer;
    (void)length;
    (void)flags;
    (void)abstract;
    WSASetLastError(WSAECONNRESET);
    return -1;
}

static LIBSSH2_RECV_FUNC(brows_ssh_Conn_recv_aborted) {
    (void)socket;
    (void)buffer;
    (void)length;
    (void)flags;
    (void)abstract;
    WSASetLastError(WSAECONNRESET);
    return -1;
}

void brows_ssh_Conn_abort_transport(brows_ssh_Conn* p) {
    if (!p || p->transport_aborted) {
        return;
    }
    p->transport_aborted = 1;
    if (p->session) {
        libssh2_session_callback_set2(p->session,
                                      LIBSSH2_CALLBACK_SEND,
                                      (libssh2_cb_generic*)brows_ssh_Conn_send_aborted);
        libssh2_session_callback_set2(p->session,
                                      LIBSSH2_CALLBACK_RECV,
                                      (libssh2_cb_generic*)brows_ssh_Conn_recv_aborted);
    }
    if (p->socket != INVALID_SOCKET) {
        shutdown(p->socket, SD_BOTH);
        if (closesocket(p->socket) == SOCKET_ERROR) {
            brows_LOG_ERROR("closesocket during transport abort > %" PRId32, WSAGetLastError());
        }
        p->socket = INVALID_SOCKET;
    }
}

int32_t brows_ssh_Conn_is_aborted(brows_ssh_Conn* p) {
    assert(p);
    return p->transport_aborted;
}

void brows_ssh_Conn_request_abort(brows_ssh_Conn* p) {
    if (p) {
        InterlockedExchange(&p->abort_requested, 1);
    }
}

brows_ssh_Conn* brows_ssh_Conn_create(void) {
    return brows_ssh_Conn_init(calloc(1, sizeof(brows_ssh_Conn)));
}

void brows_ssh_Conn_destroy(brows_ssh_Conn* p) {
    if (p) {
        if (p->session || p->socket != INVALID_SOCKET || p->winsock_started) {
            brows_ssh_Conn_close(p, NULL);
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
    if (InterlockedCompareExchange(&p->abort_requested, 0, 0)) {
        brows_ssh_Conn_abort_transport(p);
        return brows_ERROR_SSH_CONN_WAIT_TIMEOUT;
    }
    p->transport_aborted = 0;
    if (p->port < 0 || p->port > UINT16_MAX) {
        return brows_ssh_ERROR_PORT_OUT_OF_RANGE;
    }
    USHORT port = htons((u_short)(p->port == 0 ? 22 : p->port));
    int32_t host_family = p->host_family;
    if (host_family != AF_INET && host_family != AF_INET6) {
        return brows_ssh_ERROR_HOST_FAMILY_OUT_OF_RANGE;
    }
    WSADATA wsadata;
    int wsastartup_err = WSAStartup(MAKEWORD(2, 0), &wsadata);
    if (wsastartup_err) {
        brows_LOG_ERROR("WSAStartup > %" PRId32, wsastartup_err);
        return brows_ERROR_WSASTARTUP;
    }
    p->winsock_started = 1;

    p->socket = socket(p->host_family, SOCK_STREAM, 0);
    if (INVALID_SOCKET == p->socket) {
        brows_LOG_ERROR("socket > %" PRId32, WSAGetLastError());
        brows_ssh_Conn_close(p, cancel);
        return brows_ERROR_SOCKET;
    }

    struct sockaddr_storage address = { 0 };
    int address_length = 0;
    if (host_family == AF_INET) {
        struct sockaddr_in* sin = (struct sockaddr_in*)&address;
        sin->sin_family = AF_INET;
        sin->sin_port = port;
        if (p->host_address_length == sizeof(sin->sin_addr)) {
            memcpy(&sin->sin_addr, p->host_address, sizeof(sin->sin_addr));
        }
        else if (p->host_address_length == 0) {
            sin->sin_addr.s_addr = (ULONG)p->host;
        }
        else {
            brows_ssh_Conn_close(p, cancel);
            return brows_ssh_ERROR_HOST_OUT_OF_RANGE;
        }
        address_length = sizeof(*sin);
    }
    else {
        struct sockaddr_in6* sin6 = (struct sockaddr_in6*)&address;
        sin6->sin6_family = AF_INET6;
        sin6->sin6_port = port;
        sin6->sin6_scope_id = p->host_scope_id;
        if (p->host_address_length != sizeof(sin6->sin6_addr)) {
            brows_ssh_Conn_close(p, cancel);
            return brows_ssh_ERROR_HOST_OUT_OF_RANGE;
        }
        memcpy(&sin6->sin6_addr, p->host_address, sizeof(sin6->sin6_addr));
        address_length = sizeof(*sin6);
    }

    u_long nonblocking = 1;
    if (ioctlsocket(p->socket, FIONBIO, &nonblocking) == SOCKET_ERROR) {
        brows_LOG_ERROR("ioctlsocket > %" PRId32, WSAGetLastError());
        brows_ssh_Conn_close(p, cancel);
        return brows_ERROR_CONNECT;
    }

    int connected = connect(p->socket, (struct sockaddr*)&address, address_length);
    if (connected == SOCKET_ERROR) {
        int connect_error = WSAGetLastError();
        if (connect_error != WSAEWOULDBLOCK && connect_error != WSAEINPROGRESS && connect_error != WSAEALREADY) {
            brows_LOG_ERROR("connect > %" PRId32, connect_error);
            brows_ssh_Conn_close(p, cancel);
            return brows_ERROR_CONNECT;
        }
        for (;;) {
            fd_set write_set = { 0 };
            fd_set error_set = { 0 };
            FD_ZERO(&write_set);
            FD_ZERO(&error_set);
            FD_SET(p->socket, &write_set);
            FD_SET(p->socket, &error_set);
            struct timeval timeout = { 0, 250000 };
            int select_result = select(0, NULL, &write_set, &error_set, &timeout);
            if (select_result == SOCKET_ERROR) {
                brows_LOG_ERROR("connect select > %" PRId32, WSAGetLastError());
                brows_ssh_Conn_close(p, cancel);
                return brows_ERROR_CONNECT;
            }
            if (select_result > 0) {
                int socket_error = 0;
                int socket_error_length = sizeof(socket_error);
                if (getsockopt(p->socket,
                               SOL_SOCKET,
                               SO_ERROR,
                               (char*)&socket_error,
                               &socket_error_length) == SOCKET_ERROR || socket_error != 0) {
                    brows_LOG_ERROR("connect completion > %" PRId32, socket_error ? socket_error : WSAGetLastError());
                    brows_ssh_Conn_close(p, cancel);
                    return brows_ERROR_CONNECT;
                }
                break;
            }
            if (brows_canceled(cancel)) {
                brows_ssh_Conn_close(p, cancel);
                return brows_ERROR_CANCELED;
            }
        }
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
            ULONGLONG disconnect_deadline = GetTickCount64() + brows_ssh_Conn_CLEANUP_TIMEOUT_MS;
            if (!p->transport_aborted) {
                for (;;) {
                    int session_disconnect_err = libssh2_session_disconnect(p->session, "brows_ssh_Conn_close");
                    if (session_disconnect_err == LIBSSH2_ERROR_EAGAIN) {
                        brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket_until(p, cancel, disconnect_deadline);
                        if (wait_socket_err) {
                            brows_LOG_ERROR("brows_ssh_Conn_wait_socket during disconnect > %" PRId32, wait_socket_err);
                            brows_ssh_Conn_abort_transport(p);
                            break;
                        }
                        continue;
                    }
                    if (session_disconnect_err < 0) {
                        brows_LOG_ERROR("libssh2_session_disconnect > %" PRId32, session_disconnect_err);
                    }
                    break;
                }
            }
            ULONGLONG free_deadline = GetTickCount64() + brows_ssh_Conn_CLEANUP_TIMEOUT_MS;
            int abort_retry_count = 0;
            for (;;) {
                int session_free_err = libssh2_session_free(p->session);
                if (session_free_err != LIBSSH2_ERROR_EAGAIN) {
                    p->session = NULL;
                    if (session_free_err < 0) {
                        brows_LOG_ERROR("libssh2_session_free > %" PRId32, session_free_err);
                    }
                    break;
                }
                if (p->transport_aborted) {
                    if (++abort_retry_count >= brows_ssh_Conn_ABORT_RETRY_COUNT) {
                        brows_LOG_ERROR("%s", "libssh2_session_free remained pending after transport abort");
                        break;
                    }
                    continue;
                }
                brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket_until(p, cancel, free_deadline);
                if (wait_socket_err) {
                    brows_LOG_ERROR("brows_ssh_Conn_wait_socket during session free > %" PRId32, wait_socket_err);
                    brows_ssh_Conn_abort_transport(p);
                    if (++abort_retry_count >= brows_ssh_Conn_ABORT_RETRY_COUNT) {
                        break;
                    }
                    continue;
                }
            }
        }
        if (p->socket != INVALID_SOCKET) {
            int closesocket_err = closesocket(p->socket);
            if (closesocket_err == SOCKET_ERROR) {
                brows_LOG_ERROR("closesocket > %" PRId32, WSAGetLastError());
            }
            p->socket = INVALID_SOCKET;
        }
        if (p->winsock_started) {
            int cleanup_err = WSACleanup();
            if (cleanup_err == SOCKET_ERROR) {
                brows_LOG_ERROR("WSACleanup > %" PRId32, WSAGetLastError());
            }
            p->winsock_started = 0;
        }
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

int64_t brows_ssh_Conn_get_host(brows_ssh_Conn* p) {
    assert(p);
    return p->host;
}

int32_t brows_ssh_Conn_get_host_family(brows_ssh_Conn* p) {
    assert(p);
    return p->host_family;
}

int32_t brows_ssh_Conn_get_port(brows_ssh_Conn* p) {
    assert(p);
    return p->port;
}

brows_ssh_Username brows_ssh_Conn_get_username(brows_ssh_Conn* p) {
    assert(p);
    return p->username;
}

brows_ERROR brows_ssh_Conn_set_host(brows_ssh_Conn* p, int64_t v) {
    assert(p);
    p->host = v;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_set_host_family(brows_ssh_Conn* p, int32_t v) {
    assert(p);
    p->host_family = v;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_set_host_address(brows_ssh_Conn* p,
                                            const uint8_t* address,
                                            uint32_t address_length,
                                            int32_t family,
                                            uint32_t scope_id) {
    assert(p);
    if (!address) {
        return brows_ssh_ERROR_HOST_OUT_OF_RANGE;
    }
    if ((family == AF_INET && address_length != 4) ||
        (family == AF_INET6 && address_length != 16) ||
        (family != AF_INET && family != AF_INET6)) {
        return brows_ssh_ERROR_HOST_OUT_OF_RANGE;
    }
    memset(p->host_address, 0, sizeof(p->host_address));
    memcpy(p->host_address, address, address_length);
    p->host_address_length = address_length;
    p->host_scope_id = family == AF_INET6 ? scope_id : 0;
    p->host_family = family;
    p->host = 0;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_set_port(brows_ssh_Conn* p, int32_t v) {
    assert(p);
    p->port = v;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Conn_set_username(brows_ssh_Conn* p, brows_ssh_Username v) {
    assert(p);
    return brows_ssh_Conn_set_string(v, &p->username);
}
