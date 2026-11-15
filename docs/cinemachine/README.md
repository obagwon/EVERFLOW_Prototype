# Everflow_Map_v2_test Cinemachine 교체

대상 씬의 기존 Main Camera를 유지하고 Cinemachine 3.1.7로 플레이 카메라를 교체했다. 패키지 manifest/lock의 기존 사용자 변경은 보존했으며 다른 씬, 플레이어 프리팹, 프로젝트 입력 설정은 수정하지 않았다.

## Inspector 위치

| Hierarchy | 확인할 설정 |
| --- | --- |
| `Main Camera` | 기존 Camera, MainCamera 태그, AudioListener, URP Additional Camera Data, Cinemachine Brain. Brain은 Late Update / Cut. |
| `EFMapPlayer/CameraTarget` | 플레이어 루트 자식. 로컬 `(0, 0.65, 0)`. 중앙 피벗의 높이 2m 캡슐 기준 가슴 위치. 본을 추적하지 않는다. |
| `FreeLook Camera` | Cinemachine Camera, Orbital Follow, Rotation Composer, Input Axis Controller, Deoccluder, EFMapCinemachineRig. |
| `EFMapPlayer` | View는 기존 Main Camera, Follow는 None, Cinemachine Rig는 FreeLook Camera. 씬 인스턴스 오버라이드. |

Orbital Follow는 World Space, 반경 6m, 가로 360° Wrap, 세로 -30°~65°, 모든 자동 리센터링 비활성화다. 시작 각도는 yaw 180° / pitch 18°. 위치 Damping은 `(0.15, 0.22, 0.15)`이며 Rotation Composer Damping은 `(0.05, 0.08)`이다. 캐릭터는 화면 중심보다 약간 왼쪽 아래에 배치한다. 실제 플레이에서 감도와 구도에 대한 체감 조정은 확인하지 못했다.

`V2Camera.inputactions`의 `Player/Look`은 `<Mouse>/delta`다. `V2CameraLook.asset`을 가로·세로 입력에 함께 연결하고 Gain `0.14` / `-0.119`, Cancel Delta Time 활성화, 입력 가감속 0으로 설정했다. 기존 입력 설정은 Both지만 중복 입력을 피하도록 이 컨트롤러의 Legacy Input은 비워두었다. 다른 씬의 기존 `InputSystem_Actions.inputactions`는 수정하지 않았다.

Deoccluder는 Pull Camera Forward, 반경 0.25m, 장애물 진입 Damping 0 / 복귀 Damping 0.35 / Smoothing 0.05다. 장애물 마스크는 실제 지형의 Default 레이어(1)다. 플레이어의 Ignore Raycast 레이어(2)와 물·UI·FX는 제외한다. 기존 탐색 호스트의 Awake가 지정된 지형 MeshCollider를 생성하는 경로를 유지한다. 지형 에셋이나 Collider 설정은 변경하지 않았다.

## 제거 및 연결 변경

- 대상 씬 Main Camera의 `EFMapCamera`를 제거했다. 이 컴포넌트가 담당하던 직접 위치·회전 쓰기, 마우스 회전, SphereCast 충돌 보정, 탑승 중 자동 시점 보정이 대상 씬에서 사라졌다. 스크립트 파일은 다른 씬을 위해 보존했다.
- Main Camera의 커스텀 Transform 제어 스크립트는 남아 있지 않다. 최종 위치·회전은 Cinemachine Brain이 적용한다.
- `EFMapCinemachineRig`는 입력 일시정지·커서 재캡처 직후 입력 버림·리스폰 시 Cinemachine 상태 초기화만 연결한다. 실제 Camera Transform을 직접 수정하지 않는다.
- 이동은 기존 `EFMapPlayer`의 실제 `view.transform.forward`를 수평면으로 투영하는 경로를 사용한다. 기존 능력 코드의 View 참조도 같은 실제 Camera다.
- 기존 탐색 검증 도구가 삭제된 follow 카메라를 참조하던 경로는 새 Play 검사로 분기했다.

## 검증 결과와 한계

Unity 6000.3.21f1 배치 실행을 시도했으나 Licensing Client 연결 거부, 초기화 시간 초과 및 연결 끊김으로 에디터 초기화가 진행되지 않았다. 작업에서 시작한 에디터 프로세스는 종료했다. Unity 안에서 씬 저장 후 재열기, 에셋 재임포트, Play Mode 실행은 완료하지 못했다. 씬은 직렬화 파일로 저장하고 디스크에서 다시 읽어 검사했다.

라이선스 로그의 관련 메시지:

```text
Connection to channel LicenseClient-vzd12 refused
Error: Timed-out after 60.00s, waiting for Licensing to initialize
Error: Licensing initialization failed after 74.86s
Error: The connection with the Unity Licensing Client has been lost.
```

Unity 설치에 포함된 Roslyn 컴파일러와 프로젝트의 기존 Bee 응답 파일·참조 어셈블리를 사용해 변경을 포함한 전체 `Assembly-CSharp`와 `Assembly-CSharp-Editor`를 별도 Temp 출력으로 컴파일했다. 두 어셈블리 모두 종료 코드 0, 최종 경고·오류 0이다. Unity의 에셋 임포트 완료를 의미하지는 않는다.

`verify_scene.py`는 로컬 fileID, 외부 GUID, 원본 Camera/태그/플레이어 참조, 프리팹 추가 자식, 모든 Cinemachine 컴포넌트 및 입력 UUID, 축 제한, 리센터링, 장애물 마스크를 검사한다. 최초 검사에서 제거된 카메라를 가리키던 follow 오버라이드를 발견해 수정한 후 다시 통과했다. 원래 씬의 카메라 연결 외 오브젝트·지형 오버라이드 보존도 확인한다. 상세 결과는 `static-checks.json`에 있다. 이 검사는 Unity 역직렬화를 대신하지 않는다.

| 요청한 플레이 항목 | 결과 |
| --- | --- |
| 제자리 마우스 상하·좌우 회전 | 설정 및 코드 검사 완료. 실제 Play 미검증. |
| 90° 회전 후 W 이동 | 실제 카메라 기준 이동 연결 검사 완료. 실제 Play 미검증. |
| 걷기·점프 추적 / 캐릭터 회전 시 시점 유지 | Damping, World Space, 리센터링 해제 검사 완료. 실제 Play 미검증. |
| 벽·절벽에서 당김 및 자연스러운 복귀 | Deoccluder·레이어 및 기존 지형 Collider 생성 경로 검사 완료. 실제 Play 미검증. |
| 능력 조준·상호작용 | 이 씬은 원래 `puzzleLabLocomotion=true`, `world=null`인 탐색 모드로 능력·상호작용 실행 경로가 비활성화되어 있다. 카메라 교체 범위에서 게임플레이를 새로 활성화하지 않았다. 실제 능력 검증 대상 없음. |
| Esc·포커스 상실·리스폰 | 기존 커서 정책과 새 입력/리스폰 연결 검사 완료. 실제 Play 미검증. |

Unity가 실행되는 환경에서 저장된 씬을 열고 `Tools > EVERFLOW > V2 Exploration > Run Cinemachine Play Checks`로 실제 Input System 마우스 이벤트, 이동·점프, World Space 유지, 임시 벽 충돌·복귀, 일시정지·복귀, 리스폰을 검사할 수 있다. 테스트 지형과 마우스는 Play 전용이며 저장하지 않는다. 이 Play 검사는 이번 작업에서는 실행되지 않았다. `Install Cinemachine Camera` 메뉴는 같은 구성을 다시 적용하고 씬 저장·재열기·참조 검사를 수행한다.

## 변경 파일

- `Assets/Scenes/Everflow_Map_v2_test.unity`
- `Assets/EverflowMap/Runtime/EFMapPlayer.cs`, `EFMapExploration.cs`
- `Assets/EverflowMap/Runtime/EFMapCinemachineRig.cs`와 meta
- `Assets/EverflowMap/V2Camera.inputactions`, `V2CameraLook.asset`와 meta
- `Assets/EverflowMap/Editor/EFMapCinemachineSetup.cs`, `EFMapCinemachineChecks.cs`와 meta
- `Assets/EverflowMap/Editor/EFMapExplorationChecks.cs`
- `docs/cinemachine/README.md`, `verify_scene.py`, `static-checks.json`

구성은 설치된 패키지 소스와 [Unity Cinemachine 3.1 Deoccluder 문서](https://docs.unity.cn/Packages/com.unity.cinemachine%403.1/manual/CinemachineDeoccluder.html)를 확인해 작성했다.
