"""Fixes a Dumper-7 bug in the generated SDK.

For Blueprint functions Dumper-7 writes the *class* name into the lookup
(GetStaticName(L"BP_Foo_C", FnName)) instead of the *function* name, so the
game can't find the function and stops with a fatal error.
This rewrites every lookup to use the name from the '// Function Pkg.Class.Name' comment.

usage: python fix_sdk.py <CppSDK folder>
"""
import os, re, sys

sdk = os.path.join(sys.argv[1], "SDK")
comment = re.compile(r"^// Function [^.\s]+\.[^.\s]+\.(.+?)\s*$")
lookup = re.compile(r'GetStaticName\(L"[^"]*", FnName\)')
fixed_files = fixed_calls = 0
for name in os.listdir(sdk):
    if not name.endswith("_functions.cpp"):
        continue
    path = os.path.join(sdk, name)
    with open(path, encoding="utf-8-sig") as f:
        lines = f.readlines()
    current, changed = None, 0
    for i, line in enumerate(lines):
        m = comment.match(line)
        if m:
            current = m.group(1)
            continue
        if current and lookup.search(line):
            new = lookup.sub('GetStaticName(L"%s", FnName)' % current, line)
            if new != line:
                lines[i] = new
                changed += 1
            current = None
    if changed:
        with open(path, "w", encoding="utf-8-sig") as f:
            f.writelines(lines)
        fixed_files += 1
        fixed_calls += changed
print("fix_sdk: fixed %d lookups in %d files" % (fixed_calls, fixed_files))

# A member named exactly like its type (e.g. "ENUM_Ores ENUM_Ores;") hides the type inside
# the class and breaks compiling. Rename such members to <name>_0 (layout stays the same).
same_name = re.compile(r"^(\s+)(\w+)(\s+)\2;", re.M)
renamed = 0
renamed_names = set()
for name in os.listdir(sdk):
    if not name.endswith("_classes.hpp"):
        continue
    path = os.path.join(sdk, name)
    with open(path, encoding="utf-8-sig") as f:
        text = f.read()
    names = set(m.group(2) for m in same_name.finditer(text))
    if not names:
        continue
    text = same_name.sub(lambda m: "%s%s%s%s_0;" % (m.group(1), m.group(2), m.group(3), m.group(2)), text)
    renamed_names.update(names)
    renamed += len(names)
    with open(path, "w", encoding="utf-8-sig") as f:
        f.write(text)

# the offset checks in Assertions.inl name the member too
asserts = os.path.join(sys.argv[1], "Assertions.inl")
if renamed_names and os.path.exists(asserts):
    with open(asserts, encoding="utf-8-sig") as f:
        text = f.read()
    for n in renamed_names:
        text = re.sub(r"(offsetof\(\w+, )%s\)" % n, r"\g<1>%s_0)" % n, text)
    with open(asserts, "w", encoding="utf-8-sig") as f:
        f.write(text)
if renamed:
    print("fix_sdk: renamed %d members that hid their type" % renamed)
