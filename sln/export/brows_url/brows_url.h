#ifndef BROWS_URL_H
#define BROWS_URL_H

#include <stdbool.h>
#include "brows_native.h"

#ifdef          brows_url_IMPORT
#define         brows_url                               brows_DLLIMPORT
#endif

#ifdef          brows_url_EXPORT
#define         brows_url                               brows_DLLEXPORT
#endif

#ifndef         brows_url
#define         brows_url                               brows_EXTERN_C
#endif

#define         brows_url_ERROR_curl_global_init        2211000
#define         brows_url_ERROR_curl_easy_setopt        2211001
#define         brows_url_ERROR_curl_easy_perform       2211002

typedef enum    brows_url_FtpFileMethod {
                brows_url_FtpFileMethod_multi_cwd       = 1,
                brows_url_FtpFileMethod_no_cwd          = 2,
                brows_url_FtpFileMethod_single_cwd      = 3 }
                brows_url_FtpFileMethod;

typedef struct  brows_url_ClientForUrl                  brows_url_ClientForUrl;
typedef         brows_ERROR(                            brows_url_ClientForUrl_DataCallback)(   brows_url_ClientForUrl*, const char* buf, size_t len);

brows_url       brows_ERROR                             brows_url_exit(void);
brows_url       brows_ERROR                             brows_url_init(void);

brows_url       brows_url_ClientForUrl*                 brows_url_ClientForUrl_create           (const char* url);
brows_url       void                                    brows_url_ClientForUrl_destroy          (brows_url_ClientForUrl*);
brows_url       const char*                             brows_url_ClientForUrl_error_string     (brows_url_ClientForUrl*);
brows_url       brows_ERROR                             brows_url_ClientForUrl_on_header        (brows_url_ClientForUrl*, brows_url_ClientForUrl_DataCallback*);
brows_url       brows_ERROR                             brows_url_ClientForUrl_on_write         (brows_url_ClientForUrl*, brows_url_ClientForUrl_DataCallback*);
brows_url       brows_ERROR                             brows_url_ClientForUrl_password         (brows_url_ClientForUrl*, char*);
brows_url       brows_ERROR                             brows_url_ClientForUrl_txrx             (brows_url_ClientForUrl*);
brows_url       brows_ERROR                             brows_url_ClientForUrl_username         (brows_url_ClientForUrl*, char*);
brows_url       brows_ERROR                             brows_url_ClientForUrl_ftp_file_method  (brows_url_ClientForUrl*, brows_url_FtpFileMethod);

#endif
