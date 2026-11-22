// Minimal GDExtension (Godot 4.7) helper: talks to the engine through the C interface by name only -
// Variant calls, properties and singletons. No godot-cpp, no method hashes.
#pragma once
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include <initializer_list>

namespace gd
{
using GetProc = void* (*)(const char*);
using Int = int64_t;
using Bool = uint8_t;

enum Type : int { NIL = 0, BOOL = 1, INT = 2, FLOAT = 3, STRING = 4, VECTOR2 = 5, VECTOR3 = 9, STRING_NAME = 21, NODE_PATH = 22, OBJECT = 24, CALLABLE = 25, DICTIONARY = 27, ARRAY = 28 };

struct CallError { int error; int32_t argument; int32_t expected; };
struct alignas(8) Variant { uint8_t data[24] = {}; };
struct alignas(8) StrName { void* p = nullptr; };
struct alignas(8) Str { void* p = nullptr; };

struct Init { int minimum_level; void* userdata; void (*initialize)(void*, int); void (*deinitialize)(void*, int); };
struct MainLoopCallbacks { void (*startup)(); void (*shutdown)(); void (*frame)(); };

using FromType = void (*)(void* r_variant, void* p_value);
using ToType = void (*)(void* r_value, void* p_variant);
using Destructor = void (*)(void* p_base);

struct Api
{
    void (*variant_call)(void*, const void*, const void* const*, Int, void*, CallError*);
    void (*variant_get_named)(const void*, const void*, void*, Bool*);
    void (*variant_set_named)(void*, const void*, const void*, Bool*);
    void (*variant_get_indexed)(const void*, Int, void*, Bool*, Bool*);
    void (*variant_stringify)(const void*, void*);
    void (*variant_destroy)(void*);
    void (*variant_new_copy)(void*, const void*);
    void (*variant_new_nil)(void*);
    int (*variant_get_type)(const void*);
    FromType (*from_type)(int);
    ToType (*to_type)(int);
    Destructor (*ptr_destructor)(int);
    void (*string_new_utf8)(void*, const char*);
    Int (*string_to_utf8)(const void*, char*, Int);
    void (*string_name_new_utf8)(void*, const char*);
    void* (*global_get_singleton)(const void*);
    void (*register_main_loop_callbacks)(void*, const MainLoopCallbacks*);
    void (*variant_construct)(int, void*, const void* const*, int32_t, CallError*);
};
inline Api api;

inline bool Load(GetProc gp)
{
#define G(field, name) api.field = (decltype(api.field))gp(name); if (!api.field) return false;
    G(variant_call, "variant_call") G(variant_get_named, "variant_get_named") G(variant_set_named, "variant_set_named")
    G(variant_get_indexed, "variant_get_indexed") G(variant_stringify, "variant_stringify") G(variant_destroy, "variant_destroy")
    G(variant_new_copy, "variant_new_copy") G(variant_new_nil, "variant_new_nil") G(variant_get_type, "variant_get_type")
    G(from_type, "get_variant_from_type_constructor") G(to_type, "get_variant_to_type_constructor") G(ptr_destructor, "variant_get_ptr_destructor")
    G(string_new_utf8, "string_new_with_utf8_chars") G(string_to_utf8, "string_to_utf8_chars")
    G(string_name_new_utf8, "string_name_new_with_utf8_chars") G(global_get_singleton, "global_get_singleton")
    G(register_main_loop_callbacks, "register_main_loop_callbacks")
    G(variant_construct, "variant_construct")
#undef G
    return true;
}

// StringNames are interned by the engine; we keep ours alive forever (they are method / property names).
inline const StrName* SN(const char* s)
{
    struct Entry { std::string k; StrName* v; };
    static std::vector<Entry> cache;
    for (auto& e : cache) if (e.k == s) return e.v;
    StrName* n = new StrName;
    api.string_name_new_utf8(n, s);
    cache.push_back({ s, n });
    return n;
}

// owning Variant
struct V
{
    Variant v;
    V() { api.variant_new_nil(&v); }
    V(const V& o) { api.variant_new_copy(&v, &o.v); }
    V& operator=(const V& o) { if (this != &o) { api.variant_destroy(&v); api.variant_new_copy(&v, &o.v); } return *this; }
    ~V() { api.variant_destroy(&v); }
    int type() const { return api.variant_get_type(&v); }
    bool nil() const { return type() == NIL; }
};

inline V FromInt(int64_t i) { V r; api.variant_destroy(&r.v); api.from_type(INT)(&r.v, &i); return r; }
inline V FromFloat(double d) { V r; api.variant_destroy(&r.v); api.from_type(FLOAT)(&r.v, &d); return r; }
inline V FromBool(bool b) { V r; Bool x = b; api.variant_destroy(&r.v); api.from_type(BOOL)(&r.v, &x); return r; }
inline V FromObj(void* o) { V r; api.variant_destroy(&r.v); api.from_type(OBJECT)(&r.v, &o); return r; }
inline V FromStr(const char* s)
{
    Str str; api.string_new_utf8(&str, s);
    V r; api.variant_destroy(&r.v); api.from_type(STRING)(&r.v, &str);
    api.ptr_destructor(STRING)(&str);
    return r;
}
inline V FromName(const char* s) { V r; api.variant_destroy(&r.v); api.from_type(STRING_NAME)(&r.v, (void*)SN(s)); return r; }
inline V FromVec3(float x, float y, float z) { float f[3] = { x, y, z }; V r; api.variant_destroy(&r.v); api.from_type(VECTOR3)(&r.v, f); return r; }

inline V NewOf(int type) { V r; api.variant_destroy(&r.v); CallError e{}; api.variant_construct(type, &r.v, nullptr, 0, &e); return r; }

inline int64_t ToInt(const V& v) { int64_t i = 0; api.to_type(INT)(&i, (void*)&v.v); return i; }
inline double ToFloat(const V& v) { double d = 0; api.to_type(FLOAT)(&d, (void*)&v.v); return d; }
inline bool ToBool(const V& v) { Bool b = 0; api.to_type(BOOL)(&b, (void*)&v.v); return b != 0; }
inline void* ToObj(const V& v) { void* o = nullptr; if (v.type() == OBJECT) api.to_type(OBJECT)(&o, (void*)&v.v); return o; }
inline void ToVec3(const V& v, float out[3]) { out[0] = out[1] = out[2] = 0; if (v.type() == VECTOR3) api.to_type(VECTOR3)(out, (void*)&v.v); }
inline double Num(const V& v) { return v.type() == INT ? (double)ToInt(v) : v.type() == FLOAT ? ToFloat(v) : v.type() == BOOL ? ToBool(v) : 0.0; }

inline std::string Text(const V& v)
{
    Str s; api.variant_stringify(&v.v, &s);
    Int n = api.string_to_utf8(&s, nullptr, 0);
    std::string out((size_t)(n > 0 ? n : 0), '\0');
    if (n > 0) api.string_to_utf8(&s, out.data(), n);
    api.ptr_destructor(STRING)(&s);
    return out;
}

inline V Call(const V& self, const char* method, std::initializer_list<const V*> args = {}, bool* ok = nullptr)
{
    const void* a[8] = {};
    int n = 0;
    for (const V* p : args) if (n < 8) a[n++] = &p->v;
    V r; api.variant_destroy(&r.v);
    CallError e{};
    api.variant_call((void*)&self.v, SN(method), a, n, &r.v, &e);
    if (ok) *ok = e.error == 0;
    if (e.error != 0) { api.variant_new_nil(&r.v); }
    return r;
}

inline V Get(const V& self, const char* name, bool* ok = nullptr)
{
    V r; api.variant_destroy(&r.v);
    Bool valid = 0;
    api.variant_get_named(&self.v, SN(name), &r.v, &valid);
    if (ok) *ok = valid != 0;
    if (!valid) api.variant_new_nil(&r.v);
    return r;
}

inline bool Set(V& self, const char* name, const V& value)
{
    Bool valid = 0;
    api.variant_set_named(&self.v, SN(name), &value.v, &valid);
    return valid != 0;
}

inline V Index(const V& arr, int64_t i)
{
    V r; api.variant_destroy(&r.v);
    Bool valid = 0, oob = 0;
    api.variant_get_indexed(&arr.v, i, &r.v, &valid, &oob);
    if (!valid || oob) api.variant_new_nil(&r.v);
    return r;
}

inline int64_t Size(const V& arr) { return ToInt(Call(arr, "size")); }

inline V Singleton(const char* name) { void* o = api.global_get_singleton(SN(name)); return o ? FromObj(o) : V(); }
}
