#ifndef BROWS_URL_H
#define BROWS_URL_H

#include <stdbool.h>
#include "brows_framework.h"

#ifdef          brows_url_IMPORT
#define         brows_url                             brows_DLLIMPORT
#endif

#ifdef          brows_url_EXPORT
#define         brows_url                             brows_DLLEXPORT
#endif

#ifndef         brows_url
#define         brows_url                             brows_EXTERN_C
#endif

#define         brows_url_ERROR_curl_global_init      2211000
#define         brows_url_ERROR_curl_easy_setopt      2211001
#define         brows_url_ERROR_curl_easy_perform     2211002
#define         brows_url_ERROR_ftp_command           2211003
#define         brows_url_ERROR_ftp_path              2211004
#define         brows_url_ERROR_ftp_tls_mode          2211005
#define         brows_url_ERROR_ftp_not_directory     2211006

typedef enum    brows_url_FtpFileMethod {
                brows_url_FtpFileMethod_multi_cwd     = 1,
                brows_url_FtpFileMethod_no_cwd        = 2,
                brows_url_FtpFileMethod_single_cwd    = 3 }
                brows_url_FtpFileMethod;

typedef enum    brows_url_FtpTlsMode {
                brows_url_FtpTlsMode_auto             = 0,
                brows_url_FtpTlsMode_none             = 1,
                brows_url_FtpTlsMode_explicit         = 2,
                brows_url_FtpTlsMode_implicit         = 3 }
                brows_url_FtpTlsMode;

typedef enum    brows_url_FtpCommand {
                brows_url_FtpCommand_make_directory   = 1,
                brows_url_FtpCommand_delete_file      = 2,
                brows_url_FtpCommand_remove_directory = 3,
                brows_url_FtpCommand_rename           = 4,
                brows_url_FtpCommand_change_directory = 5 }
                brows_url_FtpCommand;

typedef struct  brows_url_ClientForUrl  brows_url_ClientForUrl;
typedef         brows_ERROR(            brows_url_ClientForUrl_DataCallback)(   brows_url_ClientForUrl*, const char* buf, size_t len);
typedef         size_t(                 brows_url_ClientForUrl_ReadCallback)(  brows_url_ClientForUrl*, char* buf, size_t len);
typedef         int(                    brows_url_ClientForUrl_ProgressCallback)(brows_url_ClientForUrl*, int64_t, int64_t, int64_t, int64_t);

brows_url       brows_ERROR             brows_url_exit(void);
brows_url       brows_ERROR             brows_url_init(void);

brows_url       brows_url_ClientForUrl* brows_url_ClientForUrl_create           (const char* url);
brows_url       void                    brows_url_ClientForUrl_destroy          (brows_url_ClientForUrl*);
brows_url       const char*             brows_url_ClientForUrl_error_string     (brows_url_ClientForUrl*);
brows_url       brows_ERROR             brows_url_ClientForUrl_on_header        (brows_url_ClientForUrl*, brows_url_ClientForUrl_DataCallback*);
brows_url       brows_ERROR             brows_url_ClientForUrl_on_write         (brows_url_ClientForUrl*, brows_url_ClientForUrl_DataCallback*);
brows_url       brows_ERROR             brows_url_ClientForUrl_password         (brows_url_ClientForUrl*, char*);
brows_url       brows_ERROR             brows_url_ClientForUrl_txrx             (brows_url_ClientForUrl*);
brows_url       brows_ERROR             brows_url_ClientForUrl_username         (brows_url_ClientForUrl*, char*);
brows_url       brows_ERROR             brows_url_ClientForUrl_ftp_file_method  (brows_url_ClientForUrl*, brows_url_FtpFileMethod);
brows_url       brows_ERROR             brows_url_ClientForUrl_on_read          (brows_url_ClientForUrl*, brows_url_ClientForUrl_ReadCallback*);
brows_url       brows_ERROR             brows_url_ClientForUrl_on_progress      (brows_url_ClientForUrl*, brows_url_ClientForUrl_ProgressCallback*);
brows_url       brows_ERROR             brows_url_ClientForUrl_ftp_tls          (brows_url_ClientForUrl*, brows_url_FtpTlsMode);
brows_url       brows_ERROR             brows_url_ClientForUrl_ftp_ca_info      (brows_url_ClientForUrl*, const char*);
brows_url       brows_ERROR             brows_url_ClientForUrl_ftp_upload       (brows_url_ClientForUrl*, int64_t, int);
brows_url       brows_ERROR             brows_url_ClientForUrl_ftp_command      (brows_url_ClientForUrl*, brows_url_FtpCommand, const char*, const char*);

#endif
