#include "brows_git_internal.h"

struct brows_GitRef {
    git_reference* agent;
    git_reference* upstream;
    int upstream_exists;
};

static brows_ERROR brows_GitRef_get_upstream(brows_GitRef* p, git_reference** out) {
    assert(p);
    if (p->upstream_exists == 0) {
        if (out) {
            *out = NULL;
        }
        return brows_ERROR_NONE;
    }
    if (p->upstream_exists == 1) {
        if (out) {
            *out = p->upstream;
        }
        return brows_ERROR_NONE;
    }
    int err = git_branch_upstream(&p->upstream, p->agent);
    if (err) {
        if (GIT_ENOTFOUND == err) {
            p->upstream_exists = 0;
            return brows_ERROR_NONE;
        }
        else {
            brows_LOG_ERROR("git_branch_upstream > %" PRId32, err);
            brows_git_log_last_error();
            return brows_git_ERROR_git_branch_upstream;
        }
    }
    p->upstream_exists = 1;
    return brows_ERROR_NONE;
}

brows_GitRef* brows_GitRef_create(git_reference* agent) {
    brows_GitRef* p = calloc(1, sizeof(brows_GitRef));
    if (!p) {
        return NULL;
    }
    p->agent = agent;
    p->upstream = NULL;
    p->upstream_exists = -1;
    return p;
}

void brows_GitRef_destroy(brows_GitRef* p) {
    if (p) {
        git_reference_free(p->agent);
        git_reference_free(p->upstream);
        free(p);
    }
}

const char* brows_GitRef_get_name(brows_GitRef* p) {
    assert(p);
    return git_reference_shorthand(p->agent);
}

brows_ERROR brows_GitRef_compare_remote(brows_GitRef* p, brows_GitRepoComparison* out) {
    assert(p);
    if (out) {
        *out = brows_GitRepoComparison_unknown;
    }
    auto is_branch = git_reference_is_branch(p->agent);
    if (!is_branch) {
        return brows_ERROR_NONE;
    }
    git_reference* upstream = NULL;
    brows_ERROR err = brows_GitRef_get_upstream(p, &upstream);
    if (err) {
        return err;
    }
    if (upstream) {
        int cmp = git_reference_cmp(p->agent, upstream);
        if (cmp) {
            if (out) {
                *out = brows_GitRepoComparison_diff;
            }
        }
        else {
            if (out) {
                *out = brows_GitRepoComparison_same;
            }
        }
    }
    return brows_ERROR_NONE;
}
