#ifndef BROWS_GIT_INTERNAL_H
#define BROWS_GIT_INTERNAL_H

#include "brows_git.h"
#include "git2.h"
#include <assert.h>
#include <malloc.h>
#include <stdint.h>
#include <string.h>

#define brows_git_internal brows_EXTERN_C

brows_git_internal void             brows_git_log_last_error(void);

brows_git_internal brows_GitRef*    brows_GitRef_create     (git_reference* agent);

#endif
