#include "brows_ssh_internal.h"

brows_ERROR brows_ssh_Chan_get_channel(brows_ssh_Chan* p, brows_Canceler* cancel, LIBSSH2_CHANNEL** channel) {
    assert(p);
    assert(p->channel_factory);
    if (p->channel == NULL) {
        brows_ERROR channel_factory_err = p->channel_factory(p, &p->channel, cancel);
        if (channel_factory_err) {
            p->channel = NULL;
            return channel_factory_err;
        }
    }
    if (p->channel == NULL) {
        brows_LOG_ERROR("brows_ssh_Chan_get_channel > %s", "NULL");
        return brows_ERROR_SSH_CHAN_GET_CHANNEL_IS_NULL;
    }
    if (channel) {
        *channel = p->channel;
    }
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Chan_ready(brows_ssh_Chan* p, brows_Canceler* cancel) {
    return brows_ssh_Chan_get_channel(p, cancel, NULL);
}

brows_ssh_Chan* brows_ssh_Chan_init(brows_ssh_Chan* p, brows_ssh_Conn* conn, brows_ssh_ChannelFactory* channel_factory) {
    if (p) {
        p->conn = conn;
        p->channel = NULL;
        p->channel_factory = channel_factory;
    }
    return p;
}

brows_ERROR brows_ssh_Chan_close(brows_ssh_Chan* p, brows_Canceler* cancel) {
    if (p) {
        if (p->channel) {
            brows_ssh_Conn_WAIT(p->conn, int, < 0, 0, libssh2_channel_close, p->channel);
        }
    }
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Chan_free(brows_ssh_Chan* p) {
    if (p) {
        if (p->channel) {
            for (;;) {
                int channel_free_err = libssh2_channel_free(p->channel);
                if (0 > channel_free_err) {
                    if (LIBSSH2_ERROR_EAGAIN == channel_free_err) {
                        brows_LOG_DEBUG("libssh2_channel_free > %d", channel_free_err);
                        continue;
                    }
                    brows_LOG_ERROR("libssh2_channel_free > %d", channel_free_err);
                    return brows_ERROR_LIBSSH2_CHANNEL_FREE;
                }
                break;
            }
        }
    }
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Chan_exec(brows_ssh_Chan* p, const char* command, brows_Canceler* cancel) {
    assert(p);
    LIBSSH2_CHANNEL* channel;
    brows_ERROR get_channel_err = brows_ssh_Chan_get_channel(p, cancel, &channel);
    if (get_channel_err) {
        return get_channel_err;
    }
    brows_ssh_Conn_WAIT(p->conn, int, < 0, 0, libssh2_channel_exec, channel, command);
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Chan_eof(brows_ssh_Chan* p, int32_t* result, brows_Canceler* cancel) {
    assert(p);
    assert(result);
    LIBSSH2_CHANNEL* channel;
    brows_ERROR get_channel_err = brows_ssh_Chan_get_channel(p, cancel, &channel);
    if (get_channel_err) {
        return get_channel_err;
    }
    int eof = libssh2_channel_eof(channel);
    if (0 > eof) {
        brows_LOG_ERROR("libssh2_channel_eof > %d", eof);
        return brows_ERROR_libssh2_channel_eof;
    }
    *result = eof;
    return brows_ERROR_NONE;
}

brows_ERROR brows_ssh_Chan_flush(brows_ssh_Chan* p, int stream_id, brows_Canceler* cancel) {
    assert(p);
    LIBSSH2_CHANNEL* channel;
    brows_ERROR get_channel_err = brows_ssh_Chan_get_channel(p, cancel, &channel);
    if (get_channel_err) {
        return get_channel_err;
    }
    brows_ssh_Conn_WAIT(p->conn, int, < 0, 0, libssh2_channel_flush_ex, channel, stream_id);
    return brows_ERROR_NONE;
}
