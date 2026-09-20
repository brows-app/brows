#ifndef BROWS_SSH_INTERNAL_H
#define BROWS_SSH_INTERNAL_H

#include "brows_ssh.h"
#include "libssh2.h"
#include <assert.h>
#include <malloc.h>
#include <stdint.h>
#include <string.h>
#include <WinSock2.h>

#define brows_ssh_internal brows_EXTERN_C

#define brows_ssh_Conn_WAIT(P, T, HAS_FAILED, CLOSE_ON_ERROR, FUNCTION, ...)                                    \
do {                                                                                                            \
    T brows_ssh_Conn_WAIT_local;                                                                                \
    brows_ssh_Conn_WAIT_ON(P, brows_ssh_Conn_WAIT_local, HAS_FAILED, CLOSE_ON_ERROR, FUNCTION, __VA_ARGS__);    \
} while (0)

#define brows_ssh_Conn_WAIT_ON(P, RESULT, HAS_FAILED, CLOSE_ON_ERROR, FUNCTION, ...)                            \
do {                                                                                                            \
    for (;;) {                                                                                                  \
        RESULT = FUNCTION(__VA_ARGS__);                                                                         \
        if (RESULT HAS_FAILED) {                                                                                \
            if (LIBSSH2_ERROR_EAGAIN == RESULT) {                                                               \
                brows_LOG_DEBUG(#FUNCTION " > %d", RESULT);                                                     \
                brows_ERROR wait_socket_err = brows_ssh_Conn_wait_socket(P, cancel);                            \
                if (wait_socket_err) {                                                                          \
                    if (CLOSE_ON_ERROR) {                                                                       \
                        brows_ssh_Conn_close(P, cancel);                                                        \
                    }                                                                                           \
                    return wait_socket_err;                                                                     \
                }                                                                                               \
                continue;                                                                                       \
            }                                                                                                   \
            brows_LOG_ERROR(#FUNCTION " > %d", RESULT);                                                         \
            if (CLOSE_ON_ERROR) {                                                                               \
                brows_ssh_Conn_close(P, cancel);                                                                \
            }                                                                                                   \
            switch (RESULT) {                                                                                   \
                case LIBSSH2_ERROR_AUTHENTICATION_FAILED: return brows_ERROR_SSH_AUTHENTICATION_FAILED;         \
                case LIBSSH2_ERROR_PASSWORD_EXPIRED:      return brows_ERROR_SSH_PASSWORD_EXPIRED;              \
                case LIBSSH2_ERROR_PUBLICKEY_UNVERIFIED:  return brows_ERROR_SSH_PUBLIC_KEY_UNVERIFIED;         \
            }                                                                                                   \
            return brows_ERROR_##FUNCTION;                                                                      \
        }                                                                                                       \
        break;                                                                                                  \
    }                                                                                                           \
} while (0)

typedef             brows_ERROR(brows_ssh_ChannelFactory)(brows_ssh_Chan*, LIBSSH2_CHANNEL**, brows_Canceler*);

struct              brows_ssh_Chan {
                    brows_ssh_Conn*             conn;
                    brows_ssh_ChannelFactory*   channel_factory;
                    LIBSSH2_CHANNEL*            channel; };

brows_ssh_internal  brows_ERROR                 brows_ssh_Chan_free         (brows_ssh_Chan*);
brows_ssh_internal  brows_ssh_Chan*             brows_ssh_Chan_init         (brows_ssh_Chan*, brows_ssh_Conn*, brows_ssh_ChannelFactory* channel_factory);
brows_ssh_internal  brows_ERROR                 brows_ssh_Chan_get_channel  (brows_ssh_Chan*, brows_Canceler*, LIBSSH2_CHANNEL**);

brows_ssh_internal  LIBSSH2_SESSION*            brows_ssh_Conn_get_session  (brows_ssh_Conn*);
brows_ssh_internal  brows_ERROR                 brows_ssh_Conn_wait_socket  (brows_ssh_Conn*, brows_Canceler*);

brows_ssh_internal  brows_ERROR                 brows_ssh_KnownHost_init    (brows_ssh_KnownHost*, struct libssh2_knownhost*, int check);

#endif
