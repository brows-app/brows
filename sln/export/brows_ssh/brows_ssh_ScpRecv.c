#include "brows_ssh_internal.h"

struct brows_ssh_ScpRecv {
    brows_ssh_Chan      base;
    libssh2_struct_stat info;
    int64_t             read;
    const char*         path;
};

static brows_ERROR brows_ssh_ScpRecv_channel_factory(brows_ssh_Chan* p, LIBSSH2_CHANNEL** result, brows_Canceler* cancel) {
    assert(p);
    assert(result);

    LIBSSH2_CHANNEL* channel = *result = NULL;
    LIBSSH2_SESSION* session = brows_ssh_Conn_get_session(p->conn);
    if (!session) {
        brows_LOG_ERROR("brows_ssh_Conn_get_session > %s", "NULL");
        return brows_ERROR_SSH_CONN_GET_SESSION;
    }
    brows_ssh_ScpRecv* _p = (brows_ssh_ScpRecv*)p;
    const char* path = _p->path;
    libssh2_struct_stat* info = &_p->info;
    for (; ; ) {
        channel = libssh2_scp_recv2(session, path, info);
        if (channel) {
            *result = channel;
            return brows_ERROR_NONE;
        }
        int err_num = libssh2_session_last_errno(session);
        if (err_num == LIBSSH2_ERROR_EAGAIN) {
            brows_LOG_DEBUG("libssh2_scp_recv2 > %d", err_num);
            brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p->conn, cancel);
            if (wait_socket_err) {
                return wait_socket_err;
            }
            continue;
        }
        brows_LOG_ERROR("libssh2_scp_recv2 > %d", err_num);

        char* err_msg = NULL;
        libssh2_session_last_error(session, &err_msg, NULL, 0);
        if (err_msg) {
            brows_LOG_ERROR("libssh2_scp_recv2 > %s", err_msg);
        }
        return brows_ssh_ERROR_libssh2_scp_recv2;
    }
}

brows_ssh_ScpRecv* brows_ssh_ScpRecv_create(brows_ssh_Conn* conn, const char* path) {
    brows_ssh_ScpRecv* p = calloc(1, sizeof(brows_ssh_ScpRecv));
    if (p) {
        p->read = 0;
        p->path = path;
        brows_ssh_Chan_init(&p->base, conn, brows_ssh_ScpRecv_channel_factory);
    }
    return p;
}

void brows_ssh_ScpRecv_destroy(brows_ssh_ScpRecv* p) {
    if (p) {
        brows_ssh_Chan_free(&p->base);
        free(p);
    }
}

int64_t brows_ssh_ScpRecv_get_length(brows_ssh_ScpRecv* p) {
    assert(p);
    return p->info.st_size;
}

int64_t brows_ssh_ScpRecv_get_position(brows_ssh_ScpRecv* p) {
    assert(p);
    return p->read;
}

brows_ERROR brows_ssh_ScpRecv_read(brows_ssh_ScpRecv* p, char* buf, size_t buf_len, size_t* result, brows_Canceler* cancel) {
    assert(p);
    assert(result);
    LIBSSH2_CHANNEL* channel = NULL;
    brows_ERROR get_channel_err = brows_ssh_Chan_get_channel(&p->base, cancel, &channel);
    if (get_channel_err) {
        return get_channel_err;
    }
    int64_t remaining = p->info.st_size - p->read;
    if (0 >= remaining) {
        *result = 0;
        return brows_ERROR_NONE;
    }
    if (buf_len > (size_t)remaining) {
        buf_len = (size_t)remaining;
    }
    for (;;) {
        ssize_t read = libssh2_channel_read(channel, buf, buf_len);
        if (read < 0) {
            if (read == LIBSSH2_ERROR_EAGAIN) {
                brows_LOG_DEBUG("libssh2_channel_read > %s", "LIBSSH2_ERROR_EAGAIN");
                brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(p->base.conn, cancel);
                if (wait_socket_err) {
                    return wait_socket_err;
                } 
                continue;
            }
            brows_LOG_ERROR("libssh2_channel_read > %d", read);
            return brows_ERROR_libssh2_channel_read;
        }
        brows_LOG_DEBUG("libssh2_channel_read > %d", read);
        p->read = read + p->read;
        *result = read;
        return brows_ERROR_NONE;
    }
}
