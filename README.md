# Kimchi Breaker

4인 1인칭 김장 파티 게임. Unity 6000.6.8f1, Windows 64-bit, P2P 호스트(Netcode for GameObjects + Unity Relay).

이 저장소에는 **예찬 담당 범위**(네트워크, 방 관리, 플레이어 컨트롤, 카메라, 게임 진행 루프)가 구현되어 있다.
남현 담당 콘텐츠(김장 재료, 스킬 효과, 추첨 연출, 정식 UI/아트/사운드)가 들어올 자리는 임시 구현과 연결 지점으로 비워 두었다.

---

## 1. 시작하기

1. Unity Hub 에서 **Unity 6000.6.8f1** 설치. 모듈은 **Windows Build Support (Mono)** 를 함께 선택한다.
2. 이 저장소를 받아 Unity Hub → Add → 저장소 폴더 선택.
3. 처음 열 때 패키지를 받느라 몇 분 걸린다. 콘솔에 빨간 오류가 없어야 정상이다.
4. 메뉴 **Kimchi Breaker → 프로젝트 점검** 을 눌러 "문제 없음" 이 나오는지 확인한다.
5. Play 를 누른다. 어떤 씬을 열어 두었든 항상 `Main` 씬에서 시작한다.

### 혼자 테스트하기
에디터와 개발 빌드에서는 1명으로도 게임을 시작할 수 있다.
메인 메뉴 → **직접 연결 방 열기** → 대기실에서 **게임 시작**.

### 여러 명 테스트하기 (한 PC)
1. **Window → Multiplayer → Multiplayer Play Mode** 에서 가상 플레이어(Player 2~4)를 켠다.
2. Play 를 누르면 창이 여러 개 뜬다.
3. 한 창에서 **직접 연결 방 열기**, 나머지 창에서 주소 `127.0.0.1` 로 **IP로 참가**.

### 다른 PC 와 테스트하기 (온라인)
온라인 방(방 코드, 공개 방 목록)은 Unity Relay 를 쓰므로 한 번 설정이 필요하다.
1. **Edit → Project Settings → Services** 에서 Unity 계정 조직을 고르고 프로젝트를 연결한다.
2. [Unity Cloud 대시보드](https://cloud.unity.com) → 해당 프로젝트 → **Multiplayer** 에서 Relay 와 Lobby 를 켠다(4인 규모는 무료 범위).
3. 메인 메뉴 → **공개 방 만들기** 또는 **비공개 방 만들기** → 대기실에 표시되는 방 코드를 친구에게 알려 준다.

> 같은 공유기 안이라면 개발 빌드의 "같은 네트워크 허용" + IP 참가로도 테스트할 수 있다(Windows 방화벽에서 포트 7777 허용 필요).

### 조작
| 키 | 동작 |
|---|---|
| 마우스 | 시점 |
| W A S D | 이동 (라운드 진행 중에만) |
| 좌클릭 | 재료 때리기 |
| E / 우클릭 | 액티브 스킬 (라운드당 1회) |
| Esc | 일시 메뉴 (나가기, 방장은 게임 중단) |

### 빌드
메뉴 **Kimchi Breaker → Windows 빌드 (개발용 / 배포용)**. 결과는 `Builds/` 폴더.
배포용 빌드에서는 IP 직접 연결이 숨겨지고 온라인 방만 쓸 수 있다.

---

## 2. 게임 흐름

```
로딩 → 메인 메뉴 → 대기실 ─(방장 시작)→ 라운드 시작 연출(탑뷰→1인칭) → 라운드 진행(타이머)
      ↑                 ↑                                                        │
      │                 └──── 피날레(방장이 대기실 복귀) ←─ 목표 승수 달성 ←──┐    ▼
      └── 방 나가기                                                        결과 ← 추첨 ← 정산
                                                               (미달 시 다음 라운드 시작 연출로)
```

- 단계와 타이머는 **호스트만** 결정하고 모든 클라이언트에 같은 상태를 보낸다(`MatchSnapshot`).
- 라운드 승자: 김치를 가장 많이 모은 사람. 동점이면 그 개수에 먼저 도달한 사람.
- 추첨: 김치 1개 = 번호표 1장. 호스트가 암호학적 난수로 1장을 뽑고, 당첨자는 다음 라운드 1판 동안 스킬을 받는다.
- 진행 중 인원이 최소 인원(정식 2명)보다 적어지면 대기실로 돌아간다.

---

## 3. 폴더 구조

```
Assets/KimchiBreaker/
  Scripts/Core/       게임 규칙(엔진 무관 순수 C#). 상태 머신, 검증기, 난수, 닉네임 정리 등
  Scripts/Runtime/
    App/              GameBootstrap (시작점, 로딩 화면)
    Net/              NetworkSession(방/연결), MatchNetwork(진행 동기화), PlayerAvatar(플레이어)
    Gameplay/         IKimchiHittable(타격 대상 인터페이스), 임시 연습 재료와 스포너
    Presentation/     카메라 연출, 경기장, 임시 UI, 임시 캐릭터 모양
  Scripts/Editor/     에디터 메뉴(점검, 빌드)
  Resources/KimchiBreaker/Net/   네트워크 프리팹
  Scenes/Main.unity   유일한 씬
  Tests/EditMode/     규칙 테스트(Unity Test Runner 와 dotnet test 양쪽에서 실행)
Tools/                에디터 없이 쓰는 검증/생성 도구
```

---

## 4. 남현 님 작업 연결 가이드

모든 판정은 **서버(호스트)에서만** 한다. 클라이언트는 요청만 보내고, 결과는 동기화된 값으로 받는다.

### 4.1 김장 재료 (임시 `PracticeIngredient` 대체)
- 재료 프리팹에 `NetworkObject` + 재료 스크립트(`NetworkBehaviour`, `IKimchiHittable` 구현)를 붙인다.
- 물리로 굴러야 하면 `Rigidbody` + `NetworkTransform` + `NetworkRigidbody` 를 함께 붙인다(현재 연습 재료와 같은 구성).
- 플레이어가 클릭하면 서버가 거리/시야각/클릭 빈도를 검증한 뒤 `ServerApplyHit(in HitContext)` 를 호출한다.
  `HitContext` 에 때린 사람, 맞은 지점, 방향, 때린 사람의 스킬이 들어 있다.
- 막타로 완성되면 서버 코드에서 `MatchNetwork.Instance.ServerAwardKimchi(clientId, 개수)` 를 호출한다(황금 배추는 2~3).
- 숨김 HP 는 서버에만 두고, 클라이언트에는 진행도(예: `NetworkVariable<byte>`)만 보내 색을 바꾼다.
- 새 네트워크 프리팹은 `Resources/KimchiBreaker/Net/` 에 넣고 `NetworkPrefabCatalog` 의 목록에 경로를 추가한다.
- 스포너는 `PracticeIngredientSpawner` 를 대체하면 된다. 서버에서만 돌고, `MatchNetwork` 가 라운드 단계에 맞춰 호출한다.

### 4.2 스킬 효과
- 현재 장착 스킬: `MatchNetwork.Instance.Snapshot` 의 각 `PlayerSlot.Skill` (모든 피어), 서버는 `ServerGetSkill(clientId)`.
- 액티브 스킬 발동: 플레이어가 E/우클릭 → 서버가 검증(라운드 중, 보유, 1회) → 모든 피어에서
  `MatchNetwork.Instance.SkillActivated` 이벤트 발생(누가, 어떤 스킬, 조준 지점). 효과는 여기에 연결한다.
- 패시브 스킬(불꽃 고무장갑 등)은 서버의 타격 처리에서 `HitContext.AttackerSkill` 을 보고 적용한다.

### 4.3 화면/연출
- 단계 변화: `MatchNetwork.Instance.PhaseChanged` (이전, 새 단계).
- 표시할 데이터: `MatchNetwork.Instance.Snapshot` (단계, 남은 시간, 플레이어별 김치/번호표/승수/스킬, 당첨 번호 등).
  남은 시간은 `Snapshot.GetRemainingSeconds(MatchNetwork.Instance.ServerNow)`.
- 방 상태/오류 문구: `NetworkSession.Instance` (Status, LastError, RoomCode).
- 즉시 타격 피드백(효과음, 파티클): `PlayerAvatar.LocalHitPerformed` (내 클릭 즉시), 다른 사람 손동작은 `PlayerAvatar.HitAnimationPlayed`.
- 임시 UI(`GameUI`), 임시 캐릭터(`AvatarVisuals`), 임시 경기장(`ArenaBuilder`)은 통째로 교체해도 네트워크 동작에 영향이 없다.

---

## 5. 보안/안정성 설계

- **호스트 권한**: 점수, 타이머, 막타, 추첨, 승패는 호스트만 결정한다. 클라이언트는 NetworkVariable 을 쓸 수 없다.
- **요청 검증**: 클라이언트 → 서버 RPC 는 모두 보낸 사람(위조 불가) 확인, 단계/권한/값 범위 확인을 거친다.
  서버 → 클라이언트 RPC 는 서버만 호출할 수 있게 막았다.
- **이동 검증**: 플레이어 위치는 서버가 속도·범위·단계를 검사해 인정한 값만 다른 사람에게 보낸다. 순간이동/속도 조작은 되돌려진다.
- **타격 검증**: 서버가 서버 기준 위치에서 거리와 시야각을 다시 계산한다.
- **요청 빈도 제한**: 요청 종류별 토큰 버킷. 비정상 요청이 쌓이면 자동으로 내보낸다.
- **접속 승인**: 접속 데이터 크기/형식, 게임 버전, 인원, 진행 중 여부를 확인한다. 거절 사유는 정해진 코드로만 보낸다.
- **닉네임**: 제어 문자, 보이지 않는 문자, 결합 문자, 태그 기호를 제거하고 길이를 제한한다(서버와 화면 양쪽).
- **통신**: 온라인 방은 Relay + DTLS 암호화. IP 직접 연결은 개발 빌드에서만 열린다.
- **예외 격리**: 화면/연출 쪽 코드에서 예외가 나도 네트워크 처리는 계속되도록 이벤트 호출을 보호했다.

호스트 자신의 부정행위는 P2P 호스트 구조상 막을 수 없다(호스트가 곧 서버). 공개 대회 수준의 공정성이 필요해지면 전용 서버로 옮겨야 한다.

---

## 6. 검증 도구 (에디터 없이 실행)

```bash
# 게임 규칙 테스트 151개 (.NET 8 SDK 필요)
dotnet test Tools/CoreTests

# 프로젝트 전체 컴파일 검사 (패키지 포함, Unity 에디터 설치 경로 필요)
python3 Tools/CompileCheck/compile_check.py --editor "<Unity 설치 경로>/Editor"

# Netcode 코드 생성(RPC/NetworkVariable) 검사 — Unity 에서만 보이는 오류를 미리 잡는다
python3 Tools/ILPPCheck/run_ilpp_check.py --editor "<Unity 설치 경로>/Editor"

# .meta / 네트워크 프리팹 / Main 씬 다시 생성 (Unity 에서 프리팹을 직접 고치기 시작했다면 --skip-content)
python3 Tools/AssetGen/generate_assets.py
```

Unity 안에서는 **Window → General → Test Runner → EditMode** 에서 같은 규칙 테스트를 돌릴 수 있다.
