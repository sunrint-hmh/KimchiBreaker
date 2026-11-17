#!/usr/bin/env python3
"""Unity 에디터 없이 프로젝트 에셋(.meta, 네트워크 프리팹, Main 씬, 빌드 설정)을 만든다.

- GUID 와 fileID 는 경로/이름에서 결정적으로 계산하므로, 다시 실행해도 같은 값이 나온다.
- 이미 있는 .meta 는 건드리지 않는다(Unity 가 만든 GUID 를 지키기 위해).
- 프리팹/씬은 매번 다시 쓴다. Unity 에서 직접 수정하기 시작했다면 이 스크립트로 덮어쓰지 말 것(--skip-content).

사용법
  python3 Tools/AssetGen/generate_assets.py
"""
import argparse
import hashlib
import os
import re
import struct
import sys

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ROOT = os.path.join(PROJECT, "Assets", "KimchiBreaker")
NET_PREFAB_DIR = os.path.join(ROOT, "Resources", "KimchiBreaker", "Net")
SCENE_PATH = os.path.join(ROOT, "Scenes", "Main.unity")

# Netcode for GameObjects 2.13.3 스크립트 GUID (패키지의 .meta 에서 확인한 값)
NETCODE_GUIDS = {
    "NetworkObject": "d5a57f767e5e46a458fc5d3c628d0cbb",
    "NetworkTransform": "e96cb6065543e43c4a752faaa1468eb1",
    "NetworkRigidbody": "f6c0be61502bb534f922ebb746851216",
}

# URP 템플릿의 Volume 스크립트와 기본 볼륨 프로필
URP_VOLUME_SCRIPT_GUID = "172515602e62fb746b5d573b38a5fe58"


# ---------------------------------------------------------------------------
# 결정적 ID
# ---------------------------------------------------------------------------
def stable_guid(key):
    return hashlib.md5(("KimchiBreaker:" + key).encode("utf-8")).hexdigest()


def stable_file_id(key):
    # Unity 의 로컬 fileID 는 64비트 정수. 양수 범위에서 결정적으로 만든다.
    value = int(hashlib.sha1(("fileID:" + key).encode("utf-8")).hexdigest()[:15], 16)
    return value + 1000000000


def xxhash32(data, seed=0):
    p1, p2, p3, p4, p5 = 2654435761, 2246822519, 3266489917, 668265263, 374761393
    mask = 0xFFFFFFFF

    def rotl(x, r):
        return ((x << r) | (x >> (32 - r))) & mask

    length = len(data)
    i = 0
    if length >= 16:
        v1 = (seed + p1 + p2) & mask
        v2 = (seed + p2) & mask
        v3 = seed & mask
        v4 = (seed - p1) & mask
        while i <= length - 16:
            for idx in range(4):
                lane = struct.unpack_from("<I", data, i)[0]
                i += 4
                if idx == 0:
                    v1 = (rotl((v1 + lane * p2) & mask, 13) * p1) & mask
                elif idx == 1:
                    v2 = (rotl((v2 + lane * p2) & mask, 13) * p1) & mask
                elif idx == 2:
                    v3 = (rotl((v3 + lane * p2) & mask, 13) * p1) & mask
                else:
                    v4 = (rotl((v4 + lane * p2) & mask, 13) * p1) & mask
        h = (rotl(v1, 1) + rotl(v2, 7) + rotl(v3, 12) + rotl(v4, 18)) & mask
    else:
        h = (seed + p5) & mask
    h = (h + length) & mask
    while i <= length - 4:
        h = (rotl((h + struct.unpack_from("<I", data, i)[0] * p3) & mask, 17) * p4) & mask
        i += 4
    while i < length:
        h = (rotl((h + data[i] * p5) & mask, 11) * p1) & mask
        i += 1
    h ^= h >> 15
    h = (h * p2) & mask
    h ^= h >> 13
    h = (h * p3) & mask
    h ^= h >> 16
    return h


def global_object_id_hash(prefab_guid, network_object_file_id):
    """Netcode 가 프리팹 에셋에 대해 계산하는 GlobalObjectIdHash 와 같은 방식(에디터가 열 때 다시 계산해 맞춘다)."""
    text = "GlobalObjectId_V1-1-{0}-{1}-0".format(prefab_guid, network_object_file_id)
    return xxhash32(text.encode("utf-8"))


# ---------------------------------------------------------------------------
# .meta
# ---------------------------------------------------------------------------
def rel(path):
    return os.path.relpath(path, PROJECT).replace(os.sep, "/")


def meta_text(path, guid):
    if os.path.isdir(path):
        return ("fileFormatVersion: 2\nguid: {0}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").format(guid)
    ext = os.path.splitext(path)[1]
    if ext == ".cs":
        return ("fileFormatVersion: 2\nguid: {0}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
                "  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n"
                "  assetBundleName: \n  assetBundleVariant: \n").format(guid)
    if ext == ".asmdef":
        return ("fileFormatVersion: 2\nguid: {0}\nAssemblyDefinitionImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").format(guid)
    if ext == ".prefab":
        return ("fileFormatVersion: 2\nguid: {0}\nPrefabImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").format(guid)
    if ext in (".md", ".txt", ".json"):
        return ("fileFormatVersion: 2\nguid: {0}\nTextScriptImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").format(guid)
    return ("fileFormatVersion: 2\nguid: {0}\nDefaultImporter:\n  externalObjects: {{}}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n").format(guid)


def read_guid(path):
    meta = path + ".meta"
    if not os.path.isfile(meta):
        return None
    with open(meta, encoding="utf-8") as f:
        m = re.search(r"^guid:\s*([0-9a-f]{32})", f.read(), re.M)
    return m.group(1) if m else None


def guid_for(path):
    """이미 .meta 가 있으면 그 GUID, 없으면 결정적으로 만든 GUID."""
    return read_guid(path) or stable_guid(rel(path))


def ensure_meta(path, created):
    meta = path + ".meta"
    if os.path.isfile(meta):
        return
    with open(meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(meta_text(path, stable_guid(rel(path))))
    created.append(rel(meta))


def ensure_metas(root, created):
    """root 와 그 아래 모든 파일/폴더에 .meta 를 만든다(이미 있으면 그대로 둔다)."""
    ensure_meta(root, created)
    for current, dirs, files in os.walk(root):
        dirs[:] = sorted(d for d in dirs if not d.endswith("~") and not d.startswith("."))
        for d in dirs:
            ensure_meta(os.path.join(current, d), created)
        for name in sorted(files):
            if name.endswith(".meta") or name.startswith("."):
                continue
            ensure_meta(os.path.join(current, name), created)


# ---------------------------------------------------------------------------
# YAML 조각
# ---------------------------------------------------------------------------
HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


def game_object(file_id, name, component_ids):
    components = "".join("  - component: {{fileID: {0}}}\n".format(c) for c in component_ids)
    return ("--- !u!1 &{id}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            "  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n"
            "  m_Component:\n{components}  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n"
            "  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
            ).format(id=file_id, components=components, name=name)


def transform(file_id, go_id):
    return ("--- !u!4 &{id}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            "  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n"
            "  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n"
            "  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {{fileID: 0}}\n"
            "  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n").format(id=file_id, go=go_id)


def mono_behaviour(file_id, go_id, script_guid, fields=""):
    return ("--- !u!114 &{id}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            "  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n{fields}").format(id=file_id, go=go_id, guid=script_guid, fields=fields)


def network_object(file_id, go_id, hash_value):
    fields = ("  GlobalObjectIdHash: {0}\n  InScenePlacedSourceGlobalObjectIdHash: 0\n  AlwaysReplicateAsRoot: 0\n"
              "  SynchronizeTransform: 1\n  ActiveSceneSynchronization: 0\n  SceneMigrationSynchronization: 1\n"
              "  SpawnWithObservers: 1\n  DontDestroyWithOwner: 0\n  AutoObjectParentSync: 1\n").format(hash_value)
    return mono_behaviour(file_id, go_id, NETCODE_GUIDS["NetworkObject"], fields)


def character_controller(file_id, go_id):
    return ("--- !u!143 &{id}\nCharacterController:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            "  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  m_Material: {{fileID: 0}}\n  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
            "  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_LayerOverridePriority: 0\n"
            "  m_ProvidesContacts: 0\n  m_Enabled: 1\n  serializedVersion: 3\n  m_Height: 1.8\n  m_Radius: 0.35\n"
            "  m_SlopeLimit: 45\n  m_StepOffset: 0.3\n  m_SkinWidth: 0.05\n  m_MinMoveDistance: 0.001\n"
            "  m_Center: {{x: 0, y: 0.95, z: 0}}\n").format(id=file_id, go=go_id)


def rigidbody(file_id, go_id):
    return ("--- !u!54 &{id}\nRigidbody:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            "  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  serializedVersion: 5\n  m_Mass: 1\n  m_LinearDamping: 0.6\n  m_AngularDamping: 0.6\n"
            "  m_CenterOfMass: {{x: 0, y: 0, z: 0}}\n  m_InertiaTensor: {{x: 1, y: 1, z: 1}}\n"
            "  m_InertiaRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_IncludeLayers:\n    serializedVersion: 2\n"
            "    m_Bits: 0\n  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_ImplicitCom: 1\n"
            "  m_ImplicitTensor: 1\n  m_UseGravity: 1\n  m_IsKinematic: 0\n  m_Interpolate: 0\n"
            "  m_Constraints: 0\n  m_CollisionDetection: 0\n").format(id=file_id, go=go_id)


def sphere_collider(file_id, go_id, radius):
    return ("--- !u!135 &{id}\nSphereCollider:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            "  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  m_Material: {{fileID: 0}}\n  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
            "  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_LayerOverridePriority: 0\n"
            "  m_IsTrigger: 0\n  m_ProvidesContacts: 0\n  m_Enabled: 1\n  serializedVersion: 3\n"
            "  m_Radius: {r}\n  m_Center: {{x: 0, y: 0, z: 0}}\n").format(id=file_id, go=go_id, r=radius)


# ---------------------------------------------------------------------------
# 프리팹
# ---------------------------------------------------------------------------
def script_guid(relative_path):
    return guid_for(os.path.join(ROOT, "Scripts", "Runtime", relative_path))


def write_prefab(name, build_components):
    path = os.path.join(NET_PREFAB_DIR, name + ".prefab")
    prefab_guid = guid_for(path)
    ids = {}

    def fid(key):
        if key not in ids:
            ids[key] = stable_file_id(name + "/" + key)
        return ids[key]

    go = fid("GameObject")
    tr = fid("Transform")
    no = fid("NetworkObject")
    extra = build_components(fid, go)  # [(fileID, yaml)]
    component_ids = [tr, no] + [c[0] for c in extra]
    text = HEADER + game_object(go, name, component_ids) + transform(tr, go)
    text += network_object(no, go, global_object_id_hash(prefab_guid, no))
    for _, yaml in extra:
        text += yaml
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return path


def build_prefabs():
    os.makedirs(NET_PREFAB_DIR, exist_ok=True)
    written = []

    def match_state(fid, go):
        mb = fid("MatchNetwork")
        return [(mb, mono_behaviour(mb, go, script_guid("Net/MatchNetwork.cs")))]

    def player_avatar(fid, go):
        cc = fid("CharacterController")
        mb = fid("PlayerAvatar")
        return [(cc, character_controller(cc, go)), (mb, mono_behaviour(mb, go, script_guid("Net/PlayerAvatar.cs")))]

    def practice_ingredient(fid, go):
        rb = fid("Rigidbody")
        sc = fid("SphereCollider")
        nt = fid("NetworkTransform")
        nr = fid("NetworkRigidbody")
        mb = fid("PracticeIngredient")
        return [
            (rb, rigidbody(rb, go)),
            (sc, sphere_collider(sc, go, 0.35)),
            (nt, mono_behaviour(nt, go, NETCODE_GUIDS["NetworkTransform"])),
            (nr, mono_behaviour(nr, go, NETCODE_GUIDS["NetworkRigidbody"])),
            (mb, mono_behaviour(mb, go, script_guid("Gameplay/PracticeIngredient.cs"))),
        ]

    written.append(write_prefab("MatchState", match_state))
    written.append(write_prefab("PlayerAvatar", player_avatar))
    written.append(write_prefab("PracticeIngredient", practice_ingredient))
    return written


# ---------------------------------------------------------------------------
# 씬
# ---------------------------------------------------------------------------
def build_scene():
    template_path = os.path.join(PROJECT, "Assets", "Scenes", "SampleScene.unity")
    os.makedirs(os.path.dirname(SCENE_PATH), exist_ok=True)
    if os.path.isfile(template_path):
        with open(template_path, encoding="utf-8") as f:
            template = f.read()
        # 씬 공통 설정(조명/내비메시 등) 블록만 가져온다.
        settings = template[:template.index("--- !u!1 &")]
        volume_profile = re.search(r"sharedProfile: (\{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\})", template)
        volume_profile = volume_profile.group(1) if volume_profile else "{fileID: 0}"
    else:
        with open(SCENE_PATH, encoding="utf-8") as f:
            existing = f.read()
        settings = existing[:existing.index("--- !u!1 &")]
        volume_profile = re.search(r"sharedProfile: (\{[^}]*\})", existing).group(1)

    boot_go, boot_tr, boot_mb = stable_file_id("Main/Boot/GO"), stable_file_id("Main/Boot/TR"), stable_file_id("Main/Boot/MB")
    vol_go, vol_tr, vol_mb = stable_file_id("Main/Vol/GO"), stable_file_id("Main/Vol/TR"), stable_file_id("Main/Vol/MB")
    text = settings
    text += game_object(boot_go, "GameBootstrap", [boot_tr, boot_mb])
    text += transform(boot_tr, boot_go)
    text += mono_behaviour(boot_mb, boot_go, script_guid("App/GameBootstrap.cs"))
    text += game_object(vol_go, "Global Volume", [vol_tr, vol_mb])
    text += transform(vol_tr, vol_go)
    text += mono_behaviour(vol_mb, vol_go, URP_VOLUME_SCRIPT_GUID,
                           "  m_IsGlobal: 1\n  priority: 0\n  blendDistance: 0\n  weight: 1\n  sharedProfile: {0}\n".format(volume_profile))
    text += ("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n"
             "  - {{fileID: {0}}}\n  - {{fileID: {1}}}\n").format(boot_tr, vol_tr)
    with open(SCENE_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return SCENE_PATH


def update_build_settings():
    path = os.path.join(PROJECT, "ProjectSettings", "EditorBuildSettings.asset")
    with open(path, encoding="utf-8") as f:
        text = f.read()
    scene_entry = "  m_Scenes:\n  - enabled: 1\n    path: {0}\n    guid: {1}\n".format(rel(SCENE_PATH), guid_for(SCENE_PATH))
    text = re.sub(r"  m_Scenes:\n(  - enabled: \d\n    path: [^\n]*\n    guid: [0-9a-f]*\n)*", scene_entry, text)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--skip-content", action="store_true", help="프리팹/씬은 건드리지 않고 .meta 만 만든다")
    args = parser.parse_args()

    created = []
    # 스크립트 GUID 를 먼저 확정해야 프리팹/씬이 올바른 스크립트를 가리킨다.
    ensure_metas(ROOT, created)
    if not args.skip_content:
        build_prefabs()
        build_scene()
        ensure_metas(ROOT, created)
        update_build_settings()

    print("새로 만든 .meta {0}개".format(len(created)))
    for path in created:
        print("  " + path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
