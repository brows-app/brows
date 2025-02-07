#include "brows_url_internal.h"

struct brows_url_App {
    int foo;
};

static brows_url_App* brows_url_App_init(brows_url_App* p) {
    if (p) {
    }
    return p;
}

brows_url_App* brows_url_App_create(void) {
    brows_url_App* p = calloc(1, sizeof(brows_url_App));
    brows_url_App* r = brows_url_App_init(p);
    if (NULL == r) {
        brows_url_App_destroy(p);
    }
    return r;
}

void brows_url_App_destroy(brows_url_App* p) {
    if (p) {
        free(p);
    }
}
