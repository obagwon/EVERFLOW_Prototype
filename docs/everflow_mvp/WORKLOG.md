# EVERFLOW MVP 작업 기록

## 2026-10-17 정리 점검 (최신, 단일 에이전트)

- 작업 전 지침/Git/HANDOFF 확인 후 `Backups/20261017-cleanup`에462개 기준 해시와 작업 전 diff/status, 수정 대상 원본을 보존했다. 기존 미커밋 통합 작업·씬·모델·meta·백업은 유지했다.
- 기존 파일 이동/삭제 없이 기본 Volume Profile의 사용 목록 밖 깨진 하위 자산9개와 존재하지 않는 SampleScene 빌드 항목을 제거했다. 이번 검사 전용 임시4개는 `validator-fixture.zip` 바이트 검증 백업 후 제거했다. 상세 이름/fileID/복원은 HANDOFF §7 및 `cleanup-volume-profile.json` 참조.
- finale 검사의 null gate 접근 순서를 수정하고 체크포인트/Renderer/grounding 참조 검사를 일반 릴리스 검사에도 연결했다. 오래된 원본 Map 물리 검사 메뉴는 MVP 타이틀 상태에서 씬을 바꾸기 전에 안내하고 중단한다.
- 빌드 smoke 자동 실행 클래스를 DEVELOPMENT_BUILD로 제한했다. 일반 플레이 규칙·너그러운 완료·완료 유지·이동/카메라/HUD/타이틀·씬 배치는 변경하지 않았다.
- `.gitignore` 로컬 백업/Python 캐시 제외, 오프라인 검사에 별도 출력/명시적 보존 기준/개발 플레이어 컴파일 옵션을 추가했다. 기존 증거를 덮어쓰지 않았다.
- 정적 참조53개와 validator 관리 코드 검사11개 PASS, C# 네 조건 컴파일 및 플레이어 경계 검사 PASS. 기존1.1.1 플레이어 headless 진입/재로드 PASS. 기존 renderer 디버그 리소스7개는 불확실해 유지했으므로 전체 에셋 검사는 FAIL로 기록한다.
- 정상 사용자 Editor 연결이 없어 수정 후 Unity 임포트/Play/새 빌드는 미검증이다. 이전249개 PASS와 새 결과를 구분했다. 동적 A 걸쇠/완성 미술·음향/사람 테스트는 기존 미완료로 유지했다. commit/push/reset/clean/대량 추적 해제 없음.

## 2026-10-17 통합 실행 결과 (16:14 KST)

- 단일 에이전트가 조사·구현·씬 저장·자체 검토를 직렬 수행했다. 이번 회차에 하위 에이전트를 생성하지 않았다.
- 정상 사용자 Editor의 명시적으로 활성화된 MVP 연결을 사용했다. 라이선스 변경/중복 Editor/강제 종료 없이 실행 가능하다.
- P3 실제 Play: `p3-checks-20261017-064730-116.txt`, runtime 77개 PASS. 계단/펌프 옆길/고지 출발대/카메라 레이/물/잎 횡단/완료 유지/재시작 확인.
- `Assets/Scenes/Everflow_Map_MVP.unity` 실제 생성·저장·재로드했다. 원본 Map과 기존 P3 사본은 보존했다. 공용 프리팹 Apply 없이 씬 객체/인스턴스 override로 통합했다.
- 회전기(UI P3/index2)는 단일 Frozen/Angle 구조를 유지해 고정 A+중간 안전 지점+가동 B로 축소했다. 최종(UI P6/index5)은 준비/실제 횡단/별도 마지막 PULSE/수문/물/물레방아/식생·빛/조망/스킵/새 게임을 연결했다.
- `mvp-checks-20261017T065831312Z.txt`: 134개 실제 Play/상태 검사 PASS. 초반·후반 스킵/정상 최종 상태 일치, 연출 중 재시작과 연속 초기화 포함. 앞 구간은 이 실행에서 fixture이므로 전체 도보 완주를 뜻하지 않는다.
- 최종 `mvp-checks-20261017T070952791Z.txt`: **249개 PASS**. 기존 꽃/덩굴/잔해/문, 새 하행 계단과 최종 구간을 실제 이동으로 연결하고 타이틀부터 엔딩/새 게임까지 실행했다. 스킵/우회 비교의 API·위치 fixture는 보고서에 구분했다. 최종 연결의 먼 가장자리만 밟는 우회는 정비 측 절반에서의 진입 증거로 차단했다.
- 화면 검토에서 발견한 장문/거울 표지는 짧은 카메라 지향 표지로 수리했다. 기존 MVP를 byte 백업 후 세 TextMesh만 수정·저장했고 Create/Open을 재실행해도 재생성하지 않는다. `EFMvpSetup/Checks`, `EFMvpFinale/Rotary/BuildSmoke`, `EFMapWorld/Node`, 타이틀 경로·빌드 씬 목록이 통합 변경이다.
- 최종 검증 후 타이틀 시작 경로와 첫 두 활성 씬을 Title/MVP로 연결했다. 정상 사용자 Editor에서 Windows **v1.1.1** 빌드 성공(오류0/경고485). 실제 exe의 headless 타이틀 진입·두 번 재로드와 종료0을 확인했다. 빌드 후 Editor 타이틀4개 PASS, MVP 편집 씬 재오픈 완료.
- 경고485개는 기존 inference/Sentis 셰이더로 분류했다. 패키지/보호 설정은 변경하지 않았다. 최종332개 baseline 비교에서 원본/P3/프리팹/모델/HUD/이미지 보존 PASS. ProjectSettings 변경은 bundleVersion 한 줄뿐이다. 사람 검증·빌드의 화면 플레이·전 범위 자유 탐색은 미검증이다.

아래는 이전 시점 기록이며 최신 상태는 HANDOFF와 최종 실행 결과를 우선한다.

## 2026-10-17 통합 MVP 재개 (15:31 KST)

사용자 요청 범위가 P3 한정에서 통합 MVP로 확장됐다. 이번부터 단일 에이전트만 사용한다. 원본/기존 P3 사본/사용자 문서를 조사했고 `Everflow_Map_MVP`는 아직 없다. `evidence/integration-20261017/before.json`에 현재332개 자산/패키지/설정 해시를 별도로 보존했다. 기존 baseline은 덮어쓰지 않았다.

정상 Editor의 revision2 검사 연결을 재활성화했다. P3 sequence1 실행은 실제 Play 진입/일시정지 해제까지 관측했으나 아직 첫 프레임/terminal 증거가 없다. Game 탭 활성화 조작을 요청했다. 검사 실행 성공과 접수 상태를 구분하며, 기존 실패 배치나 라이선스 설정 변경은 하지 않는다.

## 2026-10-17 재개 — 현재 상태

재개 시작: 13:11:09 KST (04:11:09 UTC). 기존 diff와 4개 보고서를 먼저 읽었다. 원본 코드의 처음부터 재작성은 하지 않았다.

| 구분 | 실제 상태 |
|---|---|
| 이미 끝난 것 | P3 생성 도구·지역 상태 소스·초기 독립 검토·소스 컴파일. 이후 정상 사용자 Editor에서 실험 씬 생성/저장 및 재오픈 참조 검사까지 진행됨 |
| 중단 당시 진행 중 | `p3-checks-20261016-132900-485.txt`: Editor 2개 항목 PASS, runtime/API fixture 17개 PASS 후 출발대 CharacterController 지지 판정 FAIL. 그 뒤의 잎 이동·출구·재시작 검사 미실행 |
| 이번 수정 | 동높이 출발대/마지막 계단 중첩에 대한 지지 판정 보강, 실제 실패 상태 진단과 회귀 fixture 추가 |
| 남은 것 | 수정 후 P3 Play 재검증·실제 횡단, 통과 후 회전 연결기·최종 직접 PULSE·복원·스킵·초기화·빌드 |

기존 4개 문서의 '씬 미생성/Play 미실행' 문구는 첫 회차 기록에 머물러 있었다. 아래 역사 기록은 보존하고 HANDOFF/VERIFICATION에 최신 실제 결과를 반영한다.

### 재개 담당·파일 소유권

- 총괄: 문서, 실행/진단 도구, 보호 파일 스냅샷, 컴파일·통합 조정. Unity 씬/Editor 실행은 총괄 단독 조정하며 이번에는 접근 가능한 연결이 없다.
- `/root/structure`: 구조·환경 읽기 전용. 역할/UI/index/대상 재확인, 최신 CLI/MCP 연결 제한 조사.
- `/root/implementation`: 먼저 `Assets/EverflowMap/Runtime/EFCanalExperiment.cs`만 수정하고 동결. 이후 새 `Assets/EverflowMap/Editor/EFCanalApproachChecks.cs/.meta`를 배정하여 실제 접근·조준 검사 구현. 인터페이스는 `Run(world, experiment, check, log)` 이터레이터이며 게임 런타임 참조를 추가하지 않는다.
- `/root/review`: 초기 읽기 검토 후 별도 배정된 `Assets/EverflowMap/Editor/EFCanalExperimentTests.cs`만 수정. 실제 실패 진단과 격리 회귀 fixture, 구현 담당의 변경 독립 검토.

같은 파일 동시 수정 없음. 기존 실험 씬도 덮어쓰지 않는다. `evidence/resume-20261017/protected-before.json`에 현재 사본·원본·공유 프리팹·패키지·빌드 설정의 재개 기준 해시를 저장했다.

### 환경 재확인

- 실제 토큰 계정 `home\\CodexSandboxOffline`, 상속된 환경 `USERNAME=vzd12`, `USERPROFILE=C:\\Users\\vzd12` 차이가 계속 존재한다.
- 최신 Editor 로그의 정상 사용자 실행(2026-10-16 22:28 KST)은 라이선스 연결/갱신 성공, Unity Student 만료일 2027-07-14를 기록한다. IPC 연결 거부를 라이선스 만료로 단정하지 않는다.
- 2026-10-17 현재 관측은 Hub helper만 보이며, CLI `status`는 인스턴스 없음, `pipeline list`는 서버 접속 불가/패키지 미설치다. CLI의 `isRunning:true`와 프로세스 목록이 서로 달라 샌드박스 발견 제한을 명시한다. 기존 Editor 강제 종료나 중복 배치 실행은 하지 않았다.
- 이전 라이선스 실패 배치를 다시 실행하지 않았다. ACL/보호 설정/라이선스 파일/설치 초기화도 변경하지 않았다.
- 기존 Unity 창 제어용 callable 도구와 살아 있는 MCP 연결이 없다. 정상 계정 Editor의 기존 `Run P3 Checks` 메뉴가 재검증의 최소 조작이다. 새 패키지 설치를 자동 진행하지 않는다.
- 증거: `evidence/followup-20261016/environment-diagnosis-20261017T041118165091Z.json` 및 같은 timestamp의 `editor-excerpt`(민감정보 제외).

### 현재 결함과 수정 근거

저장 씬의 출발대와 마지막 계단 윗면은 모두 y31이며 CP의 XZ가 두 도형에 겹친다. 기존 지지 판정은 첫 `Physics.Raycast`의 collider가 출발대여야 하므로 같은 높이 계단을 먼저 맞으면 거부했다. 두 번째 실제 보고서 `p3-checks-20261017-041754-665.txt`에서 grounded=true, 첫 hit=마지막 계단, 두 collider의 ray 거리=0.199999를 확인하여 이 원인을 확정했다.

구현은 지정 바닥 `Collider.Raycast`로 실제 바닥을 확인하고, 그보다 0.03m 이상 높은 다른 고체가 있으면 거부하도록 보강했다. 기존 접지/수직거리/발 위치 범위와 공중 거부는 유지한다. 실제 Play에서 출발대 지지/CP와6개 회귀 fixture를 통과했다. 다음 실패는 종점에서 데크보다7cm 높은 잎을 밟은 채 데크 지지를 요구한 테스트 가정이다. 실제 입력으로 잎에서 옆으로 내려온 뒤 검사하도록 수정했다. 기존 검사는 출발대까지 텔레포트해 접근 경로를 검증하지 못했으므로 별도 실제 계단 이동·카메라 레이 입력 검사를 선행하도록 추가했다. 두 후속 검사는 재실행 대기다.

### 재개에서 수행한 검사

- 13:36 KST 정상 Editor의 명시적 `Enable Local Check Bridge` 활성화 확인. 실제 wrong session/expired/unsupported/replay 네 요청 모두 거부, run 미실행·보호 파일 해시 불변. `editor-bridge-negative-20261017T043623289465Z.json`.
- bridge sequence3 `p3-repair-access` 실제 실행 완료: 49개 계단 Transform 변경, byte 백업 생성 후 사본 저장. `p3-access-repair-20261017T043626193Z-16390a32.json`, before83111d6a…/after5a59ef93…. 원본/shared 보호 PASS. 별도 저장 씬 비교17개 검사 PASS: 두 계단의 Transform 위치/회전/크기 외 나머지1043개 블록 보존. 기존 보호 baseline은 덮어쓰지 않았고 명시적 repair evidence를 인자로 전달했다.
- sequence4 `p3-checks` 실제 참조 검사/Play 진입 요청 후 도메인 재로드 관측. 새 terminal 보고서는 아직 없어 실행 성공으로 계산하지 않는다. 일시정지/프레임 진행 상태를 사용자에게 확인 중이다. 도메인 재로드 중 status가 잠시 disabled로 쓰인 후 main 세션으로 복원되는 현상을 발견해 import worker의 status writer 가드 검토 중이며, 현재 Play를 임의 중단하지 않는다.

- 13:30 KST: helper 병합 후 실제 Editor 컴파일/Play가 가능해졌다. `p3-checks-20261017-043005-587.txt`는 [EDIT]2개/runtime12개 PASS 뒤 첫 접근 계단에서 FAIL. 실제 local=(2.4662,15.8509,-47.9959), 접지 true, 첫 턱 top15.6과 발14.8509 차이≈0.75m > stepOffset0.55m. 기존 출발대 텔레포트 검사에서 발견할 수 없었던 실제 배치 결함이다.
- 총괄 추가 소유: `EFCanalExperimentSetup.cs`의 `RepairAccess()` 사본 한정 수정 도구와 생성 프로파일. 원래 사본의 고유 byte 백업/Undo/dirty guard/기존 또는 수정 프로파일만 허용/원본 자산 해시 비교/참조 검사 후 사본 저장. 첫 계단 시작 높이를14.6으로 낮추고 측면 계단을4.1m 동쪽 평행 이동하여 데크 밖에서 높이를 확보한 뒤 옆으로 진입한다. 도구 작성은 저장/실제 통과 증거가 아니다.
- 구현 담당 재배정: 현재 `EFCanalExperimentTests.cs`만 소유해 명시적 세션 활성화·고정 두 명령만 받는 로컬 Editor bridge 추가. 총괄은 Setup, 요청 CLI만 수정하므로 같은 파일 동시 수정 없음. 검토 담당은 두 변경의 독립 읽기 검토만 수행한다.

- 13:26 KST 실제 Unity 시작은 Safe Mode `Tests.cs:345 CS0103 EFCanalApproachChecks`로 중단됐다. 새 helper(13:23 저장)가 실제 Bee 컴파일 목록(10/16 캐시)에 없음을 확인했다. 별도 컴파일 결과를 Editor 성공으로 보고하지 않는다. 사용자에게 Safe Mode의 Assets > Refresh를 요청했고 라이브 메뉴 호출 수단은 없다. 읽기 독립 검토도 namespace/접근성/GUID/asmdef 문제 근거 없이 새 소스 미발견과 일치한다고 확인했다. 결과를 기다리며 파일을 다시 바꾸지 않는다.

- 세 담당의 현재 작업 완료/파일 동결 확인. 런타임 변경과 검사 코드는 다른 에이전트가 검토했다.
- `python -B tools/verify_mvp_experiment.py`: 최종 보강본 런타임34개/Editor27개/플레이어 조건 런타임34개 오류·경고0. 322개 기존 기준 파일 중 최초 회차의 World/Node 두 소스만 변경, 예상 밖 변경 없음. GUID/meta 검사 PASS.
- `python -B tools/test_unity_launch_guard.py`: 10개 PASS. 실제 Unity 실행 아님.
- `python -B tools/check_mvp_saved_scene.py`: 실제 생성된 씬1043개 직렬화 블록, 15개 정적/참조/보존 검사 PASS. 이번 재개에서 원본/사본 씬·프리팹·패키지·빌드 설정의 해시 변화 없음.
- `git diff --check`: 공백 오류 없음. CRLF 변환 안내는 Git 줄바꿈 경고이며 컴파일 경고가 아니다.
- 사용자 정상 계정 Editor의 두 번째 실행: [EDIT]2개 PASS, runtime30개 PASS 후 잎 종점 데크 지지 FAIL. 출발대 수정 효과·6개 지원 회귀 fixture·실제 잎 경로 캡슐 이동은 확인했다. 전체 성공으로 바꾸지 않았다.
- 최신 실제 접근・능력 레이/하차 보행 검사: 구현 담당과 다른 에이전트가 검토하고 실패 시 이터레이터/입력/활성 상태 정리를 보강했다. 컴파일 통과 후 사용자에게 기존 메뉴 한 번 재실행을 요청했다. Unity 연결 제한 때문에 자동 호출은 불가하다.

---

## 2026-10-16 첫 회차 기록 (아래는 당시 상태)

시작: 2026-10-16 21:30:45 KST (12:30:45 UTC). 기준 커밋: `9161746`.

## 보존과 기준

- 시작 시 `git status --short`: `?? docs/everflow_mvp/`만 존재. 이 폴더의 사용자 제공 `improvement_plan.md`를 읽고 보존한다.
- 프로젝트 내 `AGENTS.md`는 검색 결과 없음. 기존 `docs/TitleAndPuzzleProgress.md`, `docs/Everflow_Map_PlayGuide.md` 및 기존 검증 도구를 조사했다.
- 원본 Map/Title/6Puzzles/Graybox, 공용 프리팹, 모델·이미지, 패키지·빌드 목록은 변경하지 않는다. 실험 씬은 Unity SaveScene 사본 도구로만 생성한다. YAML 치환은 하지 않는다.

## 담당과 파일 소유권

| 담당 | 상태 | 수정 소유권 | 인터페이스/의존 관계 |
|---|---|---|---|
| 총괄 `/root` | 소스 통합·컴파일·보고 완료, Unity 실행 차단 | EFMapWorld.cs, EFMapNode.cs, tools/*mvp_experiment*.py, 01_p3_prototype/WORKLOG/VERIFICATION/REVIEW/HANDOFF | 실험 opt-in 접지 판정·완료 상태 복원, Unity 실행 단독 담당 |
| 구조 조사 `/root/structure` | 조사·기준 문서 완료 | 00_baseline.md만 (코드 수정 없음) | 실제 stage 대응·복구·카메라·엔딩 근거 조사 |
| 구현 `/root/implementation` | 지정 4파일 작성·최종 수정 동결 | EFCanalExperiment.cs, EFCanalExperimentSetup.cs 및 해당 meta만 | 기존 stage[3] 사본 생성 도구, 지역 복구/시각 |
| 독립 검토 `/root/review` | 최종 검토 완료·검사 코드 동결 | 재배정 후 EFCanalExperimentTests.cs 및 meta만 | 초기 검토 수정 금지 준수. 별도 테스트 도구 작성 허용 후 지정 파일만 수정 |

동시 최대 하위 에이전트 3개. 같은 파일 동시 수정 없음. Unity 실행/씬 저장/빌드는 총괄만 한다.

## 실행 순서와 판정 지점

1. 기준·구조 조사 → P3 도구·지역 상태 구현 → 독립 검토 → 컴파일/정적 검사 → Unity P3 실행 검증.
2. P3 실행을 막는 문제가 풀려야 P5/P6 확장을 통합한다. 사람 테스트는 이번 회차의 선행 조건으로 두지 않는다.
3. Unity 실행 불가 시 설정 도구와 실행 대기 상태를 산출물로 제공하고, 독립 가능한 검사·문서·결함 수정을 계속한다. 실제 생성/플레이/빌드와 구분한다.

## 조사에서 확인한 차이

사용자 역할 P3 수로는 실제 stage index 3(UI P4), 회전 연결기는 실제 index 2(UI P3). 실제 P1은 꽃+덩굴, P2는 파편+석판, P5는 뿌리+문이다. 진행 ID를 전역 변경하지 않는다. 마지막 별도 PULSE는 없으며 현재 원본은 준비 조건 충족으로 엔딩을 시작한다. 도달 자동 완료는 없지만 요청의 마지막 직접 입력은 아직 미충족이다.

## 환경 차단 기록

Unity 6000.3.21f1 및 Windows standalone 모듈 설치 확인. 총괄이 새로 시작한 배치 에디터 PID 38776만 종료했다. 기존 Hub/라이선스 프로세스는 종료하지 않았다.

`evidence/unity-baseline.log`: `Connection to channel LicenseClient-vzd12 refused`, `Timed-out after 60.01s`, `Licensing initialization failed after 74.81s`. 에셋 임포트/컴파일/executeMethod 단계까지 도달한 증거 없음. 보호 설정 변경·라이선스 우회·같은 명령 반복 안 함.

## 결정

- P3 먼저. 기존 실제 수로의 마당(y22.8)보다 낮은 출구(y13.25) 제안은 목표 공간 관계를 충족하지 않아 구현 전 재설계를 요청했다.
- 공유 런타임은 기존 씬 기본값 false인 실험 옵션만 추가한다. 보조 완료는 사용기록/각도 재검사를 요구하지 않고 실제 안전 착지만 확인하도록 한다.
- 완료 상태의 안전 복원 API는 최종 단계를 제외하며 최종 엔딩을 보조 인정으로 실행하지 않는다.

## 최종 변경 파일

- 기존 런타임: `Assets/EverflowMap/Runtime/EFMapWorld.cs`, `EFMapNode.cs`.
- 새 런타임: `Assets/EverflowMap/Runtime/EFCanalExperiment.cs` 및 meta.
- 새 Editor 도구: `Assets/EverflowMap/Editor/EFCanalExperimentSetup.cs`, `EFCanalExperimentTests.cs` 및 meta.
- 새 CLI: `tools/verify_mvp_experiment.py`, `tools/run_mvp_experiment_unity.py`.
- 문서: `00_baseline.md`, `01_p3_prototype.md`, 본 문서, `VERIFICATION.md`, `REVIEW.md`, `HANDOFF.md`, `evidence/*`.
- 사용자 제공 `improvement_plan.md`는 변경하지 않음. 원본 씬/공유 자산/패키지/빌드 목록/기존 Build 산출물 변경 없음. commit/push 없음.

## 검토에서 수정한 항목

- 높은 다음 spawn은 완료 직후 FallFloor를 올려 합법적 하강까지 되돌린다는 구현 결함을 발견하여 제거했다. 원래 저지대 다음 spawn을 유지한다.
- 모델/펌프/잎 Transform 변경에 PrefabUtility.RecordPrefabInstancePropertyModifications를 추가하여 사본 저장·재로드 시 override 유지 경로를 명시했다. ApplyPrefabInstance는 사용하지 않는다.
- 생성 실패는 부분 사본과 dirty scene을 보존하고 UTC+고유 suffix 실패 JSON을 남긴다. 재생성 자동 덮어쓰기/삭제 없음.
- 원본 Map SHA256 생성 전후 비교, 사본의 노드/공급/타깃/캡슐 여유 검사를 생성기에 추가했다. 이 Unity 검사는 아직 실행하지 않았다.
- 크기 조정된 도형의 로컬 좌표 margin이 안전 가장자리를 과도하게 제외하는 문제를 제거했다. 실제 지지 바닥 ray는 유지한다.
- 낮은 집수조·높은 수로에서 같은 물 노드에 FREEZE 허용. 사거리10m는 유지한다.

## 최종 검증·종료 범위

2026-10-16 21:43:54 KST 중간/최종 정리 시각 확인. 첫 검토 가능한 소스 산출물은 90분 목표보다 일찍 남았으나, 이는 통합 씬 완성이나 물리 진행 성공이 아니다.

최종 코드 동결 후 Roslyn 런타임34개/Editor26개 및 플레이어 조건 런타임34개 모두 오류·경고 없이 컴파일. GUID/meta/322개 기준 파일 보존 검사 PASS. Python 문법과 git diff 공백 검사 PASS. 실제 하위 에이전트의 최종 독립 검토 완료.

Unity 라이선스 IPC 차단으로 실험 씬 생성/열기/Play/빌드는 미수행이다. P3 기술적 실행 통과를 선언하지 않았으며 P5/P6·마지막 직접 PULSE·복원/스킵은 미통합으로 남긴다. 독립 가능한 소스 수정·검사 도구·문서 작업은 마쳤다. 사람 테스트 대기 때문에 종료한 것이 아니다. 전체 회차 목표인 실행 검증된 통합 프로토타입은 **미달성**이며, 정확한 재개 절차와 남은 결함을 HANDOFF에 명시했다.
