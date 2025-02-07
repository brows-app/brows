#include "brows_url_internal.h"

brows_ERROR brows_url_init(void) {
    CURLcode curlcode = curl_global_init(CURL_GLOBAL_DEFAULT);
    if (curlcode) {
        brows_url_ERROR("Error int curl_global_init > %d", curlcode);
        return brows_url_ERROR_curl_global_init;
    }
    return brows_ERROR_NONE;
}

brows_ERROR brows_url_exit(void) {
    curl_global_cleanup();
    return brows_ERROR_NONE;
}
