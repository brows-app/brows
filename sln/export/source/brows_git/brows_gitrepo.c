#include "brows_git_internal.h"

brows_ERROR brows_GitRepo_find(const char* path, int32_t search, brows_GitRepo** out) {
    if (out) {
        *out = NULL;
    }
    if (NULL == path || NULL == out) {
        return brows_git_ERROR_argument;
    }
    brows_GitRepo* p = calloc(1, sizeof(brows_GitRepo));
    if (!p) {
        return brows_git_ERROR_calloc;
    }
    int32_t flags = search ? 0 : GIT_REPOSITORY_OPEN_NO_SEARCH;
    int err = git_repository_open_ext(&p->agent, path, flags, NULL);
    if (err) {
        brows_GitRepo_destroy(p);
        if (GIT_ENOTFOUND == err) {
            return brows_ERROR_NONE;
        }
        brows_LOG_ERROR("git_repository_open > %" PRId32, err);
        brows_git_log_last_error();
        return brows_git_ERROR_git_repository_open;
    }
    *out = p;
    return brows_ERROR_NONE;
}

void brows_GitRepo_destroy(brows_GitRepo* p) {
    if (NULL == p) {
        return;
    }
    git_config_free(p->config);
    git_repository_free(p->agent);
    free(p);
}

const char* brows_GitRepo_get_path(brows_GitRepo* p) {
    assert(p);
    return git_repository_path(p->agent);
}

brows_GitRepoState brows_GitRepo_get_state(brows_GitRepo* p) {
    assert(p);
    return git_repository_state(p->agent);
}

brows_ERROR brows_GitRepo_head(brows_GitRepo* p, brows_GitRef** out) {
    if (out) {
        *out = NULL;
    }
    if (NULL == p || NULL == p->agent || NULL == out) {
        return brows_git_ERROR_argument;
    }
    git_reference* head = NULL;
    int err = git_repository_head(&head, p->agent);
    if (err) {
        git_reference_free(head);
        brows_LOG_ERROR("git_repository_head > %" PRId32, err);
        brows_git_log_last_error();
        return brows_git_ERROR_git_repository_head;
    }
    brows_GitRef* ref = NULL;
    brows_ERROR create_error = brows_GitRef_create(head, p->agent, &ref);
    if (create_error != brows_ERROR_NONE) {
        return create_error;
    }
    *out = ref;
    return brows_ERROR_NONE;
}

brows_ERROR brows_GitRepo_config_entry(brows_GitRepo* p, const char* name, brows_GitConfigEntry** result) {
    if (result) {
        *result = NULL;
    }
    if (NULL == p || NULL == p->agent || NULL == name || NULL == result) {
        return brows_git_ERROR_argument;
    }
    int err = 0;
    brows_ERROR ret = brows_ERROR_NONE;
    git_config_entry* entry = NULL;
    if (NULL == p->config) {
        err = git_repository_config_snapshot(&p->config, p->agent);
        if (err) {
            brows_LOG_ERROR("git_repository_config > %" PRId32, err);
            ret = brows_git_ERROR_git_repository_config;
            goto cleanup;
        }
    }
    err = git_config_get_entry(&entry, p->config, name);
    if (err) {
        brows_LOG_ERROR("git_config_get_entry > %" PRId32, err);
        ret = brows_git_ERROR_git_config_get_entry;
        goto cleanup;
    }
    *result = brows_GitConfigEntry_create(entry->name, entry->value, entry->backend_type, entry->origin_path, entry->level, entry->include_depth);
    if (NULL == *result) {
        ret = brows_git_ERROR_calloc;
    }
cleanup:
    if (entry) {
        git_config_entry_free(entry);
    }
    if (err) {
        brows_git_log_last_error();
    }
    return ret;
}
