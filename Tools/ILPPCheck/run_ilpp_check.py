#!/usr/bin/env python3
"""Netcode IL 후처리(RPC/NetworkVariable 코드 생성)를 Unity 에디터 없이 실행해 오류를 찾는다.

1. CompileCheck 로 패키지와 프로젝트를 실행 가능한 DLL 로 빌드한다(--full).
2. Netcode 의 코드 생성기(Unity.Netcode.Editor.CodeGen)를 함께 빌드한다.
3. 코드 생성기를 프로젝트 런타임 어셈블리에 실행하고 진단을 출력한다. 오류가 있으면 종료 코드 1.

사용법
  python3 Tools/ILPPCheck/run_ilpp_check.py --editor /opt/unity/6000.6.8f1/Editor
"""
import argparse
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
TARGETS = ["KimchiBreaker.Runtime"]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--editor", required=True)
    parser.add_argument("--cache", default=os.path.expanduser("~/.cache/kimchibreaker-ilpp-check"))
    args = parser.parse_args()

    compile_check = os.path.join(PROJECT, "Tools", "CompileCheck", "compile_check.py")
    build = subprocess.run(
        [sys.executable, compile_check, "--editor", args.editor, "--full",
         "--extra", "Unity.Netcode.Editor.CodeGen", "--cache", args.cache],
        capture_output=True, text=True)
    print(build.stdout.strip())
    if build.returncode != 0:
        print(build.stderr.strip())
        print("빌드 실패로 IL 후처리 검사를 하지 못했습니다.")
        return 1

    out_dir = os.path.join(args.cache, "out")
    codegen = os.path.join(out_dir, "Unity.Netcode.Editor.CodeGen.dll")
    if not os.path.isfile(codegen):
        print("Netcode 코드 생성기 빌드에 실패했습니다(위 '참고' 항목 확인).")
        return 1

    managed = os.path.join(args.editor, "Data", "Managed")
    harness_dir = os.path.join(args.cache, "harness")
    restore = subprocess.run(
        ["dotnet", "build", os.path.join(HERE, "ILPPCheck.csproj"), "-c", "Release", "-o", harness_dir,
         "-p:UnityManagedDir=" + managed, "-nologo", "-v:q"],
        capture_output=True, text=True)
    if restore.returncode != 0:
        print(restore.stdout[-4000:])
        print("검사 프로그램 빌드 실패")
        return 1

    exit_code = 0
    for target in TARGETS:
        rsp_path = os.path.join(out_dir, target + ".rsp")
        refs, defines = [], []
        with open(rsp_path, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line.startswith("-r:"):
                    refs.append(line[3:].strip('"'))
                elif line.startswith("-define:"):
                    defines.extend(d for d in line[len("-define:"):].split(";") if d)
        # 코드 생성기가 런타임에 불러오는 어셈블리(Cecil 등)도 찾을 수 있게 한다.
        cecil = [r for r in refs if os.path.basename(r).startswith("Mono.Cecil")]
        extra_dirs = {os.path.dirname(c) for c in cecil}
        request = os.path.join(args.cache, target + ".ilpp.txt")
        with open(request, "w", encoding="utf-8") as f:
            f.write(f"processor={codegen}\n")
            f.write(f"target={os.path.join(out_dir, target + '.dll')}\n")
            f.write(f"output={os.path.join(args.cache, target + '.processed.dll')}\n")
            for ref in refs:
                f.write(f"ref={ref}\n")
            for directory in sorted(extra_dirs):
                for name in sorted(os.listdir(directory)):
                    if name.endswith(".dll"):
                        f.write(f"ref={os.path.join(directory, name)}\n")
            for define in defines:
                f.write(f"define={define}\n")
        print(f"\n== {target} IL 후처리")
        run = subprocess.run(["dotnet", os.path.join(harness_dir, "ILPPCheck.dll"), request], capture_output=True, text=True)
        print(re.sub(re.escape(PROJECT + os.sep), "", (run.stdout + run.stderr).strip()))
        if run.returncode != 0:
            exit_code = 1
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
