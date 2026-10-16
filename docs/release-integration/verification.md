# 독립 검증 보고

검증일: 2026-10-16. 구현 담당과 별도 에이전트가 검토·검사를 수행했다. 최종 구현 전달 후 제품 코드 결함은 발견하지 않았고, 수정·재검증 반환은 **0회**다. 아래 컴파일·관리 코드 검사는 통과했으나 Unity 실행 검사는 초기화 단계에서 중단되어 전체 플레이 동작을 정상이라고 확정하지 않는다.

## 요구사항과 변경 대조

| 요구사항 | 코드 검토 결과 | 실제 실행 확인 수준 |
|---|---|---|
| 타이틀 → 현재 사용자 Map | 기존 `EFTitleScreen.GameplayScene`은 `Assets/Scenes/Everflow_Map.unity`. StartGame의 Single 비동기 로드와 중복 시작 방지는 보존됨. Release build도 같은 상수를 사용함. | 경로/코드 확인. 실제 버튼 클릭·씬 전환은 미검증. |
| 반복 Release 설정에도 같은 Map | Configure가 추출된 `CreateReleaseScenes`를 사용. Title/Map을 앞에 한 번씩 배치하고 기존 utility 항목은 순서대로 disabled 보존. | 실제 생산 함수 본문을 추출한 관리 코드 검사에서 10회 반복과 입력 불변성 통과. native Configure 및 이미지 importer/PlayerSettings 효과는 미실행. |
| 현재 저장된 일반 Build 설정 보존 | `ValidateBuildScenes`는 첫 두 enabled Title/Map 및 중복 여부를 검사하며 추가 enabled utility는 허용. 현행 Title/Map/MVP 목록을 거부하지 않음. | 현재 YAML 목록을 입력으로 생산 검증 함수 실행 통과. Unity Build guard 자체는 미실행. |
| Map 플레이어·퍼즐·물레방아·엔딩 참조 | MVP 타입 대체를 제거하고 `EverflowMap.EFMapWorld/EFMapPlayer` 참조를 검증. 6개 stage, spawn/arrival/nodes/requirements/restoration, gate/sideWater, wheel/finalFlow/별도 endingCamera 검사. P3 빈 restoration 목록 허용. | 코드 검토 및 Unity DLL 대상 컴파일. Unity preview-scene 검사는 진입하지 못함. |
| 엔딩 → Restart/Title | 기존 `EFMapWorld`의 P6 조건→물 전파→wheel→Ended 흐름 보존. RestartWorld는 현재 씬 path를 Single 재로드, ReturnToTitle은 기존 타이틀 path를 Single 로드함. | 기존 런타임 파일이 baseline과 동일하며 분기 코드 검토. 실제 엔딩·재시작·복귀 미검증. |
| 사용자 수정/Graybox/MVP/에셋/설정 보존 | baseline 388개 파일 중 허용한 Editor 3개만 변경. 씬·에셋·런타임·ProjectSettings·Packages는 작업 전 바이트와 동일. | Unity 시도 종료 후 SHA256 재검사 통과. |

## 실제 수행한 검사

1. `python tools/verify_release_integration.py --compile --allow Assets/EverflowMVP/Editor/EFReleaseSetup.cs --allow Assets/EverflowMVP/Editor/EFReleaseBuild.cs --allow Assets/EverflowMVP/Editor/EFReleaseTests.cs`
   - runtime-editor 33개, editor 24개, UNITY_EDITOR를 제거한 runtime-player 33개 스크립트 모두 exit 0, 컴파일 진단 없음.
   - Unity 6000.3.21f1 Roslyn과 로컬 프로젝트 DLL 참조 사용. Unity import나 Windows player build 결과가 아니다.
   - baseline 388개 중 위 허용 파일 3개만 변경, 예기치 않은 변경 0개. Unity 시도 후 동일 결과 재확인.
   - 근거: `verification-compile-results.json`, `verification-preservation-results.json`, `verification-results.json`, `verification-compile/*-compile.txt`.
2. `python tools/verify_release_logic.py`
   - 생산 코드에서 `CreateReleaseScenes`, `ValidateBuildScenes`, `Require`, `NextVersion`, `Transaction` 본문을 그대로 추출하여 .NET에서 실행. scene-entry와 BuildResult 타입만 최소 관리 코드로 대체.
   - **44개 PASS**, 컴파일/실행 exit 0. 현재 저장 목록, Title/Map 중복 정리, utility 순서와 disabled 보존, 입력 불변, 10회 반복, 빈/누락/역순/legacy/중복 거부, 추가 enabled utility 허용을 검사했다.
   - 버전 후보 반영, 성공 commit, 실패/취소/unknown rollback, build 예외 및 commit 예외 rollback 검사 통과.
   - Unity native API, 실제 Configure, 씬 참조, Play Mode를 대체 검증한 결과가 아니다.
   - 근거: `verification-logic-results.json`, `verification-logic/Program.cs`, `verification-logic/execution.txt`.
3. 편집한 Editor 파일에 `git diff --check` 수행: exit 0, 공백 오류 없음. Git의 LF→CRLF 안내만 발생.
4. 단일 Unity Editor batch 실행 시도: **시작 성공, 검사 진입 실패**.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Unity.exe' -batchmode -nographics -quit -projectPath 'C:/Users/vzd12/EVERFLOW_Prototype' -executeMethod EverflowMVP.Editor.EFReleaseIntegrationVerification.Run -logFile 'C:/Users/vzd12/EVERFLOW_Prototype/docs/release-integration/verification-unity.log'
```

실제 재현 도구는 `python tools/verify_release_unity.py`다. 새 Editor PID 19960만 실행했고 총 90.08초 후 그 프로세스만 종료했다. 기존 PID 29300은 Editor가 아닌 Unity Hub `resources/unity.exe`이며 종료하지 않았다. 로그에는 이번 실행의 다음 오류가 기록됐다.

- `Connection to channel LicenseClient-vzd12 refused`
- `Error: Timed-out after 60.01s, waiting for Licensing to initialize`
- `Error: Licensing initialization failed after 74.81s`
- 이후 Licensing Client 재접속 시도 중 제한 시간 도달. MonoManager ReloadAssembly 단계까지 진행했으나 검증 진입점의 PASS/FAIL 및 `verification-unity-results.json`은 생성되지 않았다.

근거: `verification-unity-launch.json`, `verification-unity.log`. 검증 진입점은 현재 effective Build Settings guard, preview-scene 참조 검증, `EFReleaseTests.RunIntegration`을 호출하도록 컴파일되었지만 **이번 Unity에서 실행되었다고 주장하지 않는다**. Configure/Map Apply/씬 저장/실제 빌드는 호출하지 않았다.

## Unity에서 이어서 확인할 절차

1. Unity Hub 라이선스 연결을 정상화하고 Unity 6000.3.21f1에서 프로젝트를 연다. import/컴파일 완료 후 Console 오류를 확인한다. 현재 작업을 저장한 뒤 아래 검사를 실행한다.
2. `Tools > EVERFLOW > Release > Run Release Integration Tests` 실행. 버전 트랜잭션, 반복 씬 목록, 타이틀/Map 저장 참조, preview 내 잘못된 참조 거부 검사 PASS를 확인한다. 이 검사는 Play나 실제 빌드를 하지 않는다.
3. `Tools > EVERFLOW > Release > 1. Set Up Title and Windows Settings`를 두 번 적용한다. Build Profiles에서 enabled 첫 두 씬이 Title, Everflow_Map이며 중복이 없고 기존 utility 항목은 disabled로 남는지 확인한다. 타이틀/Map의 씬 파일과 이미지 배치가 바뀌지 않는지 확인한다. 이것이 아직 미실행인 실제 Configure 반복 검사다.
4. 타이틀 씬을 직접 열고 Play. 시작 버튼 연속 클릭 시 Map에 한 번만 진입하는지, 인트로 후 이동/카메라/능력 입력과 커서 상태가 맞는지 확인한다. P1~P6를 진행하고 최종 수로 조건 충족→물 전파→물레방아 회전→엔딩 카메라를 확인한다. 엔딩 `Restart world`가 Map의 P1 초기 상태로 돌아오는지, 재진행 후 `Title`→시작이 새 Map 세션을 만드는지 확인한다.
5. 자동 회귀는 `Tools > EVERFLOW > Map > Run Physical and State Tests` 및 `Tools > EVERFLOW > Presentation > Run UI and Passage Regression Tests`를 실행할 수 있다. 이 기존 도구들은 Play를 시작하고 기존 evidence 경로에 결과를 기록한다. 사람의 마우스 조작·실제 Windows 플레이어 검사와 구분한다.
6. 필요 시 Windows 플랫폼에서 일반 Build/Build And Run으로 검증 guard 통과와 실제 타이틀→Map→엔딩→Restart/Title을 확인한다. 배포용 `3. Build Windows Release (Next Patch)`는 성공 시 버전을 올리므로 실제 배포 빌드가 필요할 때 사용한다. 이번 작업에서는 빌드/버전 증가를 수행하지 않았다.

## 변경 파일

제품 변경은 `EFReleaseSetup.cs`, `EFReleaseBuild.cs`, `EFReleaseTests.cs` 세 파일이다. 독립 검증용 신규 파일은 `Assets/EverflowMVP/Editor/EFReleaseIntegrationVerification.cs`와 `.meta`, `tools/verify_release_integration.py`, `tools/verify_release_logic.py`, `tools/verify_release_unity.py`, 이 보고서 및 `verification*` 증거 파일들이다. 프로덕션 코드 수정은 구현 담당만 수행했다.

미확인: Unity 에셋 import 완료, preview 참조/오류 주입 테스트, native Configure 반복, 기존 이동·카메라·능력과 UI 실제 동작, 물레방아 엔딩/Restart/Title 플레이, Windows player build와 실행. Unity 라이선스 초기화 제한이 남았으므로 이 항목들은 정상 판정을 보류한다.
