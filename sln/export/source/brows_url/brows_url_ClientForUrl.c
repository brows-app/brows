#include "brows_url_internal.h"

#define brows_CURL_EASY_SETOPT(CURL, ENUM, ...) do { \
    CURLcode curlcode_for_##ENUM = curl_easy_setopt(CURL, ENUM, __VA_ARGS__); \
    if (curlcode_for_##ENUM) { \
        brows_url_ERROR("Error in curl_easy_setopt " #ENUM " > %d", curlcode_for_##ENUM); \
        brows_url_ERROR("%s", curl_easy_strerror(curlcode_for_##ENUM)); \
        return brows_url_ERROR_curl_easy_setopt; \
    } \
} while (0)

struct brows_url_ClientForUrl {
    BOOL                                    read_callback_set;
    BOOL                                    progress_callback_set;
    BOOL                                    write_callback_set;
    BOOL                                    header_callback_set;
    const char*                             url;
    CURL*                                   curl;
    brows_url_ClientForUrl_DataCallback*    write_callback;
    brows_url_ClientForUrl_DataCallback*    header_callback;
    brows_url_ClientForUrl_ReadCallback*     read_callback;
    brows_url_ClientForUrl_ProgressCallback* progress_callback;
    char*                                   error_buffer;
};

static size_t writefunction(char* ptr, size_t size, size_t nmemb, void* userdata) {
    brows_url_ClientForUrl* p = userdata;
    size_t total = size * nmemb;
    if (p->write_callback) {
        p->write_callback(p, ptr, total);
    }
    return total;
}

static size_t headerfunction(char* buffer, size_t size, size_t nitems, void* userdata) {
    brows_url_ClientForUrl* p = userdata;
    size_t total = size * nitems;
    if (p->header_callback) {
        p->header_callback(p, buffer, total);
    }
    return total;
}

static size_t readfunction(char* buffer, size_t size, size_t nitems, void* userdata) {
    brows_url_ClientForUrl* p = userdata;
    if (size != 0 && nitems > SIZE_MAX / size) {
        return CURL_READFUNC_ABORT;
    }
    size_t total = size * nitems;
    if (p->read_callback) {
        return p->read_callback(p, buffer, total);
    }
    return 0;
}

static int progressfunction(void* userdata,
                            curl_off_t download_total,
                            curl_off_t downloaded,
                            curl_off_t upload_total,
                            curl_off_t uploaded) {
    brows_url_ClientForUrl* p = userdata;
    if (p->progress_callback) {
        return p->progress_callback(p, download_total, downloaded, upload_total, uploaded);
    }
    return 0;
}

static BOOL ftp_path_valid(const char* path) {
    if (NULL == path || path[0] == '\0' || (path[0] == '/' && path[1] == '\0')) {
        return FALSE;
    }
    return strpbrk(path, "\r\n") == NULL;
}

static char* ftp_command_line(const char* name, const char* path) {
    if (NULL == name || ftp_path_valid(path) == FALSE) {
        return NULL;
    }
    size_t name_length = strlen(name);
    size_t path_length = strlen(path);
    if (path_length > SIZE_MAX - name_length - 2) {
        return NULL;
    }
    size_t length = name_length + path_length + 2;
    char* command = malloc(length);
    if (NULL == command) {
        return NULL;
    }
    memcpy(command, name, name_length);
    command[name_length] = ' ';
    memcpy(command + name_length + 1, path, path_length);
    command[name_length + 1 + path_length] = '\0';
    return command;
}

brows_url_ClientForUrl* brows_url_ClientForUrl_create(const char* url) {
    brows_url_ClientForUrl* p = calloc(1, sizeof(brows_url_ClientForUrl));
    if (NULL == p) {
        return NULL;
    }
    CURL* curl = curl_easy_init();
    if (NULL == curl) {
        brows_url_ERROR("%s", "Error in curl_easy_init");
        free(p);
        return NULL;
    }
    CURLcode curlcode = curl_easy_setopt(curl, CURLOPT_URL, url);
    if (curlcode) {
        brows_url_ERROR("Error in curl_easy_setopt CURLOPT_URL > %" PRId32, curlcode);
        curl_easy_cleanup(curl);
        free(p);
        return NULL;
    }
    p->error_buffer = calloc(CURL_ERROR_SIZE * 2, sizeof(char));
    if (NULL == p->error_buffer) {
        brows_url_ERROR("%s", "Failed to allocate space for error buffer.");
        curl_easy_cleanup(curl);
        free(p);
        return NULL;
    }
    curlcode = curl_easy_setopt(curl, CURLOPT_ERRORBUFFER, p->error_buffer);
    if (curlcode) {
        brows_url_ERROR("Error in curl_easy_setopt CURLOPT_ERRORBUFFER > %" PRId32, curlcode);
        curl_easy_cleanup(curl);
        free(p);
        return NULL;
    }
    p->curl = curl;
    p->header_callback = NULL;
    p->header_callback_set = FALSE;
    p->read_callback = NULL;
    p->read_callback_set = FALSE;
    p->progress_callback = NULL;
    p->progress_callback_set = FALSE;
    p->url = url;
    p->write_callback = NULL;
    p->write_callback_set = FALSE;

    return p;
}

void brows_url_ClientForUrl_destroy(brows_url_ClientForUrl* p) {
    if (p) {
        if (p->curl) {
            curl_easy_cleanup(p->curl);
            p->curl = NULL;
        }
        free(p->error_buffer);
        free(p);
    }
}

brows_ERROR brows_url_ClientForUrl_on_write(brows_url_ClientForUrl* p, brows_url_ClientForUrl_DataCallback* callback) {
    assert(p);
    if (FALSE == p->write_callback_set) {
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_WRITEFUNCTION, writefunction);
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_WRITEDATA, p);
        p->write_callback_set = TRUE;
    }
    p->write_callback = callback;
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_on_header(brows_url_ClientForUrl* p, brows_url_ClientForUrl_DataCallback* callback) {
    assert(p);
    if (FALSE == p->header_callback_set) {
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_HEADERFUNCTION, headerfunction);
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_HEADERDATA, p);
        p->header_callback_set = TRUE;
    }
    p->header_callback = callback;
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_on_read(brows_url_ClientForUrl* p, brows_url_ClientForUrl_ReadCallback* callback) {
    assert(p);
    if (FALSE == p->read_callback_set) {
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_READFUNCTION, readfunction);
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_READDATA, p);
        p->read_callback_set = TRUE;
    }
    p->read_callback = callback;
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_on_progress(brows_url_ClientForUrl* p,
                                                brows_url_ClientForUrl_ProgressCallback* callback) {
    assert(p);
    if (FALSE == p->progress_callback_set) {
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_NOPROGRESS, 0L);
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_XFERINFOFUNCTION, progressfunction);
        brows_CURL_EASY_SETOPT(p->curl, CURLOPT_XFERINFODATA, p);
        p->progress_callback_set = TRUE;
    }
    p->progress_callback = callback;
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_username(brows_url_ClientForUrl* p, char* value) {
    assert(p);
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_USERNAME, value);
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_password(brows_url_ClientForUrl* p, char* value) {
    assert(p);
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_PASSWORD, value);
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_txrx(brows_url_ClientForUrl* p) {
    assert(p);
    p->error_buffer[0] = 0;
    CURLcode curlcode = curl_easy_perform(p->curl);
    if (curlcode) {
        brows_url_ERROR("Error in curl_easy_perform > %" PRId32, curlcode);
        brows_url_ERROR("%s", curl_easy_strerror(curlcode));
        brows_url_ERROR("%s", p->error_buffer[0]
            ? p->error_buffer
            : "No more info");
        return brows_url_ERROR_curl_easy_perform;
    }
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_ftp_file_method(brows_url_ClientForUrl* p, brows_url_FtpFileMethod value) {
    assert(p);
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_FTP_FILEMETHOD, 
        value == brows_url_FtpFileMethod_multi_cwd ? CURLFTPMETHOD_MULTICWD :
        value == brows_url_FtpFileMethod_no_cwd ? CURLFTPMETHOD_NOCWD :
        value == brows_url_FtpFileMethod_single_cwd ? CURLFTPMETHOD_SINGLECWD :
        CURLFTPMETHOD_DEFAULT);
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_ftp_tls(brows_url_ClientForUrl* p, brows_url_FtpTlsMode value) {
    assert(p);
    if (value == brows_url_FtpTlsMode_none) {
        if (strncmp(p->url, "ftps://", 7) != 0) {
            return brows_ERROR_NONE;
        }
        value = brows_url_FtpTlsMode_implicit;
    }
    if (value == brows_url_FtpTlsMode_explicit) {
        if (strncmp(p->url, "ftp://", 6) != 0) {
            brows_url_ERROR("%s", "Explicit FTP TLS requires an ftp:// transfer URL.");
            return brows_url_ERROR_ftp_tls_mode;
        }
    }
    else if (value == brows_url_FtpTlsMode_implicit) {
        if (strncmp(p->url, "ftps://", 7) != 0) {
            brows_url_ERROR("%s", "Implicit FTP TLS requires an ftps:// transfer URL.");
            return brows_url_ERROR_ftp_tls_mode;
        }
    }
    else {
        brows_url_ERROR("%s", "The FTP TLS mode is invalid.");
        return brows_url_ERROR_ftp_tls_mode;
    }
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_USE_SSL, (long)CURLUSESSL_ALL);
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_SSL_VERIFYPEER, 1L);
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_SSL_VERIFYHOST, 2L);
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_ftp_ca_info(brows_url_ClientForUrl* p, const char* path) {
    assert(p);
    if (NULL == path || path[0] == '\0') {
        brows_url_ERROR("%s", "The FTP certificate authority path is empty.");
        return brows_url_ERROR_ftp_path;
    }
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_CAINFO, path);
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_ftp_upload(brows_url_ClientForUrl* p, int64_t length, int append) {
    assert(p);
    if (length < -1) {
        brows_url_ERROR("%s", "The FTP upload length is invalid.");
        return brows_url_ERROR_ftp_path;
    }
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_UPLOAD, 1L);
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_APPEND, append ? 1L : 0L);
    brows_CURL_EASY_SETOPT(p->curl, CURLOPT_INFILESIZE_LARGE, (curl_off_t)length);
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_ClientForUrl_ftp_command(brows_url_ClientForUrl* p,
                                                 brows_url_FtpCommand command,
                                                 const char* first_path,
                                                 const char* second_path) {
    assert(p);
    struct curl_slist* list = NULL;
    char* first = NULL;
    char* second = NULL;
    brows_ERROR result = brows_ERROR_NONE;
    const char* first_command = NULL;
    const char* second_command = NULL;
    switch (command) {
        case brows_url_FtpCommand_make_directory:
            first_command = "MKD";
            break;
        case brows_url_FtpCommand_change_directory:
            first_command = "CWD";
            break;
        case brows_url_FtpCommand_delete_file:
            first_command = "DELE";
            break;
        case brows_url_FtpCommand_remove_directory:
            first_command = "RMD";
            break;
        case brows_url_FtpCommand_rename:
            first_command = "RNFR";
            second_command = "RNTO";
            break;
        default:
            brows_url_ERROR("%s", "The FTP command is invalid.");
            return brows_url_ERROR_ftp_command;
    }
    first = ftp_command_line(first_command, first_path);
    if (NULL == first) {
        brows_url_ERROR("%s", "The first FTP command path is invalid or could not be allocated.");
        result = brows_url_ERROR_ftp_path;
        goto cleanup;
    }
    struct curl_slist* updated = curl_slist_append(list, first);
    if (NULL == updated) {
        brows_url_ERROR("%s", "Could not allocate the first FTP command.");
        result = brows_url_ERROR_ftp_command;
        goto cleanup;
    }
    list = updated;
    if (second_command != NULL) {
        second = ftp_command_line(second_command, second_path);
        if (NULL == second) {
            brows_url_ERROR("%s", "The second FTP command path is invalid or could not be allocated.");
            result = brows_url_ERROR_ftp_path;
            goto cleanup;
        }
        updated = curl_slist_append(list, second);
        if (NULL == updated) {
            brows_url_ERROR("%s", "Could not allocate the second FTP command.");
            result = brows_url_ERROR_ftp_command;
            goto cleanup;
        }
        list = updated;
    }
    p->error_buffer[0] = '\0';
    CURLcode curlcode = curl_easy_setopt(p->curl, CURLOPT_QUOTE, list);
    if (curlcode) {
        brows_url_ERROR("Error in curl_easy_setopt CURLOPT_QUOTE > %" PRId32, curlcode);
        brows_url_ERROR("%s", curl_easy_strerror(curlcode));
        result = brows_url_ERROR_curl_easy_setopt;
        goto cleanup;
    }
    curlcode = curl_easy_setopt(p->curl, CURLOPT_NOBODY, 1L);
    if (curlcode) {
        brows_url_ERROR("Error in curl_easy_setopt CURLOPT_NOBODY > %" PRId32, curlcode);
        brows_url_ERROR("%s", curl_easy_strerror(curlcode));
        result = brows_url_ERROR_curl_easy_setopt;
        goto cleanup;
    }
    curlcode = curl_easy_perform(p->curl);
    long response_code = 0;
    curl_easy_getinfo(p->curl, CURLINFO_RESPONSE_CODE, &response_code);
    if (curlcode) {
        brows_url_ERROR("Error in curl_easy_perform > %" PRId32, curlcode);
        brows_url_ERROR("%s", curl_easy_strerror(curlcode));
        brows_url_ERROR("%s", p->error_buffer[0] ? p->error_buffer : "No more info");
        result = command == brows_url_FtpCommand_change_directory &&
                 curlcode == CURLE_QUOTE_ERROR && response_code == 550
            ? brows_url_ERROR_ftp_not_directory
            : brows_url_ERROR_curl_easy_perform;
    }
cleanup:
    curl_easy_setopt(p->curl, CURLOPT_QUOTE, NULL);
    curl_easy_setopt(p->curl, CURLOPT_NOBODY, 0L);
    curl_slist_free_all(list);
    free(first);
    free(second);
    return result;
}

const char* brows_url_ClientForUrl_error_string(brows_url_ClientForUrl* p) {
    assert(p);
    return p->error_buffer;
}
