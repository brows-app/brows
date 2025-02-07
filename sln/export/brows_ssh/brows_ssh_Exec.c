#include "brows_ssh_internal.h"

struct brows_ssh_Exec {
    brows_ssh_Chan base;
};

static brows_ERROR brows_ssh_Exec_channel_factory(brows_ssh_Chan* p, LIBSSH2_CHANNEL** result, brows_Canceler* cancel) {
    assert(p);
    assert(result);

    LIBSSH2_CHANNEL* channel = *result = NULL;
    LIBSSH2_SESSION* session = brows_ssh_Conn_get_session(p->conn);
    if (!session) {
        brows_LOG_ERROR("brows_ssh_Conn_get_session > %s", "NULL");
        return brows_ERROR_SSH_CONN_GET_SESSION;
    }
    for (; ; ) {
        channel = libssh2_channel_open_session(session);
        if (channel) {
            *result = channel;
            return brows_ERROR_NONE;
        }
        int open_session_err = libssh2_session_last_errno(session);
        if (open_session_err == LIBSSH2_ERROR_EAGAIN) {
            brows_LOG_DEBUG("libssh2_channel_open_session > %" PRId32, open_session_err);
            brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p->conn, cancel);
            if (wait_socket_err) {
                return wait_socket_err;
            }
            continue;
        }
        brows_LOG_ERROR("libssh2_channel_open_session > %" PRId32, open_session_err);

        char* open_session_err_msg = NULL;
        libssh2_session_last_error(session, &open_session_err_msg, NULL, 0);
        if (open_session_err_msg) {
            brows_LOG_ERROR("libssh2_channel_open_session > %s", open_session_err_msg);
        }
        return brows_ERROR_libssh2_channel_open_session;
    }
}

brows_ssh_Exec* brows_ssh_Exec_create(brows_ssh_Conn* conn) {
    brows_ssh_Exec* p = calloc(1, sizeof(brows_ssh_Exec));
    if (p) {
        brows_ssh_Chan_init(&p->base, conn, brows_ssh_Exec_channel_factory);
    }
    return p;
}

brows_ERROR brows_ssh_Exec_read(brows_ssh_Exec* p, int stream_id, char* buf, size_t buf_len, size_t* result, brows_Canceler* cancel) {
    assert(p);
    assert(result);
    LIBSSH2_CHANNEL* channel;
    brows_ERROR get_channel_err = brows_ssh_Chan_get_channel(&p->base, cancel, &channel);
    if (get_channel_err) {
        return get_channel_err;
    }
    for (;;) {
        ssize_t read = libssh2_channel_read_ex(channel, stream_id, buf, buf_len);
        if (read < 0) {
            if (read == LIBSSH2_ERROR_EAGAIN) {
                brows_LOG_DEBUG("libssh2_channel_read_ex > %s", "LIBSSH2_ERROR_EAGAIN");
                brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p->base.conn, cancel);
                if (wait_socket_err) {
                    return wait_socket_err;
                }
                continue;
            }
            brows_LOG_ERROR("libssh2_channel_read_ex > %" PRIdPTR, read);
            return brows_ERROR_libssh2_channel_read_ex;
        }
        *result = (size_t)read;
        return brows_ERROR_NONE;
    }
}

void brows_ssh_Exec_destroy(brows_ssh_Exec* p) {
    if (p) {
        brows_ssh_Chan_free(&p->base);
        free(p);
    }
}
