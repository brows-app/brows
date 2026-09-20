#include "brows_ssh_internal.h"

struct brows_ssh_ScpSend {
    brows_ssh_Chan      base;
    int                 mode;
    int64_t             size;
    int64_t             writ;
    const char*         path;
};

static brows_ERROR brows_ssh_ScpSend_channel_factory(brows_ssh_Chan* p, LIBSSH2_CHANNEL** result, brows_Canceler* cancel) {
    assert(p);
    assert(result);
    LIBSSH2_CHANNEL* channel = *result = NULL;
    LIBSSH2_SESSION* session = brows_ssh_Conn_get_session(p->conn);
    if (!session) {
        brows_LOG_ERROR("brows_ssh_Conn_get_session > %s", "NULL");
        return brows_ERROR_SSH_CONN_GET_SESSION;
    }
    brows_ssh_ScpSend* _p = (brows_ssh_ScpSend*)p;
    for (; ; ) {
        channel = libssh2_scp_send64(session, _p->path, _p->mode, _p->size, 0, 0);
        if (channel) {
            *result = channel;
            return brows_ERROR_NONE;
        }
        int err_num = libssh2_session_last_errno(session);
        if (err_num == LIBSSH2_ERROR_EAGAIN) {
            brows_LOG_DEBUG("libssh2_scp_send > %s", "LIBSSH2_ERROR_EAGAIN");
            brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p->conn, cancel);
            if (wait_socket_err) {
                return wait_socket_err;
            }
            continue;
        }
        brows_LOG_ERROR("libssh2_scp_send > %" PRId32, err_num);

        char* err_msg = NULL;
        libssh2_session_last_error(session, &err_msg, NULL, 0);
        if (err_msg) {
            brows_LOG_ERROR("libssh2_scp_send > %s", err_msg);
        }
        return brows_ERROR_libssh2_scp_send;
    }
}

brows_ssh_ScpSend* brows_ssh_ScpSend_create(brows_ssh_Conn* conn, const char* path, int mode, int64_t size) {
    brows_ssh_ScpSend* p = calloc(1, sizeof(brows_ssh_ScpSend));
    if (p) {
        p->mode = mode;
        p->path = path;
        p->size = size;
        p->writ = 0;
        brows_ssh_Chan_init(&p->base, conn, brows_ssh_ScpSend_channel_factory);
    }
    return p;
}

void brows_ssh_ScpSend_destroy(brows_ssh_ScpSend* p) {
    if (p) {
        brows_ssh_Chan_free(&p->base);
        free(p);
    }
}

brows_ERROR brows_ssh_ScpSend_write(brows_ssh_ScpSend* p, const char* buf, size_t buf_len, size_t* result, brows_Canceler* cancel) {
    assert(p);
    assert(result);
    LIBSSH2_CHANNEL* channel = NULL;
    brows_ERROR get_channel_err = brows_ssh_Chan_get_channel(&p->base, cancel, &channel);
    if (get_channel_err) {
        return get_channel_err;
    }
    for (;;) {
        ssize_t written = libssh2_channel_write(channel, buf, buf_len);
        if (0 > written) {
            if (LIBSSH2_ERROR_EAGAIN == written) {
                brows_LOG_DEBUG("libssh2_channel_write > %s", "LIBSSH2_ERROR_EAGAIN");
                brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p->base.conn, cancel);
                if (wait_socket_err) {
                    return wait_socket_err;
                }
                continue;
            }
            brows_LOG_ERROR("libssh2_channel_write > %" PRIdPTR, written);
            return brows_ERROR_libssh2_channel_write;
        }
        brows_LOG_DEBUG("libssh2_channel_write > %" PRIdPTR, written);
        p->writ = written + p->writ;
        *result = written;
        return brows_ERROR_NONE;
    }
}
