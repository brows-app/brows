#include "brows_git_internal.h"

struct brows_GitRef {
    git_repository* repo;
    git_reference* agent;
    git_reference* upstream;
    int upstream_exists;
};

static brows_ERROR brows_GitRef_get_upstream(brows_GitRef* p, git_reference** out) {
    assert(p);
    if (out) {
        *out = NULL;
    }
    else {
        return brows_git_ERROR_argument;
    }
    if (p->upstream_exists == 0) {
        return brows_ERROR_NONE;
    }
    if (p->upstream_exists == 1) {
        *out = p->upstream;
        return brows_ERROR_NONE;
    }
    int err = git_branch_upstream(&p->upstream, p->agent);
    if (err) {
        if (GIT_ENOTFOUND == err) {
            git_reference_free(p->upstream);
            p->upstream = NULL;
            p->upstream_exists = 0;
            return brows_ERROR_NONE;
        }
        else {
            git_reference_free(p->upstream);
            p->upstream = NULL;
            brows_LOG_ERROR("git_branch_upstream > %" PRId32, err);
            brows_git_log_last_error();
            return brows_git_ERROR_git_branch_upstream;
        }
    }
    p->upstream_exists = 1;
    *out = p->upstream;
    return brows_ERROR_NONE;
}

brows_ERROR brows_GitRef_create(git_reference* agent, git_repository* repo, brows_GitRef** out) {
    if (out) {
        *out = NULL;
    }
    if (NULL == agent || NULL == repo || NULL == out) {
        if (agent) {
            git_reference_free(agent);
        }
        return brows_git_ERROR_argument;
    }
    brows_GitRef* p = calloc(1, sizeof(brows_GitRef));
    if (!p) {
        git_reference_free(agent);
        return brows_git_ERROR_calloc;
    }
    const char* path = git_repository_workdir(repo);
    uint32_t flags = GIT_REPOSITORY_OPEN_NO_SEARCH;
    if (NULL == path) {
        path = git_repository_path(repo);
        flags |= GIT_REPOSITORY_OPEN_BARE;
    }
    int err = git_repository_open_ext(&p->repo, path, flags, NULL);
    if (err != 0) {
        brows_LOG_ERROR("git_repository_open_ext > %" PRId32, err);
        brows_git_log_last_error();
        git_reference_free(agent);
        free(p);
        return brows_git_ERROR_git_reference;
    }
    const char* name = git_reference_name(agent);
    err = git_reference_lookup(&p->agent, p->repo, name);
    if (err != 0) {
        brows_LOG_ERROR("git_reference_lookup > %" PRId32, err);
        brows_git_log_last_error();
        git_reference_free(agent);
        git_repository_free(p->repo);
        free(p);
        return brows_git_ERROR_git_reference;
    }
    git_reference_free(agent);
    p->upstream = NULL;
    p->upstream_exists = -1;
    *out = p;
    return brows_ERROR_NONE;
}

void brows_GitRef_destroy(brows_GitRef* p) {
    if (p) {
        git_reference_free(p->agent);
        git_reference_free(p->upstream);
        git_repository_free(p->repo);
        free(p);
    }
}

const char* brows_GitRef_get_name(brows_GitRef* p) {
    assert(p);
    return git_reference_shorthand(p->agent);
}

brows_ERROR brows_GitRef_compare_remote(brows_GitRef* p, brows_GitRepoComparison* out) {
    if (out) {
        *out = brows_GitRepoComparison_unknown;
    }
    if (NULL == p || NULL == p->agent || NULL == out) {
        return brows_git_ERROR_argument;
    }
    int is_branch = git_reference_is_branch(p->agent);
    if (!is_branch) {
        return brows_ERROR_NONE;
    }
    git_reference* upstream = NULL;
    brows_ERROR err = brows_GitRef_get_upstream(p, &upstream);
    if (err) {
        return err;
    }
    if (upstream) {
        const git_oid* local_target = git_reference_target(p->agent);
        const git_oid* upstream_target = git_reference_target(upstream);
        if (local_target && upstream_target) {
            *out = git_oid_equal(local_target, upstream_target)
                ? brows_GitRepoComparison_same
                : brows_GitRepoComparison_diff;
        }
    }
    return brows_ERROR_NONE;
}
