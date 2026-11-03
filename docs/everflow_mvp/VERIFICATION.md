# 검증 보고

## 2026-10-17 정리 점검 (최신)

이번 회차는 단일 에이전트로 수행했다. 시작 시 AGENTS.md를 상위 경로/프로젝트에서 찾았으나 없었고 Git status/diff와 HANDOFF를 우선 확인했다. 기존 미커밋 작업을 백업한 뒤 제한된 수정만 수행했다. 이전 통합 완료 여부는 MVP 씬 파일·249개 PASS 보고서·verified scene hash·빌드 파일로 재확인했으며 이번 실행 결과와 구분한다.

| 수준 | 이번 결과와 한계 |
|---|---|
| 정적 자산 검사 | `python -B tools/check_project_assets.py`: MVP 참조/대상/관리자/카메라/리스너53개 PASS. 전체 YAML의 local fileID, script GUID, meta 누락/중복/고아, 빌드 경로 오류0. **전체 pass=false**: `PC_Renderer.asset`의 기존 probe 디버그 리소스 GUID 7개 미해결. 보존한 미확정 문제이며 성공 처리하지 않음 |
| Profile 하위 자산 제거 | `cleanup-volume-profile.json`: 정상 목록 밖이며 외부 직렬화 참조가 없는 깨진 자산9개 제거. 정상 효과19개와 나머지 직렬화 블록 바이트, root component 목록, meta 보존. Unity 재임포트는 미검증 |
| 오프라인 C# 컴파일 | `cleanup-compile/compile-results.json`: runtime-editor37/editor29/runtime-player37/runtime-development37. 설치 Unity DLL 기반 Roslyn. 플레이어 조건에서는 Editor DLL 참조도 제거. 일반 릴리스 smoke 클래스 없음/개발 플레이어에 있음/두 플레이어 DLL의 UnityEditor 참조 없음 확인. Unity 자체 임포트/컴파일 결과와 구분 |
| validator 결함 회귀 | `cleanup-validator-tests.json`: 실제 `EFMvpSetup.Validate` 메서드 본문을 추출해 관리 코드 대체 객체로11개 PASS. 정상 구성, null finale/gate/제어부/체크포인트, 다른 grounding 월드, 누락 Renderer, 잘못된 공급/순환 조건. 실제 Unity PreviewScene 호출은 미검증 |
| 보존 검사 | `cleanup-preservation.json`: 작업 시작 SHA256 기준. 허용한 코드/설정/문서만 변경, 기존 씬·프리팹·모델·이미지·모든 meta·패키지·기존 증거 유지. 파일 이동/삭제0. 백업=`Backups/20261017-cleanup` |
| 이전 플레이어 실제 실행 | `cleanup-existing-player.json`: 기존 `Build/1.1.1/EVERFLOW.exe` headless 실행 종료0. 타이틀→MVP 진입과 두 번 재로드 PASS. **이번 수정 반영 빌드가 아니며** 화면/퍼즐 완주 확인과 다름 |
| 실행 환경 | `environment-20261017T104857846373Z.json`: 실제 프로세스 계정과 정상 사용자 환경 불일치. Local Editor Bridge status 파일 없음. 기존 라이선스 IPC 실패 실행을 반복하지 않았고 Editor/Hub/라이선스 프로세스를 종료하지 않음 |
| 수정 후 Unity 임포트/Play/새 빌드 | **미검증**. 저장·재로드, 타이틀 진입, 퍼즐/능력/추락, 마지막 PULSE→정상/스킵→반복 재시작은 정상 Editor에서 Run MVP Checks로 재검사해야 함. 이번에는 씬 바이트를 변경하지 않음 |

남은 렌더러 참조는 설치 URP의 `Runtime/Deprecated.cs`에 과거 probe 리소스 구조만 남아 있으며 현재 사용 여부를 실행 확인하지 못했다. 패키지/렌더러 교체나 임의 리소스 재연결을 하지 않았다. `verified.json`은 이전 runtime fingerprint이므로 이번 소스의 Play PASS 증거로 사용하지 않는다. 새 smoke 클래스는 Development Build에만 포함되므로 일반 릴리스에 `-everflow-mvp-smoke`를 주는 것으로 자동 검증/종료를 기대하지 않는다.

## 2026-10-17 통합 MVP 최종 결과 (이전 회차)

라이선스 IPC로 막혔던 샌드박스 배치 실행을 반복하지 않았다. 정상 사용자 Editor에 사용자가 활성화한 제한된 파일 연결을 사용했다. 이번 통합은 단일 에이전트가 수행했으며 실행 단계와 정적 검사를 아래처럼 구분한다.

| 검사 수준 | 실제 결과·증거 |
|---|---|
| 소스 컴파일/정적 보존 | `python -B tools/verify_mvp_experiment.py`: runtime-editor37/editor29/runtime-player37, 오류0, GUID/meta PASS. 설치 Unity DLL 기반 Roslyn 검사이며 이것만으로 Play를 주장하지 않는다. `evidence/compile-results.json`, `asset-checks.json` |
| P3 씬 생성/저장/접근 수리 | 기존 `Everflow_Map_P3_DesignTest_20261016` 재사용. 계단49개 Transform 수리/저장·재로드와 byte 백업. `evidence/resume-20261017/p3-access-repair-20261017T043626193Z-16390a32.json` |
| P3 실제 Editor Play | `evidence/p3-checks-20261017-064730-116.txt`: Editor2개/runtime77개 PASS. 실제 콜라이더 이동·조준/키/클릭, 펌프→수문 경과 프레임→물/얼음, 출발대, 잎 캡슐 이동, 보조 완료/낙하 유지/재시작을 구분 기록 |
| MVP 생성/저장/재로드/참조 | 실제 `Assets/Scenes/Everflow_Map_MVP.unity` 생성. `evidence/integration-20261017/scene-created.txt`. 전체 루트의 Missing Script 및 World/Stage/최종 제어부/카메라 참조 검증 PASS. 이후 세 표지만 byte 백업 후 수리·저장했으며 재오픈은 idempotent |
| MVP 실제 Editor Play | **249개 PASS**, `evidence/integration-20261017/mvp-checks-20261017T070952791Z.txt`. 타이틀→꽃/덩굴→잔해/석판→회전 A/B→수로/잎/출구 하행→뿌리·문→최종 접근/정비/안전 연결/횡단→별도 마지막 PULSE→복원→새 게임. 실제 CharacterController·레이/키/클릭/탑승을 사용. 수로 진입의 의도적인 R/낙하 복구 검사 포함 |
| 오입력·우회·스킵·초기화 | 같은249개 보고서: 준비·도달만으로 엔딩 없음, 최종 오입력/중복 입력 차단, 먼 가장자리 접촉 우회 차단, 정상/초반 **Space 입력**/후반 공통 완료 API의 최종 상태 일치, 연출 중 예외 낙하/재시작, 탐색 복귀 후 카메라·입력 복구, 반복 새 게임. 비교 준비는 API/위치 fixture라고 명시. 연속 물레방아 각도/경과 시간은 최종 상태 비교에서 제외 |
| 실제 화면 검토 | 같은 실행의 `*-title/start/rotary/canal/final-control/ending.png`, 이전 `070816332Z` 캡처도 확인. 타이틀/HUD 표시, 새 표지의 거울·크기 문제 수정과 복원 조망 확인. 모든 화면 비율·시점이나 초심자 가독성 검증은 아님 |
| 타이틀/빌드 씬 연결 | `EFTitleScreen.GameplayScene`=MVP, Title/MVP 첫 두 활성 씬, 원본과 유틸리티 비활성 보존. `published.txt`와 `ProjectSettings/EditorBuildSettings.asset`. 빌드 후 `mvp-checks-20261017T071237305Z.txt` **4개 PASS**, v1.1.1 타이틀 캡처 |
| 실제 Windows 빌드 | 정상 Editor `mvp-build`→기존 `EFReleaseBuild.BuildWindows`: **Succeeded / 1.1.1 / Errors0 / Warnings485**. `Build/1.1.1/build-result.txt`, `evidence/integration-20261017/latest-build.txt`. ProjectSettings diff는 bundleVersion 1.1.0→1.1.1 한 줄 |
| 실제 빌드 실행 | `Build/1.1.1/EVERFLOW.exe -batchmode -nographics -everflow-mvp-smoke -logFile ...`: 종료0, 타이틀/MVP·두 번 재로드 모두 PASS. 정확한 명령/결과=`evidence/integration-20261017/player-smoke-1.1.1.json`, 실제 로그=`player-smoke-1.1.1.log`. 화면 없는 실행이며 빌드의 전체 물리 완주·렌더링 확인과 구분 |
| 최종 보존 | `evidence/integration-20261017/final-preservation.json`: 통합 전332개 기준, 예상 외 변경0, 원본 Map/P3 및 공유 자산/이미지/HUD 보존, 최종 MVP 저장 hash=검증 stamp. 마지막 `mvp-create`로 Play를 끈 MVP 편집 씬 재오픈, `final-editor-status.json` |

빌드 경고485개는 기존 `com.unity.ai.inference`의 ConvGeneric/Sentis 셰이더 경고485줄과 대응한다(`build-log-triage.json`). 패키지/셰이더/보호 설정을 변경하지 않았고 다른 GPU에서의 영향은 미검증이다. Player 로그의 Unity 원격 설정/수집 서버 접속 `Curl error7` 두 건은 남아 있으나 이 실행의 게임 진입·초기화/종료 검사는 PASS했다.

실패 증거도 보존했다: `065533113Z`(스킵 fixture 능력 선택), `070002817Z`(움직이는 잔해 조준 좌표), `070047806Z`(석판 정렬 경계의 검사 지연). 실제 입력/중앙 정렬 검사로 수정 후 순차 재실행했으며 충돌을 끄거나 진행 조건을 제거하지 않았다. 첫134/220개 보고서의 앞 구간 fixture 또는 오래된 미검증 문구는 최종249개 보고서와 혼동하지 않는다.

**미검증:** 빌드에서의 화면 렌더링·6개 퍼즐 수동 완주, 모든 임의 우회/해상도/마우스 조작, 실제 초심자 이해도·재미·목표8~9분. 자동 검사 시간은 플레이타임 측정값으로 사용하지 않는다. 자체 diff 검토는 REVIEW 참조. 아래는 이전 시점의 역사 기록이다.

## 2026-10-17 재개 시 확인한 실제 결과

**실험 씬은 생성되어 있고 Editor Play 검사도 일부 실행되었다. P3 전체는 FAIL 상태이며 수정 후 재실행은 대기 중이다.** 아래 첫 회차의 '미생성/전체 미실행' 기록을 현재 상태로 읽지 않는다.

| 검사 구분 | 현재 증거/결과 | 한계 |
|---|---|---|
| 씬 생성·저장 | `Assets/Scenes/Everflow_Map_P3_DesignTest_20261016.unity` 실제 파일, 2026-10-16 22:28:50 KST 저장 | 새 통합 완료본/빌드가 아님. 이번 재개에서 덮어쓰지 않음 |
| 씬 재오픈·참조 검사 | `p3-checks-20261016-132900-485.txt`의 [EDIT] 2개 PASS | 노드/카메라/안전 옵션/보호 파일 검사. 실제 이동 성공과 다름 |
| Editor Play 상태/API fixture | `p3-checks-20261017-041754-665.txt`: runtime30개 PASS 후 FAIL | 최초17개 및 지지 회귀6개 등을 포함. API final sample은 실제 시간 애니메이션 검사와 다름 |
| 출발대 실제 충돌 fixture | 수정 후 **PASS**: 실제 grounded 지지와 CP | 원래 첫 hit=동높이 마지막 계단, 지정 데크와 거리 동일. 6개 격리 회귀 fixture도 PASS |
| 잎 물리 이동 구간 | 실제 탑승·캡슐 이동·종점 도달 **PASS**, 종점 고정 데크 지지 **FAIL** | 종점에서 아직7cm 높은 잎을 밟고 있음. 실제 하차 보행 후 검사하도록 테스트 수정, 재실행 대기 |
| 완료/낙하/반복 초기화 후반 | 실패 지점 이후 **미실행** | 초기 상태/API 일부 PASS와 구분 |
| 최신 실행 환경 진단 | 별도 샌드박스 계정/정상 사용자 환경 변수 차이 유지, CLI live 연결 불가 | 정상 사용자 Editor의 라이선스 연결 성공 로그가 있어 만료로 단정하지 않음. 실패 배치 반복 없음 |
| 런처 보호 회귀 | `python -B tools/test_unity_launch_guard.py`: **10개 PASS** | 순수/모킹 검사. 계정 차이·기존 잠금·다른 버전 Editor·동시 helper·직전 재검사에서 Popen 미호출. Unity 실행 성공 아님 |
| 수정 후 C# 컴파일 | 런타임34개/Editor27개/플레이어 조건 런타임34개 오류·경고0 | 실제 접근 검사까지 포함한 최종 소스. 설치 DLL 기반 Roslyn 검사이며 수정 후 Unity 임포트/Play 결과 아님 |
| 실제 저장 씬 정적 참조 | `resume-20261017/saved-scene-20261017T041552400298Z.json`: **15개 PASS**, 직렬화 블록1043개 | 로컬 fileID·Transform 부모자식·script GUID·공급 참조·옵션·보호 파일 해시. Unity 물리 검사 아님 |
| 실제 계단 접근 | `p3-checks-20261017-043005-587.txt`: 첫 ridge PASS, 첫 새 계단 입구 **FAIL** | runtime12개 PASS. 첫 턱≈0.75m > stepOffset0.55m. 이후 조준/후반/재시작 미실행 |
| 최신 Unity 자체 컴파일 | **CS0103 해소**, 실제 Play 진입 | helper를 기존 Tests에 병합한 뒤 13:30 보고서 생성. 과거 오류 증거는 보존. 그 뒤 접근 수리/bridge 소스는 별도 컴파일 PASS, 실제 실행 대기 |
| 수리 저장 | **실제 완료**, 49개 계단 Transform | `resume-20261017/p3-access-repair-20261017T043626193Z-16390a32.json`, 기존 사본 byte 백업/Undo/원본 자산 보존 |
| 수리 저장 후 정적 비교 | **17개 PASS**,1043개 블록 | `saved-scene-20261017T043633822679Z.json`: 변경은 두 생성 계단의 Transform 위치/회전/크기만. 재오픈 Play와 구분 |
| 실제 Editor bridge 거부 검사 | **4개 PASS** | 잘못된 세션/만료/미허용 명령/순서 재사용을 실제 거부하고 게임 명령 미실행, 보호 파일 보존. `editor-bridge-negative-20261017T043623289465Z.json` |
| 수리 후 실제 이동 / 전체 횡단 / 빌드 | **미검증** | sequence4 검사 접수와 Play 도메인 재로드까지 관측, terminal 보고서 미생성. 전체 시작→종료와 빌드 별개 |

기획 역할/UI/index/대상 이름 대응은 HANDOFF의 표를 따른다. 수로는 기획 P3+P4 / 실제 UI P4 / index3 / `SM_IceSlide`이며, 회전 연결기는 기획 P5 / UI P3 / index2 / `SM_RotatingBridge`다. 실제 UI P5/index4는 `SM_VineRoot_P5`·`SM_Door_P5`다.

새 검증 도구의 동높이 중첩, 5mm 차이, 12cm 높은 다른 바닥, 아래층, 공중, 안전 가장자리 6개 격리 충돌 fixture는 두 번째 실제 Play 보고서에서 통과했다. 최신 접근 검사는 기존 spawn부터 실제 이동 입력을 사용하고 자동 카메라 조준과 실제 Player 키/클릭 경로를 거친다. 아직 이 추가 검사가 실행됐다는 보고서는 없다. 실패 이후 미실행 항목을 성공 개수에 포함하지 않는다.

## 2026-10-16 첫 회차 검증 기록 (아래는 당시 상태)

이번 회차의 새 증거만 아래에 기록한다. 기존 `docs/map-integration` / `docs/ability-validation`의 과거 PASS를 새 실험의 실행 결과로 재사용하지 않는다.

## 실제 수행

| 구분 | 결과 | 증거와 한계 |
|---|---|---|
| 기준 문서/실제 코드 조사 | 완료 | `00_baseline.md`; 실제 stage 대응과 기존 최종 자동 시작 확인 |
| 원본 보존 SHA256 | PASS | `evidence/preserved-baseline.json`, `preservation.json`; 시작 시 Assets/ProjectSettings/Packages 322개 파일. 허용 변경은 World/Node 소스 두 개뿐 |
| C# 컴파일 | PASS: 런타임34개/Editor26개, 플레이어 조건 런타임34개. 오류·경고0 | `python tools/verify_mvp_experiment.py`; 설치 Unity 6000.3.21f1 및 프로젝트 DLL을 참조한 Roslyn. 런타임 Editor 정의, Editor 소스, UNITY_EDITOR 없는 플레이어 정의를 각각 컴파일. **Unity 임포트/실제 플레이어 빌드가 아님** |
| GUID/meta 검사 | PASS | `asset-checks.json`; 새 스크립트 meta 및 Assets 내 GUID 중복 검사. 저장된 실험 씬 참조 검증은 아님 |
| Python 도구 문법 | PASS | `python -m py_compile tools/verify_mvp_experiment.py tools/run_mvp_experiment_unity.py`; 도움말 실행. Unity 동작 검증 아님 |
| 변경 diff 공백 검사 | PASS | `git diff --check`; Unity 저장과 관계없음 |
| 독립 코드 검토 | 수행 | `REVIEW.md`; 구현 담당과 다른 에이전트가 검토 |
| Unity 배치 실행 가용성 | 실패/중단 | `unity-baseline.log`, `unity-execution-status.json`; licensing IPC refused/60.01초 timeout/74.81초 initialization failed. 해당 시도 PID만 종료 |

## 수행하지 못한 검사

Unity가 에셋 임포트와 executeMethod까지 진행했다는 증거가 없어 다음은 모두 **미실행/미검증**이다.

- 실험 씬 실제 생성·열기·저장 및 생성기의 Physics 기반 참조/충돌 검사.
- 실제 정상 수로 횡단, 낮은 집수조/높은 출발대의 카메라 조준·시야·10m 사거리·가림.
- 출구 가장자리 착지, 출발대/수로 추락, 아래 원래 지형 착지, 재진입, 낙하 복구 볼륨 경계.
- 보조 완료 후 실제 통행 유지, 반복 FREEZE/PULSE/R, 완료 회귀·중복 이벤트 부재.
- Editor Play 새 게임·반복 3회 재시작·타이틀/HUD/카메라 회귀.
- P5/P6 최소 통합, 최종 직접 PULSE, 정상 복원/초반·후반 스킵의 동일 최종 상태.
- Windows 플레이어 빌드 생성 및 그 실행. 기존 Build 폴더를 새 결과로 보고하지 않는다.
- 사람이 수행한 초심자 이해도·재미·완주율·8~9분/5~6분 플레이 시간.

## 판정

**P3 소스·설정 도구를 검토할 수 있는 단계이며, 실행 검증을 통과한 통합 프로토타입은 아니다.** 라이선스 환경 차단을 우회해 씬 YAML을 만들거나 후속 퍼즐을 무리하게 연결하지 않았다. P3 실행 통과 전 P5/P6 및 복원·스킵 통합은 보류한다. 새 기능 확대 대신 컴파일·독립 검토·보존 검사·실행 절차를 남겼다.

## 이어서 실행할 검사

1. Unity 라이선스가 정상인 환경에서 컴파일 완료 후 `Tools > EVERFLOW > MVP Experiment > Create P3 Copy`.
2. 생성된 씬에서 `Tools > EVERFLOW > MVP Experiment > Run P3 Checks`. 이 도구의 fixture 기반 상태 검사와 실제 횡단 검사를 구분해 보고서를 읽는다.
3. 같은 실험 씬을 직접 플레이해 위 미검증 항목을 확인한다. 원본 Apply Integration/Apply Presentation은 사용하지 않는다.
4. P3 진행 차단이 없음을 확인한 뒤 후속 P5/P6와 최종 입력/복원/스킵을 통합하고 빌드한다.

CLI는 `python tools/run_mvp_experiment_unity.py create` 및 `test`. 이미 실행 중인 Editor를 종료하지 않고 거부하며 자체 실행 PID에만 시간 제한을 적용한다. 이번 회차에서는 동일 라이선스 실패 명령을 반복하지 않았다.
