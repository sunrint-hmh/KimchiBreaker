#!/usr/bin/env python3
"""Unity 에디터를 실행하지 않고(라이선스 없이) 프로젝트 C# 코드를 컴파일 검사한다.

하는 일
  1. Packages/manifest.json 의 패키지를 에디터 내장본/레지스트리에서 받아 의존성까지 모은다.
  2. 모든 .asmdef 를 읽어 Unity 와 같은 방식(define, 참조, versionDefines)으로 csc 를 호출한다.
  3. Assets/ 아래 어셈블리의 오류와 경고를 출력한다. 오류가 있으면 종료 코드 1.

한계: 타입/문법 검사만 한다. IL 후처리(Netcode RPC 변환 등)와 실제 실행은 검증하지 않는다.

사용법
  python3 Tools/CompileCheck/compile_check.py --editor /opt/unity/6000.6.8f1/Editor
"""
import argparse
import hashlib
import io
import json
import os
import re
import subprocess
import sys
import tarfile
import urllib.request

REGISTRY = "https://packages.unity.com"


def log(msg):
    print(msg, flush=True)


# ---------------------------------------------------------------------------
# 버전 비교
# ---------------------------------------------------------------------------
def parse_version(text):
    """'2.13.3', '6000.6.8f1', '1.0.0-pre.2' → 비교 가능한 튜플."""
    nums = re.findall(r"\d+", text.split("-")[0].split("+")[0])
    core = tuple(int(n) for n in nums[:3])
    core = core + (0,) * (3 - len(core))
    is_release = 0 if "-" in text else 1
    return core + (is_release,)


def version_satisfies(version, expression):
    """Unity versionDefines 표현식 평가."""
    expression = (expression or "").strip()
    if not expression:
        return True
    v = parse_version(version)
    m = re.match(r"^([\[\(])\s*([^,\s]*)\s*,\s*([^\]\)\s]*)\s*([\]\)])$", expression)
    if m:
        lo_inc, lo, hi, hi_inc = m.group(1) == "[", m.group(2), m.group(3), m.group(4) == "]"
        if lo:
            lv = parse_version(lo)
            if v < lv or (v == lv and not lo_inc):
                return False
        if hi:
            hv = parse_version(hi)
            if v > hv or (v == hv and not hi_inc):
                return False
        return True
    m = re.match(r"^\[\s*([^\]\s]+)\s*\]$", expression)
    if m:
        return v == parse_version(m.group(1))
    return v >= parse_version(expression)


# ---------------------------------------------------------------------------
# 패키지 수집
# ---------------------------------------------------------------------------
class PackageSet:
    def __init__(self, editor, cache):
        self.editor = editor
        self.cache = cache
        pm = os.path.join(editor, "Data", "Resources", "PackageManager")
        self.builtin_dir = os.path.join(pm, "BuiltInPackages")
        self.bundled_dir = os.path.join(pm, "Editor")
        self.resolved = {}  # name -> (version, path)
        self.requested = {}  # name -> 요구된 최소 버전
        os.makedirs(cache, exist_ok=True)

    def _builtin(self, name):
        path = os.path.join(self.builtin_dir, name)
        manifest = os.path.join(path, "package.json")
        if os.path.isfile(manifest):
            with open(manifest, encoding="utf-8-sig") as f:
                return json.load(f)["version"], path
        return None

    def _extract(self, name, version, opener):
        dest = os.path.join(self.cache, f"{name}@{version}")
        if not os.path.isfile(os.path.join(dest, "package.json")):
            tmp = dest + ".partial"
            os.makedirs(tmp, exist_ok=True)
            with opener() as tar:
                for member in tar.getmembers():
                    parts = member.name.split("/", 1)
                    if len(parts) < 2 or not parts[1]:
                        continue
                    target = os.path.normpath(os.path.join(tmp, parts[1]))
                    if not target.startswith(tmp + os.sep):
                        continue  # 경로 탈출 방지
                    if member.isdir():
                        os.makedirs(target, exist_ok=True)
                    elif member.isfile():
                        os.makedirs(os.path.dirname(target), exist_ok=True)
                        with tar.extractfile(member) as src, open(target, "wb") as out:
                            out.write(src.read())
            os.rename(tmp, dest)
        return dest

    def _bundled(self, name, version):
        tgz = os.path.join(self.bundled_dir, f"{name}-{version}.tgz")
        if os.path.isfile(tgz):
            return self._extract(name, version, lambda: tarfile.open(tgz, "r:gz"))
        return None

    def _registry(self, name, version):
        with urllib.request.urlopen(f"{REGISTRY}/{name}", timeout=120) as r:
            meta = json.load(r)
        entry = meta.get("versions", {}).get(version)
        if entry is None:
            raise SystemExit(f"레지스트리에 {name}@{version} 이 없습니다.")
        dist = entry["dist"]
        with urllib.request.urlopen(dist["tarball"], timeout=600) as r:
            blob = r.read()
        if dist.get("shasum") and hashlib.sha1(blob).hexdigest() != dist["shasum"]:
            raise SystemExit(f"{name}@{version} 체크섬 불일치")
        return self._extract(name, version, lambda: tarfile.open(fileobj=io.BytesIO(blob), mode="r:gz"))

    def add(self, name, version):
        """패키지와 그 의존성을 재귀적으로 모은다. 같은 패키지는 더 높은 버전을 택한다."""
        if name.startswith("com.unity.modules.") or name.startswith("com.unity.feature."):
            return
        builtin = self._builtin(name)
        if builtin:
            version, path = builtin  # 내장 패키지는 에디터 버전으로 고정된다
        else:
            current = self.resolved.get(name)
            if current and parse_version(current[0]) >= parse_version(version):
                return
            path = self._bundled(name, version) or self._registry(name, version)
        if name in self.resolved and self.resolved[name][0] == version:
            return
        self.resolved[name] = (version, path)
        with open(os.path.join(path, "package.json"), encoding="utf-8-sig") as f:
            deps = json.load(f).get("dependencies") or {}
        for dep_name, dep_version in deps.items():
            self.add(dep_name, dep_version)


# ---------------------------------------------------------------------------
# asmdef 수집
# ---------------------------------------------------------------------------
class Assembly:
    def __init__(self, path, data, owner):
        self.path = path
        self.dir = os.path.dirname(path)
        self.data = data
        self.name = data["name"]
        self.owner = owner  # 'project' 또는 패키지 이름
        self.sources = []
        self.guid = read_guid(path + ".meta")
        self.out = None
        self.failed = False

    @property
    def editor_only(self):
        include = self.data.get("includePlatforms") or []
        return include == ["Editor"]

    @property
    def included_in_editor(self):
        include = self.data.get("includePlatforms") or []
        exclude = self.data.get("excludePlatforms") or []
        if include:
            return "Editor" in include
        return "Editor" not in exclude


def read_guid(meta_path):
    try:
        with open(meta_path, encoding="utf-8") as f:
            for line in f:
                if line.startswith("guid:"):
                    return line.split(":", 1)[1].strip()
    except OSError:
        pass
    return None


def load_json_lenient(path):
    with open(path, encoding="utf-8-sig") as f:
        text = f.read()
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        text = re.sub(r",(\s*[\]}])", r"\1", text)  # 끝에 붙은 쉼표 허용
        return json.loads(text)


def skip_dir(name):
    return name.endswith("~") or name.startswith(".")


def discover(root, owner, assemblies, asmrefs):
    for current, dirs, files in os.walk(root):
        dirs[:] = sorted(d for d in dirs if not skip_dir(d))
        for name in files:
            full = os.path.join(current, name)
            if name.endswith(".asmdef"):
                assemblies.append(Assembly(full, load_json_lenient(full), owner))
            elif name.endswith(".asmref"):
                asmrefs.append((current, load_json_lenient(full).get("reference", "")))


def assign_sources(roots, assemblies, asmrefs):
    by_dir = {a.dir: a for a in assemblies}
    by_name = {a.name: a for a in assemblies}
    by_guid = {a.guid: a for a in assemblies if a.guid}
    ref_dirs = {}
    for directory, reference in asmrefs:
        target = by_guid.get(reference[5:]) if reference.startswith("GUID:") else by_name.get(reference)
        if target:
            ref_dirs[directory] = target
    orphans = []
    for root in roots:
        for current, dirs, files in os.walk(root):
            dirs[:] = sorted(d for d in dirs if not skip_dir(d))
            owner = None
            probe = current
            while True:
                if probe in by_dir:
                    owner = by_dir[probe]
                    break
                if probe in ref_dirs:
                    owner = ref_dirs[probe]
                    break
                if probe == root or len(probe) <= len(root):
                    break
                probe = os.path.dirname(probe)
            for name in sorted(files):
                if name.endswith(".cs"):
                    if owner:
                        owner.sources.append(os.path.join(current, name))
                    else:
                        orphans.append(os.path.join(current, name))
    return orphans


# ---------------------------------------------------------------------------
# define 계산
# ---------------------------------------------------------------------------
def base_defines(unity_version):
    major, minor, patch = parse_version(unity_version)[:3]
    defines = {
        f"UNITY_{major}_{minor}_{patch}", f"UNITY_{major}_{minor}", f"UNITY_{major}",
        "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_LINUX", "UNITY_STANDALONE",
        "UNITY_STANDALONE_LINUX", "UNITY_64", "PLATFORM_STANDALONE", "PLATFORM_STANDALONE_LINUX",
        "ENABLE_MONO", "NET_STANDARD", "NET_STANDARD_2_0", "NET_STANDARD_2_1", "NETSTANDARD", "NETSTANDARD2_1",
        "CSHARP_7_OR_LATER", "CSHARP_7_3_OR_NEWER", "DEBUG", "TRACE", "UNITY_ASSERTIONS",
        "ENABLE_INPUT_SYSTEM", "UNITY_INCLUDE_TESTS", "ENABLE_UNITY_COLLECTIONS_CHECKS",
        "ENABLE_AUDIO", "ENABLE_PHYSICS", "ENABLE_UNITYWEBREQUEST", "ENABLE_ANIMATION",
        "ENABLE_UNET", "ENABLE_BURST_AOT", "ENABLE_PROFILER", "ENABLE_MANAGED_JOBS",
        "ENABLE_MANAGED_TRANSFORM_JOBS", "ENABLE_MANAGED_ANIMATION_JOBS", "ENABLE_MANAGED_AUDIO_JOBS",
        "ENABLE_UNITY_CONSENT", "ENABLE_ENGINE_CODE_STRIPPING", "ENABLE_TEXTURE_STREAMING",
        "ENABLE_VIDEO", "ENABLE_DIRECTOR", "ENABLE_TERRAIN", "ENABLE_AI", "ENABLE_CLOTH",
        "ENABLE_TILEMAP", "ENABLE_PHYSICS_2D", "UNITY_UGP_API", "ENABLE_UIELEMENTS", "ENABLE_UGUI",
    }
    timeline = [(5, m) for m in range(3, 7)]
    for year, last in ((2017, 4), (2018, 4), (2019, 4), (2020, 3), (2021, 3), (2022, 3), (2023, 3)):
        timeline += [(year, m) for m in range(1, last + 1)]
    timeline += [(6000, m) for m in range(0, minor + 1)]
    for a, b in timeline:
        if (a, b) <= (major, minor):
            defines.add(f"UNITY_{a}_{b}_OR_NEWER")
    return defines


def constraint_ok(expression, defines):
    for alternative in expression.split("||"):
        ok = True
        for term in alternative.split("&&"):
            term = term.strip()
            if not term:
                continue
            if term.startswith("!"):
                ok = ok and term[1:].strip() not in defines
            else:
                ok = ok and term in defines
        if ok:
            return True
    return False


# ---------------------------------------------------------------------------
# 컴파일
# ---------------------------------------------------------------------------
def find_csc(editor):
    sdk_root = os.path.join(editor, "Data", "DotNetSdk")
    sdk_dir = os.path.join(sdk_root, "sdk")
    for version in sorted(os.listdir(sdk_dir), reverse=True):
        csc = os.path.join(sdk_dir, version, "Roslyn", "bincore", "csc.dll")
        if os.path.isfile(csc):
            return [os.path.join(sdk_root, "dotnet"), csc]
    raise SystemExit("에디터에서 C# 컴파일러(csc)를 찾지 못했습니다.")


def dlls_in(directory):
    if not os.path.isdir(directory):
        return []
    return sorted(os.path.join(directory, f) for f in os.listdir(directory) if f.endswith(".dll"))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--editor", required=True, help="Unity 에디터 폴더 (Unity 실행 파일이 있는 Editor 폴더)")
    parser.add_argument("--project", default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))
    parser.add_argument("--cache", default=os.path.expanduser("~/.cache/kimchibreaker-compile-check"))
    parser.add_argument("--langversion", default="9.0")
    parser.add_argument("--show-package-warnings", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    project = os.path.abspath(args.project)
    editor = os.path.abspath(args.editor)
    data = os.path.join(editor, "Data")
    with open(os.path.join(project, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as f:
        unity_version = re.search(r"m_EditorVersion:\s*(\S+)", f.read()).group(1)

    packages = PackageSet(editor, os.path.join(args.cache, "packages"))
    with open(os.path.join(project, "Packages", "manifest.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    for name, version in manifest["dependencies"].items():
        packages.add(name, version)
    log(f"패키지 {len(packages.resolved)}개 확보")
    if args.verbose:
        for name, (version, _) in sorted(packages.resolved.items()):
            log(f"  {name}@{version}")

    assemblies, asmrefs = [], []
    roots = [os.path.join(project, "Assets")]
    discover(roots[0], "project", assemblies, asmrefs)
    for name, (_, path) in sorted(packages.resolved.items()):
        discover(path, name, assemblies, asmrefs)
        roots.append(path)
    orphans = assign_sources(roots, assemblies, asmrefs)
    project_orphans = [p for p in orphans if p.startswith(roots[0] + os.sep)]

    by_name = {}
    for assembly in assemblies:
        by_name.setdefault(assembly.name, assembly)
    # Unity 가 내부적으로 쓰는 별칭: "Unity.ugui" 는 런타임/에디터 UI 어셈블리 둘 다를 뜻한다.
    aliases = {"Unity.ugui": ["UnityEngine.UI", "UnityEditor.UI"]}
    by_guid = {a.guid: a for a in assemblies if a.guid}

    # asmdef 에 속하지 않은 Assets 스크립트는 Unity 의 Assembly-CSharp(-Editor) 로 들어간다.
    def is_editor_path(path):
        return "/Editor/" in path.replace(os.sep, "/")

    predefined = []
    for label, sources, editor_flag in (
        ("Assembly-CSharp", [p for p in project_orphans if not is_editor_path(p)], False),
        ("Assembly-CSharp-Editor", [p for p in project_orphans if is_editor_path(p)], True),
    ):
        if sources:
            fake = Assembly(os.path.join(project, label + ".asmdef"), {"name": label, "autoReferenced": False}, "project")
            fake.sources = sources
            fake.predefined = True
            fake.predefined_editor = editor_flag
            predefined.append(fake)

    defines_base = base_defines(unity_version)
    package_versions = {name: version for name, (version, _) in packages.resolved.items()}
    package_versions["Unity"] = unity_version

    def resolve_refs(assembly):
        result = []
        for ref in assembly.data.get("references") or []:
            if ref in aliases:
                targets = [by_name.get(alias) for alias in aliases[ref]]
            else:
                targets = [by_guid.get(ref[5:]) if ref.startswith("GUID:") else by_name.get(ref)]
            for target in targets:
                if target and target is not assembly:
                    result.append(target)
        return result

    def defines_for(assembly):
        defines = set(defines_base)
        for entry in assembly.data.get("versionDefines") or []:
            resource = entry.get("name", "")
            version = package_versions.get(resource)
            if version is None and resource.startswith("com.unity.modules."):
                version = "1.0.0"  # 엔진 내장 모듈은 항상 존재한다
            if version and version_satisfies(version, entry.get("expression", "")):
                defines.add(entry["define"])
        return defines

    out_dir = os.path.join(args.cache, "out")
    os.makedirs(out_dir, exist_ok=True)
    csc = find_csc(editor)

    engine_refs = dlls_in(os.path.join(data, "Managed", "UnityEngine"))
    bcl_refs = (
        [os.path.join(data, "NetStandard", "ref", "2.1.0", "netstandard.dll")]
        + dlls_in(os.path.join(data, "NetStandard", "compat", "2.1.0", "shims", "netstandard"))
        + dlls_in(os.path.join(data, "NetStandard", "compat", "2.1.0", "shims", "netfx"))
        + dlls_in(os.path.join(data, "NetStandard", "Extensions", "2.0.0"))
    )
    editor_bcl_refs = dlls_in(os.path.join(data, "NetStandard", "EditorExtensions"))
    analyzers = dlls_in(os.path.join(data, "Tools", "BuildPipeline", "Unity.SourceGenerators"))

    # 패키지에 들어 있는 미리 컴파일된 DLL (Newtonsoft.Json, nunit 등)
    precompiled = {}
    for _, (_, path) in packages.resolved.items():
        for current, dirs, files in os.walk(path):
            dirs[:] = [d for d in dirs if not skip_dir(d)]
            for name in files:
                if name.endswith(".dll"):
                    precompiled.setdefault(name, os.path.join(current, name))

    state = {}
    order = []

    def visit(assembly, stack):
        mark = state.get(assembly.name)
        if mark == "done":
            return
        if mark == "visiting":
            return  # 순환 참조는 무시
        state[assembly.name] = "visiting"
        for dep in resolve_refs(assembly):
            visit(dep, stack + [assembly.name])
        state[assembly.name] = "done"
        order.append(assembly)

    wanted = [a for a in assemblies if a.owner == "project"]
    for assembly in wanted:
        visit(assembly, [])
    auto_referenced = []
    if predefined:
        # Assembly-CSharp 는 autoReferenced 인 모든 어셈블리를 참조한다.
        for assembly in assemblies:
            if assembly.data.get("autoReferenced", True) and assembly.included_in_editor:
                visit(assembly, [])
                auto_referenced.append(assembly)
        order.extend(predefined)

    error_count = 0
    warning_count = 0
    failed_packages = []

    def compile_one(assembly):
        nonlocal error_count, warning_count
        is_project = assembly.owner == "project"
        defines = defines_for(assembly)
        for constraint in assembly.data.get("defineConstraints") or []:
            if not constraint_ok(constraint, defines):
                assembly.failed = True  # 조건 불충족 → Unity 도 컴파일하지 않는다
                return
        if not assembly.included_in_editor or not assembly.sources:
            assembly.failed = True
            return

        refs = []
        if getattr(assembly, "predefined", False):
            deps = [a for a in auto_referenced if not a.failed and a.out]
            if not assembly.predefined_editor:
                deps = [a for a in deps if not a.editor_only]
            else:
                deps += [p for p in predefined if p is not assembly and p.out]
        else:
            deps = []
            seen = set()
            pending = resolve_refs(assembly)
            while pending:  # 참조의 참조까지 넘겨야 타입 전달 오류가 나지 않는다
                dep = pending.pop()
                if dep.name in seen:
                    continue
                seen.add(dep.name)
                deps.append(dep)
                pending.extend(resolve_refs(dep))
        for dep in deps:
            if dep.out and not dep.failed:
                refs.append(dep.out)
        if not assembly.data.get("noEngineReferences", False):
            refs += engine_refs
        refs += bcl_refs
        if assembly.editor_only or getattr(assembly, "predefined_editor", False):
            refs += editor_bcl_refs
        wanted_dlls = list(assembly.data.get("precompiledReferences") or [])
        if not assembly.data.get("overrideReferences", False):
            wanted_dlls += ["nunit.framework.dll"] if "TestAssemblies" in (assembly.data.get("optionalUnityReferences") or []) else []
        for dll in wanted_dlls:
            if dll in precompiled:
                refs.append(precompiled[dll])

        assembly.out = os.path.join(out_dir, assembly.name + ".dll")
        rsp = os.path.join(out_dir, assembly.name + ".rsp")
        with open(rsp, "w", encoding="utf-8") as f:
            f.write("-target:library\n-nologo\n-nostdlib+\n-deterministic\n-debug-\n-optimize-\n")
            f.write("-nowarn:CS1701,CS1702,CS0169,CS0649,CS0282\n")
            f.write(f"-langversion:{args.langversion if is_project else 'preview'}\n")
            f.write("-refonly\n" if not is_project else "")
            if assembly.data.get("allowUnsafeCode", False):
                f.write("-unsafe+\n")
            f.write(f'-out:"{assembly.out}"\n')
            f.write("-define:" + ";".join(sorted(defines)) + "\n")
            for ref in dict.fromkeys(refs):
                f.write(f'-r:"{ref}"\n')
            for analyzer in analyzers:
                f.write(f'-analyzer:"{analyzer}"\n')
            for source in assembly.sources:
                f.write(f'"{source}"\n')
        result = subprocess.run(csc + ["-noconfig", "@" + rsp], capture_output=True, text=True)
        output = (result.stdout + result.stderr).strip()
        lines = [line for line in output.splitlines() if line.strip()]
        errors = [line for line in lines if re.search(r"\berror\b", line)]
        warnings = [line for line in lines if re.search(r"\bwarning\b", line)]
        if result.returncode != 0:
            assembly.failed = True
            if os.path.exists(assembly.out):
                os.remove(assembly.out)
        if is_project:
            error_count += len(errors)
            warning_count += len(warnings)
            status = "실패" if result.returncode != 0 else "성공"
            log(f"[{status}] {assembly.name} (소스 {len(assembly.sources)}개, 오류 {len(errors)}, 경고 {len(warnings)})")
            for line in errors + warnings:
                log("    " + line.replace(project + os.sep, ""))
        else:
            if result.returncode != 0:
                failed_packages.append((assembly, errors))
            elif args.show_package_warnings and warnings:
                log(f"[패키지 경고] {assembly.name}: {len(warnings)}개")
            if args.verbose:
                log(f"  패키지 어셈블리 {assembly.name}: {'실패' if result.returncode != 0 else '성공'}")

    for assembly in order:
        compile_one(assembly)

    if failed_packages:
        log(f"\n참고: 패키지 어셈블리 {len(failed_packages)}개가 이 도구에서 컴파일되지 않았습니다 (검사 도구의 한계일 수 있음).")
        for assembly, errors in failed_packages:
            log(f"  - {assembly.name} ({assembly.owner}): 오류 {len(errors)}개")
            if args.verbose:
                for line in errors[:15]:
                    log("      " + line)

    log(f"\n프로젝트 코드: 오류 {error_count}개, 경고 {warning_count}개")
    return 1 if error_count else 0


if __name__ == "__main__":
    sys.exit(main())
