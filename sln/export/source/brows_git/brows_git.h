#ifndef     BROWS_GIT_H
#define     BROWS_GIT_H

#include    "brows_framework.h"

#ifdef      brows_git_IMPORT
#define     brows_git                                   brows_DLLIMPORT
#endif

#ifdef      brows_git_EXPORT
#define     brows_git                                   brows_DLLEXPORT
#endif

#ifndef     brows_git
#define     brows_git                                   brows_EXTERN_C
#endif

typedef struct  brows_GitConfigEntry                        brows_GitConfigEntry;
typedef struct  brows_GitRef                                brows_GitRef;
typedef struct  brows_GitRepo                               brows_GitRepo;

typedef enum    brows_GitConfigLevel {
                brows_GitConfigLevel_program_data = 1,
                brows_GitConfigLevel_system = 2,
                brows_GitConfigLevel_xdg = 3,
                brows_GitConfigLevel_global = 4,
                brows_GitConfigLevel_local = 5,
                brows_GitConfigLevel_worktree = 6,
                brows_GitConfigLevel_app = 7,
                brows_GitConfigLevel_highest = -1
}               brows_GitConfigLevel;

typedef enum    brows_GitRepoState {
                brows_GitRepoState_none                     = 0,
                brows_GitRepoState_merge                    = 1,
                brows_GitRepoState_revert                   = 2,
                brows_GitRepoState_revert_sequence          = 3,
                brows_GitRepoState_cherrypick               = 4,
                brows_GitRepoState_cherrypick_sequence      = 5,
                brows_GitRepoState_bisect                   = 6,
                brows_GitRepoState_rebase                   = 7,
                brows_GitRepoState_rebase_interactive       = 8,
                brows_GitRepoState_rebase_merge             = 9,
                brows_GitRepoState_apply_mailbox            = 10,
                brows_GitRepoState_apply_mailbox_or_rebase  = 11
}               brows_GitRepoState;

typedef enum    brows_GitRepoComparison {
                brows_GitRepoComparison_unknown             = 0,
                brows_GitRepoComparison_same                = 1,
                brows_GitRepoComparison_diff                = 2
}               brows_GitRepoComparison;

#define     brows_git_ERROR_git_repository_open             2115001
#define     brows_git_ERROR_git_repository_config           2115002
#define     brows_git_ERROR_git_config_get_entry            2115003
#define     brows_git_ERROR_calloc                          2115004
#define     brows_git_ERROR_git_repository_head             2115005
#define     brows_git_ERROR_git_branch_upstream             2115006

brows_git   brows_ERROR                                     brows_git_init(void);
brows_git   brows_ERROR                                     brows_git_exit(void);

brows_git   brows_GitConfigEntry*                           brows_GitConfigEntry_create(const char* name, const char* value, const char* source_kind, const char* source_path, brows_GitConfigLevel, unsigned int include_depth);
brows_git   void                                            brows_GitConfigEntry_destroy            (brows_GitConfigEntry*);
brows_git   unsigned int                                    brows_GitConfigEntry_get_include_depth  (brows_GitConfigEntry*);
brows_git   brows_GitConfigLevel                            brows_GitConfigEntry_get_level          (brows_GitConfigEntry*);
brows_git   const char*                                     brows_GitConfigEntry_get_name           (brows_GitConfigEntry*);
brows_git   const char*                                     brows_GitConfigEntry_get_source_kind    (brows_GitConfigEntry*);
brows_git   const char*                                     brows_GitConfigEntry_get_source_path    (brows_GitConfigEntry*);
brows_git   const char*                                     brows_GitConfigEntry_get_value          (brows_GitConfigEntry*);

brows_git   brows_ERROR                                     brows_GitRef_compare_remote             (brows_GitRef*, brows_GitRepoComparison*);
brows_git   void                                            brows_GitRef_destroy                    (brows_GitRef*);
brows_git   const char*                                     brows_GitRef_get_name                   (brows_GitRef*);

brows_git   void                                            brows_GitRepo_destroy                   (brows_GitRepo*);
brows_git   brows_ERROR                                     brows_GitRepo_find                      (const char* path, int32_t search, brows_GitRepo** out);
brows_git   const char*                                     brows_GitRepo_get_path                  (brows_GitRepo*);
brows_git   brows_GitRepoState                              brows_GitRepo_get_state                 (brows_GitRepo*);
brows_git   brows_ERROR                                     brows_GitRepo_head                      (brows_GitRepo*, brows_GitRef**);
brows_git   brows_ERROR                                     brows_GitRepo_config_entry              (brows_GitRepo*, const char* name, brows_GitConfigEntry**);

#endif
