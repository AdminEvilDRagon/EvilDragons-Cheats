// Tiny IL2CPP runtime helper: looks classes, methods and fields up by name through the exports of
// GameAssembly.dll (they stay the same between Unity versions, unlike the internal structs).
#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstdint>
#include <cmath>
#include <string>
#include <vector>

namespace il2
{
struct Class; struct Method; struct Field; struct Object; struct Image; struct Domain; struct Assembly; struct Type;
struct String { void* klass; void* monitor; int32_t length; wchar_t chars[1]; };
struct Array { void* klass; void* monitor; void* bounds; size_t length; void* items[1]; };

#define IL2_API(ret, name, args) inline ret(*name) args = nullptr;
IL2_API(Domain*, domain_get, ())
IL2_API(Assembly**, domain_get_assemblies, (Domain*, size_t*))
IL2_API(Image*, assembly_get_image, (Assembly*))
IL2_API(const char*, image_get_name, (Image*))
IL2_API(Class*, class_from_name, (Image*, const char*, const char*))
IL2_API(Method*, class_get_method_from_name, (Class*, const char*, int))
IL2_API(Field*, class_get_field_from_name, (Class*, const char*))
IL2_API(size_t, field_get_offset, (Field*))
IL2_API(void, field_static_get_value, (Field*, void*))
IL2_API(void, field_static_set_value, (Field*, void*))
IL2_API(Object*, runtime_invoke, (Method*, void*, void**, Object**))
IL2_API(String*, string_new, (const char*))
IL2_API(void*, thread_attach, (Domain*))
IL2_API(void*, object_unbox, (Object*))
IL2_API(Type*, class_get_type, (Class*))
IL2_API(Object*, type_get_object, (Type*))
IL2_API(Class*, object_get_class, (Object*))
IL2_API(Class*, class_get_parent, (Class*))
#undef IL2_API

inline bool Load()
{
    HMODULE ga = GetModuleHandleW(L"GameAssembly.dll");
    if (!ga) return false;
#define L(n) n = (decltype(n))GetProcAddress(ga, "il2cpp_" #n); if (!n) return false;
    L(domain_get) L(domain_get_assemblies) L(assembly_get_image) L(image_get_name) L(class_from_name)
    L(class_get_method_from_name) L(class_get_field_from_name) L(field_get_offset) L(field_static_get_value)
    L(field_static_set_value) L(runtime_invoke) L(string_new) L(thread_attach) L(object_unbox)
    L(class_get_type) L(type_get_object) L(object_get_class) L(class_get_parent)
#undef L
    return true;
}

inline Image* FindImage(const char* name)
{
    size_t n = 0;
    Assembly** a = domain_get_assemblies(domain_get(), &n);
    for (size_t i = 0; i < n; i++)
    {
        Image* img = assembly_get_image(a[i]);
        if (img && _stricmp(image_get_name(img), name) == 0) return img;
    }
    return nullptr;
}

// walks up the parents, so inherited methods (Component.get_transform ...) are found too
inline Method* FindMethod(Class* c, const char* name, int args)
{
    for (; c; c = class_get_parent(c))
        if (Method* m = class_get_method_from_name(c, name, args)) return m;
    return nullptr;
}

inline void* MethodPtr(Method* m) { return m ? *(void**)m : nullptr; }   // MethodInfo::methodPointer is the first field

inline std::string Utf8(const String* s)
{
    if (!s || s->length <= 0) return {};
    int len = WideCharToMultiByte(CP_UTF8, 0, s->chars, s->length, nullptr, 0, nullptr, nullptr);
    std::string out(len, '\0');
    WideCharToMultiByte(CP_UTF8, 0, s->chars, s->length, out.data(), len, nullptr, nullptr);
    return out;
}

template <class T> inline T& At(void* obj, size_t off) { return *(T*)((char*)obj + off); }

// System.Decimal: flags (sign + scale), hi, lo64
struct Decimal { int32_t flags = 0; int32_t hi = 0; uint64_t lo = 0; };
inline Decimal Dec(double v)
{
    Decimal d;
    if (v < 0) { d.flags = (int32_t)0x80000000; v = -v; }
    d.lo = (uint64_t)llround(v);
    return d;
}
inline double ToDouble(const Decimal& d)
{
    double v = (double)d.lo + (double)(uint32_t)d.hi * 18446744073709551616.0;
    v /= std::pow(10.0, (d.flags >> 16) & 0xFF);
    return d.flags < 0 ? -v : v;
}

struct Vec3 { float x, y, z; };
inline Vec3 operator+(Vec3 a, Vec3 b) { return { a.x + b.x, a.y + b.y, a.z + b.z }; }
inline Vec3 operator*(Vec3 a, float s) { return { a.x * s, a.y * s, a.z * s }; }
inline float Dist(Vec3 a, Vec3 b) { float x = a.x - b.x, y = a.y - b.y, z = a.z - b.z; return sqrtf(x * x + y * y + z * z); }
}
