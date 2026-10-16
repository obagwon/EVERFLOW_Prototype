# 전시 실행·배포 흐름 통합 결과

2026-10-16. 총괄이 범위와 완료 기준을 확정하고, 구현 담당과 별도 검증 담당에게 순차 위임했다. 구현 완료 후 제품 코드 편집을 중단한 상태에서 독립 검증했다. 검증에서 확인된 제품 코드 결함은 없었으며, 결함 반환에 따른 수정·재검증은 0회다(허용 최대 2회). Unity 실행 검증은 아래 환경 문제로 완료하지 못했다.

추천 기록은 Graybox 통합을 제안했으나 이후 사용자 작업은 `Everflow_Map`을 실제 게임 씬으로 지정했다. 현재 Map 연결을 보존했다. 타이틀 시작, 전용 Release 빌드의 대상 씬, Map 엔딩의 전체 재시작과 타이틀 복귀는 이미 구현되어 있었으며, 이번 작업은 남아 있던 배포 검증의 누락을 보완했다. 새로운 퍼즐이나 런타임 기능을 추가하지 않았다.

변경 파일과 동작:

| 파일 | 변경 |
|---|---|
| `Assets/EverflowMVP/Editor/EFReleaseSetup.cs` | Release 씬 목록 생성·검사 공통화. Map이 없는 게임 씬을 옛 MVP로 대신 인정하던 fallback 제거. Map 노드·복원·물 공급·최종 수로·물레방아·별도 엔딩 카메라 참조 검사 강화. P3의 빈 복원 목록 허용 유지. |
| `Assets/EverflowMVP/Editor/EFReleaseBuild.cs` | 일반 Build/Build And Run에서도 Title→Map 순서·중복 검사와 저장된 씬 참조 검사를 실행. 추가 검증 씬이 활성화된 기존 설정도 허용. 기존 버전 트랜잭션 유지. |
| `Assets/EverflowMVP/Editor/EFReleaseTests.cs` | 반복 씬 목록 구성, 원본 항목 보존, 누락·역순·중복·MVP 대체 거부, preview 씬의 엔딩 참조 오류 주입 검사를 추가. |
| `Assets/EverflowMVP/Editor/EFReleaseIntegrationVerification.cs` 및 `.meta` | 독립 검증용 Unity 배치 진입점. Configure와 씬 저장을 호출하지 않음. |
| `tools/verify_release_integration.py`, `tools/verify_release_logic.py`, `tools/verify_release_unity.py` | 파일 보존·3종 컴파일, 실제 함수 본문을 추출한 관리 코드 검사, 제한 시간 있는 Unity 배치 실행 도구. |
| `docs/release-integration/*` | 범위, 시작 시점 해시, 구현 기록, 독립 검증 결과와 실행 로그. |

Release Configure의 기존 정책대로, 실행 시 Title과 Map을 첫 두 활성 씬으로 지정하고 다른 씬 항목은 목록에 비활성 상태로 보존한다. 기존 타이틀 씬은 재생성하지 않는다. 이번 작업에서는 Configure 자체를 실행하거나 사용자의 저장된 씬 목록을 변경하지 않았다.

실제 수행한 검사:

| 검사 | 결과와 범위 |
|---|---|
| Unity 6000.3.21f1 설치 DLL을 참조한 Roslyn 컴파일 | runtime-editor 33개, editor 24개, runtime-player 33개 파일 모두 exit 0, 진단 0. Unity 자체 임포트나 플레이어 빌드 결과가 아님. |
| 독립 관리 코드 검사 | 44개 PASS. 실제 제품 함수 본문으로 현재 Title/Map/MVP 활성 설정 허용, 반복 목록 구성 10회, 순서·중복·누락·이전 씬 대체 거부, 버전 성공·실패·취소·예외 롤백 등을 실행. 최소 자료형 대체를 사용했으므로 Unity API/Configure 부작용 검증은 아님. |
| 시작 시점 SHA-256 비교 | 기준 388개 파일 중 허용한 제품 Editor 코드 3개만 변경. 사용자 씬·프리팹·이미지·FBX·패키지·프로젝트 설정은 동일. 앱 버전 `1.1.0` 유지. 신규 검증 파일은 기준 비교와 별개로 추가됨. |
| 코드 검토 | 타이틀 시작과 Release의 공통 Map 대상, Map의 엔딩→재시작/타이틀 복귀, 기존 Single 씬 로드 경로 확인. 실제 플레이 동작의 정상 여부는 미확인. |
| `git diff --check` | 수정한 제품 코드 3개에서 공백 오류 없음. |
| 실제 Unity 배치 실행 | 실패/미완료. 아래 사유로 검증 진입점에 도달하지 못함. |

Unity 실행 근거:

- 설치된 `6000.3.21f1/Editor/Unity.exe`를 `-batchmode -nographics -quit -executeMethod EverflowMVP.Editor.EFReleaseIntegrationVerification.Run`으로 실행했다.
- 시작 전 실제 Editor 프로세스는 없었다. 기존 PID 29300은 Unity Hub였고 종료하지 않았다.
- `verification-unity.log`에 `Connection to channel LicenseClient-vzd12 refused`, `Licensing initialization failed after 74.81s`, 라이선스 연결 손실이 기록되었다.
- 총 90초 제한 후 작업이 시작한 Editor PID 19960만 종료했다. `verification-unity-results.json`이 생성되지 않아 Unity 검사를 통과했다고 판단하지 않았다.
- 이전 작업의 Unity PASS 기록을 이번 결과로 사용하지 않았다.

미확인: Unity 임포트·실제 Editor 회귀 검사 실행, 전체 Play Mode, 사람의 마우스/키보드 조작, 실제 반복 Configure의 프로필·에셋 부작용, Windows 빌드 및 실행 파일의 타이틀→맵→엔딩→복귀 흐름. 기존 플레이어 이동·카메라·퍼즐·UI가 정상이라고 확정하지 않는다.

Unity에서 이어서 확인할 순서:

1. Unity Hub에서 라이선스를 정상화하고 이 프로젝트를 6000.3.21f1로 연다. 임포트·컴파일 완료 후 Console 오류를 확인한다.
2. `Tools > EVERFLOW > Release > Run Release Integration Tests`를 실행한다. 저장된 타이틀/Map 참조 검사와 새 회귀 검사가 모두 PASS인지 확인한다. 이 검사는 Play 검증과 별개다.
3. 작업 중인 씬을 저장한 뒤 `Release > 1. Set Up Title and Windows Settings`를 두 번 실행한다. 활성 Build Profile/Build Settings의 첫 두 씬이 Title→Map인지, 다른 검증 씬 항목은 비활성 상태로 남는지, 타이틀 UI가 중복되지 않는지 확인한다.
4. `Tools > EVERFLOW > Presentation > Run UI and Passage Regression Tests`, `Tools > EVERFLOW > Map > Run Physical and State Tests`, `Run Third Person Camera Tests`를 실행한다. 자동 입력/위치 fixture와 사람의 실제 조작은 구분한다.
5. Title 씬에서 Play하여 게임 시작 연속 클릭, Map 진입 후 커서·시점·이동과 6개 퍼즐을 직접 확인한다. 최종 물 전파·물레방아·엔딩 후 Restart world로 진행/연출이 초기화되는지, Title→게임 시작으로 새 게임이 시작되는지 각각 확인한다.
6. Windows x86_64에서 `Release > 3. Build Windows Release (Next Patch)`로 새 산출물을 만든다. 현재 버전은 1.1.0이므로 성공 시 1.1.1이 된다. 실행 파일에서도 위 흐름과 UI·아이콘을 확인한다. 전용 메뉴의 성공한 배포만 버전을 증가시키며 일반 Build/Build And Run은 증가시키지 않는 기존 정책을 유지한다.

근거: `verification-results.json`, `verification-compile-results.json`, `verification-logic-results.json`, `verification-preservation-results.json`, `verification-unity-launch.json`, `verification-unity.log`. 독립 담당의 상세 판정은 `verification.md`에 있다.
