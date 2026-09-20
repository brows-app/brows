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
    BOOL                                    write_callback_set;
    BOOL                                    header_callback_set;
    const char*                             url;
    CURL*                                   curl;
    brows_url_ClientForUrl_DataCallback*    write_callback;
    brows_url_ClientForUrl_DataCallback*    header_callback;
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

const char* brows_url_ClientForUrl_error_string(brows_url_ClientForUrl* p) {
    assert(p);
    return p->error_buffer;
}
