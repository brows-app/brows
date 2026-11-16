#ifndef BROWS_GIT_INTERNAL_H
#define BROWS_GIT_INTERNAL_H

#include "brows_git.h"
#include "git2.h"
#include <assert.h>
#include <malloc.h>
#include <stdint.h>
#include <string.h>

#define brows_git_internal brows_EXTERN_C

struct brows_GitRepo {
    git_repository* agent;
    git_config* config;
};

static int brows_git_copy_string(char** destination, const char* source) {
    assert(destination);
    *destination = NULL;
    if (NULL == source) {
        return 1;
    }
    size_t length = strlen(source);
    if (length == SIZE_MAX) {
        return 0;
    }
    char* copy = malloc(length + 1);
    if (NULL == copy) {
        return 0;
    }
    memcpy(copy, source, length + 1);
    *destination = copy;
    return 1;
}

brows_git_internal void         brows_git_log_last_error  (void);

brows_git_internal brows_ERROR  brows_GitRef_create       (git_reference* agent, git_repository* repo, brows_GitRef** out);

#endif
