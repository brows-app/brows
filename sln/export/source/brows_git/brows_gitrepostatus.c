#include "brows_git_internal.h"
#include <limits.h>

typedef struct brows_GitFileStatusEntry {
    char* path;
    char* original_path;
    uint32_t status;
} brows_GitFileStatusEntry;

struct brows_GitRepositoryStatus {
    char* work_directory;
    char* git_directory;
    char* branch;
    char* head_id;
    char* upstream;
    brows_GitRepoState state;
    int32_t is_unborn;
    int32_t is_detached;
    int32_t is_bare;
    int32_t upstream_missing;
    int32_t has_ahead;
    int32_t ahead;
    int32_t has_behind;
    int32_t behind;
    size_t file_count;
    brows_GitFileStatusEntry* files;
};

static void brows_GitRepositoryStatus_free(brows_GitRepositoryStatus* p) {
    if (NULL == p) {
        return;
    }
    free(p->work_directory);
    free(p->git_directory);
    free(p->branch);
    free(p->head_id);
    free(p->upstream);
    for (size_t i = 0; i < p->file_count; i++) {
        free(p->files[i].path);
        free(p->files[i].original_path);
    }
    free(p->files);
    free(p);
}

static int32_t brows_GitRepositoryStatus_copy_string(char** destination, const char* source) {
    return brows_git_copy_string(destination, source) ? 1 : 0;
}

static int32_t brows_GitRepositoryStatus_copy_oid(char** destination, const git_oid* oid) {
    if (NULL == oid) {
        *destination = NULL;
        return 1;
    }
    return brows_GitRepositoryStatus_copy_string(destination, git_oid_tostr_s(oid));
}

static const char* brows_GitRepositoryStatus_shorthand(const char* name) {
    static const char* remote_prefix = "refs/remotes/";
    static const char* local_prefix = "refs/heads/";
    if (name && strncmp(name, remote_prefix, strlen(remote_prefix)) == 0) {
        return name + strlen(remote_prefix);
    }
    if (name && strncmp(name, local_prefix, strlen(local_prefix)) == 0) {
        return name + strlen(local_prefix);
    }
    return name;
}

static int32_t brows_GitRepositoryStatus_copy_upstream_fallback(
    char** destination,
    const char* remote_name,
    const char* merge_target) {

    const char* merge_short = brows_GitRepositoryStatus_shorthand(merge_target);
    if (remote_name && strcmp(remote_name, ".") != 0 && merge_short) {
        size_t remote_length = strlen(remote_name);
        size_t merge_length = strlen(merge_short);
        if (remote_length > SIZE_MAX - merge_length - 2) {
            return 0;
        }
        char* combined = malloc(remote_length + merge_length + 2);
        if (NULL == combined) {
            return 0;
        }
        memcpy(combined, remote_name, remote_length);
        combined[remote_length] = '/';
        memcpy(combined + remote_length + 1, merge_short, merge_length + 1);
        *destination = combined;
        return 1;
    }

    const char* fallback = merge_short ? merge_short : remote_name;
    return brows_GitRepositoryStatus_copy_string(destination, fallback);
}

static int32_t brows_GitRepositoryStatus_copy_file(brows_GitFileStatusEntry* destination, const git_status_entry* source) {
    const char* path = NULL;
    const char* original_path = NULL;
    if ((source->status & GIT_STATUS_WT_RENAMED) && source->index_to_workdir) {
        path = source->index_to_workdir->new_file.path;
        original_path = source->index_to_workdir->old_file.path;
    }
    if ((source->status & GIT_STATUS_INDEX_RENAMED) && source->head_to_index) {
        if (NULL == path) {
            path = source->head_to_index->new_file.path;
        }
        original_path = source->head_to_index->old_file.path;
    }
    if (NULL == path && source->index_to_workdir) {
        path = source->index_to_workdir->new_file.path;
        if (NULL == path) {
            path = source->index_to_workdir->old_file.path;
        }
    }
    if (NULL == path && source->head_to_index) {
        path = source->head_to_index->new_file.path;
        if (NULL == path) {
            path = source->head_to_index->old_file.path;
        }
    }

    if (!brows_GitRepositoryStatus_copy_string(&destination->path, path ? path : "") ||
        !brows_GitRepositoryStatus_copy_string(&destination->original_path, original_path)) {
        return 0;
    }
    destination->status = (uint32_t)source->status;
    return 1;
}

static brows_ERROR brows_GitRepositoryStatus_tracking_config(
    git_repository* repo,
    const char* branch,
    int32_t* configured,
    char** remote_name_out,
    char** merge_target_out) {

    *configured = 0;
    *remote_name_out = NULL;
    *merge_target_out = NULL;

    git_buf remote_buf = GIT_BUF_INIT;
    git_buf merge_buf = GIT_BUF_INIT;
    int remote_error = git_branch_upstream_remote(&remote_buf, repo, branch);
    int merge_error = git_branch_upstream_merge(&merge_buf, repo, branch);
    if ((remote_error != 0 && remote_error != GIT_ENOTFOUND) ||
        (merge_error != 0 && merge_error != GIT_ENOTFOUND)) {
        brows_git_log_last_error();
        git_buf_dispose(&remote_buf);
        git_buf_dispose(&merge_buf);
        return brows_git_ERROR_git_repository_status;
    }

    if (remote_error == 0 || merge_error == 0) {
        *configured = 1;
        if ((remote_buf.ptr && !brows_git_copy_string(remote_name_out, remote_buf.ptr)) ||
            (merge_buf.ptr && !brows_git_copy_string(merge_target_out, merge_buf.ptr))) {
            free(*remote_name_out);
            *remote_name_out = NULL;
            free(*merge_target_out);
            *merge_target_out = NULL;
            git_buf_dispose(&remote_buf);
            git_buf_dispose(&merge_buf);
            return brows_git_ERROR_calloc;
        }
    }

    git_buf_dispose(&remote_buf);
    git_buf_dispose(&merge_buf);
    return brows_ERROR_NONE;
}

static brows_ERROR brows_GitRepositoryStatus_upstream(
    git_repository* repo,
    const char* branch_ref_name,
    const git_oid* head_oid,
    brows_GitRepositoryStatus* status) {

    if (NULL == branch_ref_name || status->is_detached) {
        return brows_ERROR_NONE;
    }

    git_buf upstream_name = GIT_BUF_INIT;
    int err = git_branch_upstream_name(&upstream_name, repo, branch_ref_name);
    if (err == GIT_ENOTFOUND) {
        int32_t configured = 0;
        char* remote_name = NULL;
        char* merge_target = NULL;
        brows_ERROR config_error = brows_GitRepositoryStatus_tracking_config(
            repo,
            branch_ref_name,
            &configured,
            &remote_name,
            &merge_target);
        if (config_error != brows_ERROR_NONE) {
            git_buf_dispose(&upstream_name);
            return config_error;
        }
        if (configured) {
            status->upstream_missing = 1;
            if (!brows_GitRepositoryStatus_copy_upstream_fallback(&status->upstream, remote_name, merge_target)) {
                free(remote_name);
                free(merge_target);
                git_buf_dispose(&upstream_name);
                return brows_git_ERROR_calloc;
            }
        }
        free(remote_name);
        free(merge_target);
        git_buf_dispose(&upstream_name);
        return brows_ERROR_NONE;
    }
    if (err != 0) {
        brows_git_log_last_error();
        git_buf_dispose(&upstream_name);
        return brows_git_ERROR_git_repository_status;
    }
    if (!brows_GitRepositoryStatus_copy_string(
        &status->upstream,
        brows_GitRepositoryStatus_shorthand(upstream_name.ptr))) {
        git_buf_dispose(&upstream_name);
        return brows_git_ERROR_calloc;
    }

    git_reference* upstream = NULL;
    err = git_reference_lookup(&upstream, repo, upstream_name.ptr);
    git_buf_dispose(&upstream_name);
    if (err == GIT_ENOTFOUND) {
        status->upstream_missing = 1;
        git_reference_free(upstream);
        return brows_ERROR_NONE;
    }
    if (err != 0) {
        brows_git_log_last_error();
        git_reference_free(upstream);
        return brows_git_ERROR_git_repository_status;
    }

    const git_oid* upstream_oid = git_reference_target(upstream);
    if (head_oid && upstream_oid) {
        size_t ahead = 0;
        size_t behind = 0;
        err = git_graph_ahead_behind(&ahead, &behind, repo, head_oid, upstream_oid);
        if (err == 0) {
            status->has_ahead = 1;
            status->ahead = ahead > INT_MAX ? INT_MAX : (int32_t)ahead;
            status->has_behind = 1;
            status->behind = behind > INT_MAX ? INT_MAX : (int32_t)behind;
        }
    }
    git_reference_free(upstream);
    return brows_ERROR_NONE;
}

static brows_ERROR brows_GitRepositoryStatus_add_files(git_repository* repo, brows_GitRepositoryStatus* status) {
    if (status->is_bare) {
        return brows_ERROR_NONE;
    }

    git_index* index = NULL;
    int err = git_repository_index(&index, repo);
    if (err != 0) {
        brows_git_log_last_error();
        git_index_free(index);
        return brows_git_ERROR_git_repository_status;
    }
    err = git_index_read(index, 1);
    git_index_free(index);
    if (err != 0) {
        brows_git_log_last_error();
        return brows_git_ERROR_git_repository_status;
    }

    git_status_options options = GIT_STATUS_OPTIONS_INIT;
    err = git_status_options_init(&options, GIT_STATUS_OPTIONS_VERSION);
    if (err != 0) {
        brows_git_log_last_error();
        return brows_git_ERROR_git_repository_status;
    }
    options.flags = GIT_STATUS_OPT_INCLUDE_UNTRACKED |
        GIT_STATUS_OPT_RECURSE_UNTRACKED_DIRS |
        GIT_STATUS_OPT_INCLUDE_IGNORED |
        GIT_STATUS_OPT_RENAMES_HEAD_TO_INDEX |
        GIT_STATUS_OPT_RENAMES_INDEX_TO_WORKDIR |
        GIT_STATUS_OPT_INCLUDE_UNREADABLE |
        GIT_STATUS_OPT_NO_REFRESH;

    git_status_list* list = NULL;
    err = git_status_list_new(&list, repo, &options);
    if (err != 0) {
        brows_git_log_last_error();
        git_status_list_free(list);
        return brows_git_ERROR_git_repository_status;
    }

    size_t count = git_status_list_entrycount(list);
    if (count > SIZE_MAX / sizeof(brows_GitFileStatusEntry)) {
        git_status_list_free(list);
        return brows_git_ERROR_calloc;
    }
    if (count > 0) {
        status->files = calloc(count, sizeof(brows_GitFileStatusEntry));
        if (NULL == status->files) {
            git_status_list_free(list);
            return brows_git_ERROR_calloc;
        }
    }

    for (size_t i = 0; i < count; i++) {
        const git_status_entry* entry = git_status_byindex(list, i);
        if (NULL == entry || !brows_GitRepositoryStatus_copy_file(&status->files[i], entry)) {
            status->file_count = i + (entry ? 1 : 0);
            git_status_list_free(list);
            return entry ? brows_git_ERROR_calloc : brows_git_ERROR_git_repository_status;
        }
        status->file_count++;
    }

    git_status_list_free(list);
    return brows_ERROR_NONE;
}

brows_ERROR brows_GitRepo_status(brows_GitRepo* repo, brows_GitRepositoryStatus** out) {
    if (out) {
        *out = NULL;
    }
    if (NULL == repo || NULL == repo->agent || NULL == out) {
        return brows_git_ERROR_argument;
    }

    brows_GitRepositoryStatus* status = calloc(1, sizeof(brows_GitRepositoryStatus));
    if (NULL == status) {
        return brows_git_ERROR_calloc;
    }

    brows_ERROR result = brows_ERROR_NONE;
    git_reference* head = NULL;
    git_reference* unborn_head = NULL;
    const char* branch_ref_name = NULL;
    const git_oid* head_oid = NULL;

    result = brows_GitRepositoryStatus_copy_string(&status->work_directory, git_repository_workdir(repo->agent))
        ? brows_ERROR_NONE
        : brows_git_ERROR_calloc;
    if (result != brows_ERROR_NONE) {
        goto cleanup;
    }
    result = brows_GitRepositoryStatus_copy_string(&status->git_directory, git_repository_path(repo->agent))
        ? brows_ERROR_NONE
        : brows_git_ERROR_calloc;
    if (result != brows_ERROR_NONE) {
        goto cleanup;
    }

    status->state = git_repository_state(repo->agent);
    status->is_bare = git_repository_is_bare(repo->agent);
    int detached = git_repository_head_detached(repo->agent);
    int unborn = git_repository_head_unborn(repo->agent);
    if (detached < 0 || unborn < 0) {
        brows_git_log_last_error();
        result = brows_git_ERROR_git_repository_status;
        goto cleanup;
    }
    status->is_detached = detached != 0;
    status->is_unborn = unborn != 0;

    int head_error = git_repository_head(&head, repo->agent);
    if (head_error == 0) {
        if (!status->is_detached) {
            result = brows_GitRepositoryStatus_copy_string(&status->branch, git_reference_shorthand(head))
                ? brows_ERROR_NONE
                : brows_git_ERROR_calloc;
            if (result != brows_ERROR_NONE) {
                goto cleanup;
            }
            branch_ref_name = git_reference_name(head);
        }
        head_oid = git_reference_target(head);
        result = brows_GitRepositoryStatus_copy_oid(&status->head_id, head_oid)
            ? brows_ERROR_NONE
            : brows_git_ERROR_calloc;
        if (result != brows_ERROR_NONE) {
            goto cleanup;
        }
    }
    else if (status->is_unborn && (head_error == GIT_EUNBORNBRANCH || head_error == GIT_ENOTFOUND)) {
        int lookup_error = git_reference_lookup(&unborn_head, repo->agent, "HEAD");
        if (lookup_error != 0) {
            brows_git_log_last_error();
            result = brows_git_ERROR_git_repository_status;
            goto cleanup;
        }
        const char* target = git_reference_symbolic_target(unborn_head);
        const char* prefix = "refs/heads/";
        if (!status->is_detached && target && strncmp(target, prefix, strlen(prefix)) == 0) {
            const char* short_name = target + strlen(prefix);
            result = brows_GitRepositoryStatus_copy_string(&status->branch, short_name)
                ? brows_ERROR_NONE
                : brows_git_ERROR_calloc;
            if (result != brows_ERROR_NONE) {
                goto cleanup;
            }
            branch_ref_name = target;
        }
        result = brows_GitRepositoryStatus_copy_oid(&status->head_id, NULL)
            ? brows_ERROR_NONE
            : brows_git_ERROR_calloc;
        if (result != brows_ERROR_NONE) {
            goto cleanup;
        }
    }
    else {
        brows_git_log_last_error();
        result = brows_git_ERROR_git_repository_status;
        goto cleanup;
    }

    result = brows_GitRepositoryStatus_upstream(repo->agent, branch_ref_name, head_oid, status);
    if (result != brows_ERROR_NONE) {
        goto cleanup;
    }

    result = brows_GitRepositoryStatus_add_files(repo->agent, status);
    if (result != brows_ERROR_NONE) {
        goto cleanup;
    }

    *out = status;
    status = NULL;

cleanup:
    git_reference_free(head);
    git_reference_free(unborn_head);
    brows_GitRepositoryStatus_free(status);
    return result;
}

void brows_GitRepositoryStatus_destroy(brows_GitRepositoryStatus* p) {
    brows_GitRepositoryStatus_free(p);
}

const char* brows_GitRepositoryStatus_get_work_directory(brows_GitRepositoryStatus* p) {
    return p ? p->work_directory : NULL;
}

const char* brows_GitRepositoryStatus_get_git_directory(brows_GitRepositoryStatus* p) {
    return p ? p->git_directory : NULL;
}

const char* brows_GitRepositoryStatus_get_branch(brows_GitRepositoryStatus* p) {
    return p ? p->branch : NULL;
}

const char* brows_GitRepositoryStatus_get_head_id(brows_GitRepositoryStatus* p) {
    return p ? p->head_id : NULL;
}

const char* brows_GitRepositoryStatus_get_upstream(brows_GitRepositoryStatus* p) {
    return p ? p->upstream : NULL;
}

brows_GitRepoState brows_GitRepositoryStatus_get_state(brows_GitRepositoryStatus* p) {
    return p ? p->state : brows_GitRepoState_none;
}

int32_t brows_GitRepositoryStatus_get_is_unborn(brows_GitRepositoryStatus* p) {
    return p ? p->is_unborn : 0;
}

int32_t brows_GitRepositoryStatus_get_is_detached(brows_GitRepositoryStatus* p) {
    return p ? p->is_detached : 0;
}

int32_t brows_GitRepositoryStatus_get_is_bare(brows_GitRepositoryStatus* p) {
    return p ? p->is_bare : 0;
}

int32_t brows_GitRepositoryStatus_get_upstream_missing(brows_GitRepositoryStatus* p) {
    return p ? p->upstream_missing : 0;
}

int32_t brows_GitRepositoryStatus_get_has_ahead(brows_GitRepositoryStatus* p) {
    return p ? p->has_ahead : 0;
}

int32_t brows_GitRepositoryStatus_get_ahead(brows_GitRepositoryStatus* p) {
    return p ? p->ahead : 0;
}

int32_t brows_GitRepositoryStatus_get_has_behind(brows_GitRepositoryStatus* p) {
    return p ? p->has_behind : 0;
}

int32_t brows_GitRepositoryStatus_get_behind(brows_GitRepositoryStatus* p) {
    return p ? p->behind : 0;
}

size_t brows_GitRepositoryStatus_get_file_count(brows_GitRepositoryStatus* p) {
    return p ? p->file_count : 0;
}

const char* brows_GitRepositoryStatus_get_file_path(brows_GitRepositoryStatus* p, size_t index) {
    return p && index < p->file_count ? p->files[index].path : NULL;
}

const char* brows_GitRepositoryStatus_get_file_original_path(brows_GitRepositoryStatus* p, size_t index) {
    return p && index < p->file_count ? p->files[index].original_path : NULL;
}

uint32_t brows_GitRepositoryStatus_get_file_status(brows_GitRepositoryStatus* p, size_t index) {
    return p && index < p->file_count ? p->files[index].status : 0;
}
