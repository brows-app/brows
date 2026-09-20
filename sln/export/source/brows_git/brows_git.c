#include "brows_git_internal.h"

brows_ERROR brows_git_init(void) {
    int n = git_libgit2_init();
    brows_LOG_INFO("git_libgit2_init > %" PRId32, n);
    return brows_ERROR_NONE;
}

brows_ERROR brows_git_exit(void) {
    int n = git_libgit2_shutdown();
    brows_LOG_INFO("git_libgit2_shutdown > %" PRId32, n);
    return brows_ERROR_NONE;
}

void brows_git_log_last_error(void) {
    const git_error* error = git_error_last();
    if (error) {
        brows_LOG_WARN("git_error_last > %" PRId32 " > %s", error->klass, error->message);
    }
    else {
        brows_LOG_WARN("%s", "git_error_last > NULL");
    }
}
