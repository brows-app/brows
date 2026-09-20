#include "brows_git_internal.h"

struct brows_GitConfigEntry {
    const char* name;
    const char* value;
    const char* source_kind;
    const char* source_path;
    unsigned int include_depth;
    brows_GitConfigLevel level;
};

static brows_GitConfigEntry* brows_GitConfigEntry_init(brows_GitConfigEntry* p, const char* name, const char* value, const char* source_kind, const char* source_path, brows_GitConfigLevel level, unsigned int include_depth) {
    assert(p);
    p->name = name;
    p->value = value;
    p->source_kind = source_kind;
    p->source_path = source_path;
    p->include_depth = include_depth;
    p->level = level;
    return p;
}

brows_GitConfigEntry* brows_GitConfigEntry_create(const char* name, const char* value, const char* source_kind, const char* source_path, brows_GitConfigLevel level, unsigned int include_depth) {
    brows_GitConfigEntry* p = calloc(1, sizeof(brows_GitConfigEntry));
    if (p) {
        return brows_GitConfigEntry_init(p, name, value, source_kind, source_path, level, include_depth);
    }
    return NULL;
}

void brows_GitConfigEntry_destroy(brows_GitConfigEntry* p) {
    free(p);
}

const char* brows_GitConfigEntry_get_name(brows_GitConfigEntry* p) {
    assert(p);
    return p->name;
}

const char* brows_GitConfigEntry_get_value(brows_GitConfigEntry* p) {
    assert(p);
    return p->value;
}

const char* brows_GitConfigEntry_get_source_kind(brows_GitConfigEntry* p) {
    assert(p);
    return p->source_kind;
}

const char* brows_GitConfigEntry_get_source_path(brows_GitConfigEntry* p) {
    assert(p);
    return p->source_path;
}

brows_GitConfigLevel brows_GitConfigEntry_get_level(brows_GitConfigEntry* p) {
    assert(p);
    return p->level;
}

unsigned int brows_GitConfigEntry_get_include_depth(brows_GitConfigEntry* p) {
    assert(p);
    return p->include_depth;
}
