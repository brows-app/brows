#ifndef BROWS_SSH_H
#define BROWS_SSH_H

#include "brows_native.h"
#include <basetsd.h>

#ifdef      brows_ssh_IMPORT
#define     brows_ssh                                   brows_DLLIMPORT
#endif

#ifdef      brows_ssh_EXPORT
#define     brows_ssh                                   brows_DLLEXPORT
#endif

#ifndef     brows_ssh
#define     brows_ssh                                   brows_EXTERN_C
#endif

typedef     struct brows_ssh_Chan                       brows_ssh_Chan;
typedef     struct brows_ssh_Conn                       brows_ssh_Conn;
typedef     struct brows_ssh_KnownHost                  brows_ssh_KnownHost;
typedef     enum   brows_ssh_KnownHost_Status           brows_ssh_KnownHost_Status;
typedef     const  char*                                brows_ssh_KnownHostName;
typedef     int32_t                                     brows_ssh_KnownHostPort;
typedef     struct brows_ssh_KnownHosts                 brows_ssh_KnownHosts;
typedef     struct brows_ssh_Exec                       brows_ssh_Exec;
typedef     struct brows_ssh_ScpRecv                    brows_ssh_ScpRecv;
typedef     struct brows_ssh_ScpSend                    brows_ssh_ScpSend;
typedef     struct brows_ssh_Shell                      brows_ssh_Shell;
typedef     int64_t                                     brows_ssh_Host;
typedef     int32_t                                     brows_ssh_HostFamily;
typedef     int32_t                                     brows_ssh_Port;
typedef     const  char *                               brows_ssh_Username;
typedef     const  char *                               brows_ssh_Fingerprint;
typedef     int32_t                                     brows_ssh_FingerprintSize;
typedef     long                                        brows_ssh_SelectTimeoutSec;
typedef     long                                        brows_ssh_SelectTimeoutMicroSec;

#define     brows_ssh_ERROR_LIBSSH2_INIT                2111000
#define     brows_ssh_ERROR_SOCKET                      2111001
#define     brows_ssh_ERROR_CONNECT                     2111002
#define     brows_ssh_ERROR_WSASTARTUP                  2111003
#define     brows_ssh_ERROR_CLOSESOCKET                 2111004
#define     brows_ssh_ERROR_WSACLEANUP                  2111005
#define     brows_ssh_ERROR_CALLOC                      2111006
#define     brows_ssh_ERROR_LIBSSH2_SESSION_HANDSHAKE   2111008
#define     brows_ssh_ERROR_LIBSSH2_HOSTKEY_HASH        2111009
#define     brows_ssh_ERROR_PORT_OUT_OF_RANGE           2111010
#define     brows_ssh_ERROR_HOST_OUT_OF_RANGE           2111011
#define     brows_ssh_ERROR_HOST_FAMILY_OUT_OF_RANGE    2111012
#define     brows_ssh_ERROR_LIBSSH2_SESSION_INIT        2111013
#define     brows_ssh_ERROR_FINGERPRINT_OK              2111014
#define     brows_ssh_ERROR_LIBSSH2_USERAUTH_PUBLICKEY_FROMFILE 2111015
#define     brows_ssh_ERROR_LIBSSH2_USERAUTH_PASSWORD   2111016
#define     brows_ssh_ERROR_STRCPY_S                    2111017
#define     brows_ssh_ERROR_LIBSSH2_CHANNEL_CLOSE       2111018
#define     brows_ssh_ERROR_LIBSSH2_CHANNEL_OPEN_SESSION 2111019
#define     brows_ssh_ERROR_LIBSSH2_CHANNEL_REQUEST_PTY 2111020

#define     brows_ERROR_brows_libssh2_channel_request_pty 2111021
#define     brows_ERROR_brows_libssh2_channel_shell     2111022
#define     brows_ERROR_brows_libssh2_session_disconnect 2122023
#define     brows_ERROR_brows_libssh2_session_free      2111024
#define     brows_ERROR_WSAStartup                      2111025
#define     brows_ERROR_brows_libssh2_session_handshake 2111026
#define     brows_ERROR_brows_libssh2_userauth_publickey_fromfile 2111027
#define     brows_ERROR_brows_libssh2_userauth_password 2111028
#define     brows_ERROR_brows_ssh_Chan_get_session      2111029
#define     brows_ERROR_brows_libssh2_channel_close     2111030
#define     brows_ERROR_brows_libssh2_channel_exec      2111031
#define     brows_ERROR_brows_libssh2_channel_flush     2111032
#define     brows_ERROR_brows_libssh2_channel_eof       2111033
#define     brows_ERROR_brows_ssh_Conn_wait_socket_error   2111035
#define     brows_ERROR_libssh2_channel_close           2111036
#define     brows_ERROR_LIBSSH2_CHANNEL_FREE            2111037
#define     brows_ERROR_SSH_CHAN_GET_CHANNEL_IS_NULL    2111038
#define     brows_ERROR_libssh2_channel_exec            2111039
#define     brows_ERROR_libssh2_channel_eof             2111040
#define     brows_ERROR_libssh2_channel_read_ex         2111041
#define     brows_ERROR_libssh2_channel_write_ex        2111042
#define     brows_ERROR_libssh2_channel_flush_ex        2111043
#define     brows_ERROR_SSH_CONN_GET_SESSION            2111044
#define     brows_ERROR_libssh2_channel_open_session    2111045
#define     brows_ERROR_WSASTARTUP                      2111046
#define     brows_ERROR_SOCKET                          2111047
#define     brows_ERROR_CONNECT                         2111048
#define     brows_ERROR_LIBSSH2_SESSION_INIT            2111049
#define     brows_ERROR_libssh2_session_handshake       2111050
#define     brows_ERROR_LIBSSH2_HOSTKEY_HASH            2111051
#define     brows_ERROR_libssh2_userauth_publickey_fromfile 2111052
#define     brows_ERROR_SSH_CONN_SOCKET_INVALID         2111053
#define     brows_ERROR_SSH_CONN_SESSION_IS_NULL        2111054
#define     brows_ERROR_libssh2_userauth_password       2111055
#define     brows_ERROR_SSH_CONN_SOCKET_EXISTS          2111056
#define     brows_ERROR_SSH_CONN_SESSION_EXISTS         2111057
#define     brows_ERROR_SSH_PUBLIC_KEY_UNVERIFIED       2111058
#define     brows_ERROR_SSH_AUTHENTICATION_FAILED       2111059
#define     brows_ERROR_SSH_PASSWORD_EXPIRED            2111060
#define     brows_ERROR_SSH_NOT_AUTHENTICATED           2111061
#define     brows_ERROR_SSH_CONN_AUTH                   2111062
#define     brows_ERROR_SSH_MEMCPY_S                    2111063
#define     brows_ERROR_SSH_libssh2_knownhost_init      2111064
#define     brows_ssh_ERROR_LIBSSH2_SESSION_HOSTKEY     2111065
#define     brows_ssh_ERROR_LIBSSH2_KNOWNHOST_CHECKP    2111066
#define     brows_ssh_ERROR_LIBSSH2_KNOWNHOST_READFILE  2111067
#define     brows_ssh_ERROR_LIBSSH2_KNOWNHOST_WRITEFILE 2111068
#define     brows_ssh_ERROR_STRNCPY_S                   2111069
#define     brows_ssh_ERROR_libssh2_scp_recv2           2111070
#define     brows_ERROR_libssh2_channel_read            2111071
#define     brows_ERROR_libssh2_scp_send                2111072
#define     brows_ERROR_libssh2_channel_write           2111073

brows_ssh   brows_ERROR                                 brows_ssh_init(void);
brows_ssh   brows_ERROR                                 brows_ssh_exit(void);
brows_ssh   const char*                                 brows_ssh_name(brows_ERROR);

brows_ssh   brows_ERROR                                 brows_ssh_Chan_eof                      (brows_ssh_Chan*, int32_t* result, brows_Canceler*);
brows_ssh   brows_ERROR                                 brows_ssh_Chan_exec                     (brows_ssh_Chan*, const char* command, brows_Canceler*);
brows_ssh   brows_ERROR                                 brows_ssh_Chan_flush                    (brows_ssh_Chan*, int stream_id, brows_Canceler*);
brows_ssh   brows_ERROR                                 brows_ssh_Chan_close                    (brows_ssh_Chan*, brows_Canceler*);

brows_ssh   brows_ERROR                                 brows_ssh_Conn_auth_by_password         (brows_ssh_Conn*, const char* password, brows_Canceler*);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_auth_by_key_file         (brows_ssh_Conn*, const char* public_key_file, const char* private_key_file, const char* passphrase, brows_Canceler*);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_auth_success             (brows_ssh_Conn*);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_close                    (brows_ssh_Conn*, brows_Canceler*);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_connect                  (brows_ssh_Conn*, brows_Canceler*);
brows_ssh   brows_ssh_Conn*                             brows_ssh_Conn_create                   (void);
brows_ssh   void                                        brows_ssh_Conn_destroy                  (brows_ssh_Conn*);
brows_ssh   brows_ssh_Fingerprint                       brows_ssh_Conn_get_fingerprint          (brows_ssh_Conn*);
brows_ssh   const char*                                 brows_ssh_Conn_get_fingerprint_hash_func(brows_ssh_Conn*);
brows_ssh   brows_ssh_FingerprintSize                   brows_ssh_Conn_get_fingerprint_size     (brows_ssh_Conn*);
brows_ssh   brows_ssh_Host                              brows_ssh_Conn_get_host                 (brows_ssh_Conn*);
brows_ssh   brows_ssh_HostFamily                        brows_ssh_Conn_get_host_family          (brows_ssh_Conn*);
brows_ssh   brows_ssh_Port                              brows_ssh_Conn_get_port                 (brows_ssh_Conn*);
brows_ssh   brows_ssh_Username                          brows_ssh_Conn_get_username             (brows_ssh_Conn*);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_set_host                 (brows_ssh_Conn*, brows_ssh_Host);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_set_host_family          (brows_ssh_Conn*, brows_ssh_HostFamily);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_set_port                 (brows_ssh_Conn*, brows_ssh_Port);
brows_ssh   brows_ERROR                                 brows_ssh_Conn_set_username             (brows_ssh_Conn*, brows_ssh_Username);

brows_ssh   brows_ssh_Exec*                             brows_ssh_Exec_create                   (brows_ssh_Conn*);
brows_ssh   void                                        brows_ssh_Exec_destroy                  (brows_ssh_Exec*);
brows_ssh   brows_ERROR                                 brows_ssh_Exec_read                     (brows_ssh_Exec*, int stream_id, char* buf, size_t buf_len, size_t* result, brows_Canceler*);

brows_ssh   brows_ssh_ScpRecv*                          brows_ssh_ScpRecv_create                (brows_ssh_Conn*, const char* path);
brows_ssh   void                                        brows_ssh_ScpRecv_destroy               (brows_ssh_ScpRecv*);
brows_ssh   int64_t                                     brows_ssh_ScpRecv_get_length            (brows_ssh_ScpRecv*);
brows_ssh   int64_t                                     brows_ssh_ScpRecv_get_position          (brows_ssh_ScpRecv*);
brows_ssh   brows_ERROR                                 brows_ssh_ScpRecv_read                  (brows_ssh_ScpRecv*, char* buf, size_t buf_len, size_t* result, brows_Canceler*);

brows_ssh   brows_ssh_ScpSend*                          brows_ssh_ScpSend_create                (brows_ssh_Conn*, const char* path, int mode, int64_t size);
brows_ssh   void                                        brows_ssh_ScpSend_destroy               (brows_ssh_ScpSend*);
brows_ssh   brows_ERROR                                 brows_ssh_ScpSend_write                 (brows_ssh_ScpSend*, const char* buf, size_t buf_len, size_t* result, brows_Canceler*);

brows_ssh   brows_ssh_KnownHost*                        brows_ssh_KnownHost_create              (void);
brows_ssh   void                                        brows_ssh_KnownHost_destroy             (brows_ssh_KnownHost*);
brows_ssh   const char*                                 brows_ssh_KnownHost_get_key_base64      (brows_ssh_KnownHost*);
brows_ssh   const char*                                 brows_ssh_KnownHost_get_name_plain      (brows_ssh_KnownHost*);
brows_ssh   brows_ssh_KnownHost_Status                  brows_ssh_KnownHost_get_status          (brows_ssh_KnownHost*);
enum        brows_ssh_KnownHost_Status {
                                                        brows_ssh_KnownHost_Status_Uninitialized = 0,
                                                        brows_ssh_KnownHost_Status_NotFound = 1,
                                                        brows_ssh_KnownHost_Status_Match = 2,
                                                        brows_ssh_KnownHost_Status_Mismatch = 3,
                                                        brows_ssh_KnownHost_Status_Error = 4 };

brows_ssh   brows_ERROR                                 brows_ssh_KnownHosts_check              (brows_ssh_KnownHosts*, const char* file, brows_ssh_KnownHostName, brows_ssh_KnownHostPort, brows_ssh_KnownHost*);
brows_ssh   brows_ssh_KnownHosts*                       brows_ssh_KnownHosts_create             (brows_ssh_Conn*);
brows_ssh   void                                        brows_ssh_KnownHosts_destroy            (brows_ssh_KnownHosts*);

#endif
