# EVERFLOW V2 씬과 스크립트 안내

작성: 2026-11-05. 이번 결과물의 실제 구성·조정 방법이다. 씬/기획 비교, 퍼즐별 상태와 후속 순서는 [다음 작업 문서](EVERFLOW_V2_Next_Steps.md)를 참조한다. 기준 기획서는 [v0.2](EVERFLOW_redesign_v0.2.md), 실험실 세부 조작은 [PuzzleLab 가이드](EVERFLOW_V02_PuzzleLab_Guide.md)다.

작업 전 프로젝트·Assets/docs 하위 및 확인한 상위 경로에서 AGENTS.md를 찾지 못했다. `지시사항.txt`와 기존 개발/검증 지침을 읽었고, 과거 타이틀·버전 변경 요청은 이번 작업 범위로 재실행하지 않았다. [작업 전 Git 상태](v2-exploration/git-status-before.txt)의 플레이어/기존 맵 수정, 실험 씬 삭제, 미추적 팀원 자산·실험실 자료를 보존했다.

## 1. 어떤 씬을 열면 되는가

| 경로 | 용도와 중요한 구성 |
| --- | --- |
| `Assets/Scenes/Everflow_Map_v2_test.unity` | 이번 주 대상. 팀원 맵에서 이동·충돌·카메라를 확인. 타이틀 실행 불필요 |
| `Assets/Scenes/Everflow_Map.unity` | 기존 6단계 맵/능력/진행/연출의 참고와 회귀 확인. 기존 사용자 배치 보존 |
| `Assets/Scenes/Everflow_V02_PuzzleLab.unity` | v0.2 규칙 T01~T04 및 지하 회로를 도형으로 검증. 새 맵과 다른 씬 |
| `Assets/Scenes/Everflow_V02_PuzzleLab_T01_Check.unity` | A 두 해법만 집중 확인하는 단독 실험실 |

새 맵 Hierarchy의 루트는 다음과 같다. FBX·조명은 원래 배치를 유지했다.

| 오브젝트 | 역할 |
| --- | --- |
| `EF_Map_v2` | `Assets/EF_Handoff_v2/EF_Map_v2.fbx` 모델 인스턴스. 지형·건물·계단·물·퍼즐 예정 모델 포함. 원본 Transform/활성 오버라이드 보존 |
| `Directional Light` | 기존 조명. 새 조명 추가하지 않음 |
| `Main Camera` | 기존 Camera + URP 추가 데이터 + AudioListener + EFMapCamera. `player` 참조를 새 플레이어로 연결 |
| `EFMapPlayer` | 기존 캡슐 프리팹의 인스턴스 하나. 월드/퍼즐 진행에 묶이지 않는 이동 분기 사용 |
| `SpawnPoint_V2_FirstPuzzle_Approach_TEMP` | 임시 시작점. 중심 좌표 (-46,1.25,-9), Y 회전 180°. 이름의 TEMP는 첫 퍼즐 위치가 팀 확정 전임을 의미 |
| `V2 Exploration` | EFMapExploration. 시작 배치·지형 충돌 준비·R/낙사 복귀·Esc/커서 소유 |

새 맵에 EventSystem·PlayerInput·추가 Camera·추가 AudioListener·EFMapWorld·PuzzleLab은 만들지 않았다. UI를 생성하지 않으므로 EventSystem이 필요 없다. 씬에 별도로 플레이어를 드래그해 추가하면 중복된다.

기존 맵의 `EF_World` 아래에는 플레이어 프리팹 인스턴스, P1~P6의 spawn/arrival/장치/복원 참조가 있고, World는 시작 시 P1 spawn으로 플레이어를 Teleport한다. 프리팹만 복사하고 World를 비워 두면 기본 이동 분기에서 멈춘다. World를 빈 상태로 붙이면 `Awake`의 wheel, `Start`의 stages/endingCamera 등 필수 참조 때문에 독립 실행 구성이 되지 않는다.

전체 실험실의 `PuzzleLab`은 areas 5개·orbs 3개·player를 소유한다. 구역 오브젝트 이름은 `T01 - A / SAME MOUND`, `T02 - B / ROOT TERRACES`, `T03 - C / WIND AND WIDE LEAF`, `T04 - G / WATERWHEEL GATE`, `D1-D5 + W / INTEGRATED CAVERN`이다. 각 구역의 장치·안전 spawn·목표·리셋 범위는 LabArea가 연결한다. Presentation 안내판은 실험실 전용이며 새 맵에 복사하지 않았다.

## 2. Unity에서 바로 실행하기

1. Unity **6000.3.21f1**로 프로젝트를 열고 컴파일이 끝날 때까지 기다린다. URP 17.3.0, Input System 1.20.0, 입력 Both 설정은 유지했다.
2. 현재 미저장 작업을 저장한 뒤 Project에서 `Assets/Scenes/Everflow_Map_v2_test.unity`를 더블클릭한다. 다른 씬은 Additive로 함께 열지 않는다.
3. Play를 누르고 Game 뷰에 포커스를 둔다. **설정/생성 메뉴를 선행 실행할 필요는 없다.** 저장된 플레이어가 있고, 시작 시 선택 지형 MeshCollider를 자동으로 만든다.
4. Esc로 커서를 풀고 다시 Esc로 탐색을 재개한다. 앱 포커스를 잃으면 입력을 멈추므로 돌아온 뒤 Game 뷰를 클릭하고 Esc로 재개한다.

| 새 맵 입력 | 실제 동작 |
| --- | --- |
| WASD | 카메라 수평 방향 기준 이동 |
| Space | 접지 상태에서 일반 점프 |
| 마우스 이동 | 우클릭 없이 3인칭 자유 회전 |
| C | 플레이어 뒤로 시점 정렬 |
| R | 시작점으로 수동 리스폰. 새 맵에서는 초기화할 퍼즐 상태가 없음 |
| Esc | 플레이어·카메라 입력 정지/재개와 커서 해제/잠금. 별도 메뉴 패널은 없음 |

탐색 씬에는 능력 대상/슬롯/HUD가 없다. 1/2/3은 재사용 플레이어의 내부 선택값만 바꿀 수 있고, 기존 Aim과 E 탑승 분기는 비활성이다. Q/X/F1~F5/Backspace 같은 실험실 전용 조작이 이 씬에서도 된다고 가정하지 않는다. R은 실험실에서는 구역 초기화, 기존 맵에서는 Retry로 의미가 다르다.

## 3. 플레이어·입력·카메라 연결

`Assets/EverflowMap/Prefabs/EFMapPlayer.prefab`은 루트 하나의 기본 캡슐이다. 하위 캐릭터 리그·Animator·PlayerInput이 없다. MeshFilter, MeshRenderer, CharacterController, EFMapPlayer가 붙어 있다. 태그는 Untagged, 레이어는 **Ignore Raycast(2)**다. 플레이어가 카메라/조준 광선에 걸리지 않게 하지만 물리 충돌은 켜져 있다.

| 구성 | 실제 설정과 흐름 |
| --- | --- |
| 입력 | `Assets/EverflowGraybox/Runtime/EFInput.cs`가 Keyboard.current로 WASD/Space/R 등을 읽음. Both 설정에서도 ENABLE_INPUT_SYSTEM 분기 우선. Input Actions나 PlayerInput 설정 불필요 |
| 시점 추가 입력 | `EFMapInput.cs`가 Mouse.current.delta와 C/Esc를 읽음. 기존 graybox 입력과 연결 |
| 이동 | `EFMapPlayer.Update → Walk → CharacterController.Move`. Rigidbody/AddForce 방식 아님. 카메라 PlanarForward/Right와 입력을 합쳐 속도 계산 |
| 접지·점프 | CharacterController.isGrounded와 충돌 플래그 사용. 별도의 groundMask 필드나 GroundCheck 자식 없음. 일반 점프 높이 1.35, 중력 24, 이동 속도 6 |
| 지면 추종 | OnControllerColliderHit에서 위쪽 면의 supportTransform을 기억. `puzzleLabLocomotion=true`이면 일반 Collider의 이동도 추종. 기존 false 모드는 EFMapNode 지면 중심 |
| 카메라 | `EFMapCamera.Update`에서 yaw/pitch 갱신 → 플레이어가 해당 각도로 이동 → 카메라 LateUpdate에서 추적. Cinemachine 패키지/가상 카메라 사용 안 함 |
| 가림 | 플레이어 앵커에서 SphereCast. 벽에 닿으면 거리를 즉시 줄이고 다시 멀어질 때 보간. 플레이어 하위·트리거·Ignore Raycast 제외 |
| 시작 배치 | 새 플레이어를 코드로 Instantiate하지 않음. 저장된 인스턴스를 Exploration.Start가 SpawnPoint 위치/방향으로 Teleport |

새 씬의 프리팹 오버라이드는 `puzzleLabLocomotion=true`, view=`Main Camera/Camera`, follow=`Main Camera/EFMapCamera`, 위치/방향이다. world는 비어 있고 원본 프리팹 및 공통 스크립트는 바꾸지 않았다. 카메라 EFMapCamera.player도 같은 인스턴스를 가리킨다. 필요 없는 null Collision Ignore Roots 항목은 빈 배열로 정리했다.

### 시작점과 리스폰

기획서 S→A 도입과 FBX의 `S_OverlookPaving`, `W_A_Pond`, `PH_A_Tut_*` 이름을 근거로 A 연못 앞 전망대를 골랐다. 기존 맵의 (27.45,1.18,30.86)을 가져오지 않았다. 새 시작점은 (-46,1.25,-9)에서 -Z 방향으로 A를 바라본다. G/W가 실제 한 화면에 보이는지는 미검증이다.

SpawnPoint 위치는 **플레이어 캡슐 중심**이다. CharacterController 높이 2/center 0이므로 발은 SpawnPoint보다 1m 낮다. FBX 계산상 바닥 y=.12, 발 y=.25로 0.13m 여유가 있고, 중심/반경 .4m의 전후좌우 5점 바닥 검사를 통과했다. 실제 캡슐 겹침과 importer 변환은 Unity 검증 대상이다.

`EFMapExploration.fallHeight=-45` 아래로 내려가거나 R을 누르면 SpawnPoint 회전을 먼저 적용한 뒤 `EFMapPlayer.Teleport`한다. Teleport는 탑승·이전 지면·속도·관성을 지우고 CharacterController를 잠시 껐다 켜며 카메라 `Snap(true)`로 추적 속도와 뒤쪽 방향을 복구한다. 기존 World.Respawn은 Stage와 nodes에 의존하므로 새 맵에 빈 World를 만들어 재사용하지 않았다. 안전한 Teleport만 재사용한 최소 전용 복귀다.

지하 바닥 -12와 W 부근 -18, 외곽 지형 최저 -36.60을 보존하려고 기준을 -45로 정했다. 지형 위의 깊은 홈은 자동 낙사 조건에 들지 않을 수 있으므로 R로 복귀한다. 구역별 체크포인트·퍼즐 상태 리셋은 아직 없다.

## 4. 충돌과 이동 설정 조절

FBX importer의 `addColliders: 0`과 원래 새 씬의 Collider 부재를 확인했다. 모델 전체를 덮는 BoxCollider와 Rigidbody를 추가하지 않았다. `V2 Exploration/EFMapExploration.solidMeshNames`의 **정확한 메시 이름 286개**에 각 MeshFilter.sharedMesh를 쓰는 비볼록 정적 MeshCollider를 시작 시 생성한다. 이미 켜진 비트리거 Collider가 있으면 재사용하므로 중복 생성하지 않는다. 빠진 이름·동명이인 메시·Rigidbody 하위 지형은 명시적 오류로 드러낸다.

선택 범위는 지형, 바닥·벽·천장·난간·기둥, 타워/D1/D3 계단, TEMP 다리, 주요 바위·돌기둥, 현재 위치의 잎/이동석/문 같은 주요 장애물이다. `W_*` 물·거품, `VOL_*` 바람/흐름 시각 메시, `FX_*`, 바다, 작은 잎·나무·횃불·매달린 뿌리, 작게 축소된 성장 프리뷰는 제외했다. 물을 얼린 고체 바닥으로 만들지 않았다. PH 예정 장치는 **정적 모델**로만 충돌하며 움직이지 않는다.

런타임 충돌체는 Play 종료 시 사라진다. Edit 모드에서도 충돌을 보고 싶다면 다음을 사용한다.

- `Tools > EVERFLOW > V2 Exploration > Bake Terrain Colliders In Open Scene`: 같은 목록을 현재 씬의 오버라이드로 생성한다. Undo 가능하며 자동 저장하지 않는다. Ctrl+S로 저장하면 다음 Play는 기존 Collider를 재사용한다. 원본 FBX importer는 바꾸지 않는다.
- `Tools > EVERFLOW > V2 Exploration > Validate Open Scene`: 단일 플레이어/활성 카메라/Listener, Missing Script, World 부재, 연결, 레이어, 시작 바닥/캡슐 겹침을 검사한다. Play 중 실행하거나 먼저 Bake한 상태에서 실행한다. 검사가 Collider를 추가하지 않는다.
- 모델 자식을 선택해 Inspector의 Collider와 Scene 뷰 Gizmos를 본다. Edit에서 Bake 전에는 Collider가 없는 것이 현재 설계다. `Window > Analysis > Physics Debugger`로 실제 충돌 형상을 확인할 수 있다.

모델 이름을 바꾸거나 FBX를 교체하면 solidMeshNames를 재검토한다. 모든 새 장식까지 자동으로 충돌체를 붙이지 않는다. 레이어는 Default(0) 지형과 Ignore Raycast(2) 플레이어 사이 물리 충돌이 켜져 있고, 카메라 mask `4294967291`은 Default를 포함한다. 지형을 Ignore Raycast로 옮기면 카메라 충돌에서 빠지고 이 구성 검사가 오류를 낸다.

| 조정 위치 | 실제 필드와 영향 |
| --- | --- |
| SpawnPoint Transform | Position / Rotation Y. 시작 및 R/낙사 복귀 위치/방향. 스케일은 1 유지. Play 중 변경은 종료하면 사라짐 |
| EFMapPlayer 인스턴스 | moveSpeed=6, gravity=24, jumpHeight=1.35, turnSharpness=14. 이번에 이동 수치 변경 없음 |
| CharacterController | height=2, radius=.4, slopeLimit=45, stepOffset=.55, skinWidth=.05, center=0. 프리팹 Apply 대신 인스턴스 오버라이드로 조정 |
| Main Camera / Camera | 기존 FOV=60, near=.1, far=500 유지 |
| Main Camera / EFMapCamera | distance=7, lookHeight=.7, shoulderOffset=.6, sensitivity=.14, verticalSensitivity=.85, minPitch=-35, maxPitch=70, followSmoothTime=.1, verticalSmoothTime=.14, collisionRadius=.25, collisionPadding=.12 |
| V2 Exploration | player / spawnPoint / mapRoot 참조, solidMeshNames, fallHeight. 참조를 지우면 명확한 초기화 오류가 발생 |

모델 분석상 타워 계단 단차 .25m, D1 .20m, D3 부두 계단 최대 약 .217m로 stepOffset .55m보다 작다. 이것은 기하학적 비교이며 실제 계단 통과를 보장하지 않는다. 일반 점프의 이상적인 같은 높이 수평 이동 거리는 약 4m(6m/s, 중력 24, 높이 1.35)이며 실제 유효 착지 여유는 더 작다. 실제 거리를 확인하지 않고 점프 수치를 늘리거나 모든 단차를 연결하지 않는다. 미연결 의심 위치는 [다음 작업 4절](EVERFLOW_V2_Next_Steps.md#4-우선-해결할-공간통합-문제)을 따른다.

## 5. 핵심 파일 연결 안내

경로는 프로젝트 루트 기준이다. 아래 Runtime 폴더 표는 파일마다 실제 경로를 적었으며, 긴 코드를 복사하는 대신 연결과 고장 증상을 정리했다.

| 파일 | 역할·씬 연결·의존성과 주의점 |
| --- | --- |
| `Assets/EverflowMap/Prefabs/EFMapPlayer.prefab` | 기존/새 맵에서 재사용하는 캡슐. CharacterController와 EFMapPlayer 포함. view/follow/world는 씬 참조이므로 원본 prefab에서는 비어 있음. 새 맵 설정을 Apply하면 다른 씬에 영향 가능 |
| `Assets/EverflowMap/Runtime/EFMapPlayer.cs` | 실제 이동·점프·타기·조준·Teleport. 새 씬은 standalone 분기만 사용. 기존 씬은 world.GameInputActive, stages, EFMapNode 등을 사용. 공통 수정 시 기존 맵/실험실도 회귀 필요. view/follow 둘 다 없으면 이동 방향 계산 불가 |
| `Assets/EverflowGraybox/Runtime/EFInput.cs` | 공용 이동·일반 키 입력. 새 키를 임의로 KeyDown에 전달해도 switch에 없는 키는 신 Input System에서 읽히지 않음. R은 이미 지원 |
| `Assets/EverflowMap/Runtime/EFMapInput.cs` | 마우스 delta, C/Esc와 기존 EFInput 연결. Input Actions 에셋을 참조하지 않음. 감도는 EFMapCamera에서 조정 |
| `Assets/EverflowMap/Runtime/EFMapCamera.cs` | 추적/회전/가림/리스폰 Snap. player를 잘못 연결하면 다른 대상을 보거나 카메라가 멈춤. collisionIgnoreRoots에 맵 루트를 넣으면 벽 충돌 전체가 무시됨 |
| `Assets/EverflowMap/Runtime/EFMapExploration.cs` **신규** | 새 씬 V2 Exploration에만 연결. 정확한 메시 목록의 충돌 준비, 시작/복귀/커서 관리. DefaultExecutionOrder -250. mapRoot 변경 시 목록·지형·fallHeight 재검토 |
| `Assets/EverflowMap/Editor/EFMapExplorationTools.cs` **신규** | 새 씬 전용 Bake/Validate 메뉴. 다른 씬 실행을 거부하며 원본 prefab/importer를 건드리지 않음. Bake 후 저장은 사용자가 수행 |
| `Assets/EverflowMap/Editor/EFMapExplorationChecks.cs` **신규** | 현재 저장된 v2 씬의 Play 자동 검사 메뉴/프로브. 테스트 입력을 주입하고 결과를 docs/v2-exploration/play-smoke-checks.txt에 기록. 이번 세션에서는 미실행 |
| `Assets/EverflowMap/Runtime/EFMapWorld.cs` | 기존 EF_World 진행·입력/커서·P1 시작·체크포인트·Retry·낙사·오프닝/엔딩. stages/debris/gate4/sideWater/wheel/finalFlow/cameras 필요. 새 맵에 연결하지 않음 |
| `Assets/EverflowMap/Runtime/EFMapNode.cs` | 기존 꽃·덩굴·물·다리·문 등. world/StageIndex/Ability.Pulse에 묶임. actions/support/supply/motion/ridePath 같은 참조 필요. v0.2 LabTarget의 대체품으로 단순 복사 금지 |
| `Assets/EverflowMap/Runtime/EFMapCheckpoint.cs` | 기존 맵 트리거. world, stage, destination, requirements가 필요. 새 맵에 붙이면 빈 World 접근 위험 |
| `Assets/EverflowMap/Runtime/EFMapAnimation.cs` | 기존 성장/복원 움직임 후보. Node.actions와 World restoration에서 호출되는 대상별 연출. 새 Lab 상태 변화와 연결하는 어댑터는 아직 없음 |
| `Assets/EF_Handoff_v2/EF_Map_v2.fbx` 및 `.meta` | 팀원 맵 원본. 이번에 수정하지 않음. source/previews/Textures라는 폴더 메타만 있는 부분을 완성된 제작 문서나 이미지 증거로 간주하지 않음 |

### 이후 퍼즐 통합에 필요한 파일

이 표의 스크립트는 모두 현재 실험실에서 분석했다. 새 맵에 연결하지 않았다. 전체 실험실에 LabTarget 20/ LabMover 5/ LabGate 2/ LabCanal 1이 실제 직렬화되어 있으며, 단순히 파일 존재만으로 구현을 판단하지 않았다.

| 실제 파일 | 연결·설정·이식 시 영향 / 끊겼을 때 증상 |
| --- | --- |
| `Assets/EverflowPuzzleLab/Runtime/LabState.cs` | Unity 밖에서도 검사 가능한 슬롯·성장/고정/방향·걸쇠·공급/3초 상태. MonoBehaviour가 아니라 각 컴포넌트 내부 상태/공용 규칙. 변경하면 모든 실험실 퍼즐 의미가 바뀜 |
| `Assets/EverflowPuzzleLab/Runtime/PuzzleLab.cs` | 실험실 루트. player, areas, slots[3], orbs[3], castRange=14. 클릭/Q/X/방향/초기화/메뉴 관리. areas나 player 누락 시 시작/갱신 오류. Exploration과 시작/커서 관리 중복 방지 필요 |
| `Assets/EverflowPuzzleLab/Runtime/LabTarget.cs` | 반응 대상. area/owner, acceptsBloom/Freeze/Flow, water, growth와 small/grown 크기·로컬 위치, grownObjects, iceSupport, directions/arrows, indicators. 물 표적과 구조물 표적 분리. iceSupport 누락은 얼려도 발판이 안 생기는 원인 |
| `Assets/EverflowPuzzleLab/Runtime/LabDirection.cs` | 방향 화살표에 target/index 연결. 틀리면 엉뚱한 흐름 방향 적용/선택 실패 |
| `Assets/EverflowPuzzleLab/Runtime/LabMover.cs` | A/B/C/지하 잎·가변 수로. target, flow, motion, rotor, home/destinations, speed, oscillate, smallReach/smallSpeed, powered. home/목적지는 월드 좌표. 참조나 배열 수가 틀리면 정지·다른 구역으로 이동·리셋 순간이동 |
| `Assets/EverflowPuzzleLab/Runtime/LabArea.cs` | 구역의 lab/spawn/goal/secondaryGoal, goalSize, bounds, recoveryHeight, goalGate/canal, sign. 접지 목표 성공과 국소 초기화. bounds/복귀 높이는 월드 기준. sign은 현재 평가에서 필요할 때 갱신하므로 이식 시 안내판을 뺄 경우 관련 검사 조건도 재검토 |
| `Assets/EverflowPuzzleLab/Runtime/LabGate.cs` | G/D4 문. root/flow/wheelDirection, door/wheel/latch, closed, lift/speed, supplied. closed는 월드 위치. 문이 완전히 열리면 걸쇠 유지. 콜라이더를 다른 고정 오브젝트에 두면 문은 올라가도 통과 불가 |
| `Assets/EverflowPuzzleLab/Runtime/LabGateFlow.cs` | G 분기의 wheelWater/spillWater 표현을 flow.Direction으로 선택. 배열 참조 누락은 표시 갱신 오류 |
| `Assets/EverflowPuzzleLab/Runtime/LabRootLift.cs` | 지하 우회 뿌리의 closed/opened 문 위치를 root.Grown으로 선택. 두 위치는 월드 좌표. 뿌리 아래로 단순 재부모하면 복귀 위치가 틀어질 수 있음 |
| `Assets/EverflowPuzzleLab/Runtime/LabCanal.cs` | 지하 Hydraulic circuit. aqueduct/dockLeaf, bypassRoot, upperIce/mainIce/branch/gate, wheel, vegetation, 수로별 물 배열, connectionPoint/tolerance=.5, successSeconds=3. 한 참조라도 다른 구역 장치를 가리키면 공급·최종 복원이 잘못됨 |
| `Assets/EverflowPuzzleLab/Runtime/LabStreamMarker.cs` | marker를 localStart/localEnd 사이로 반복 이동하는 물 표현. 공급 논리 자체가 아님. 물 표시 루트 활성과 함께 연결해야 마른 수로가 흐르는 것처럼 보이지 않음 |
| `Assets/EverflowPuzzleLab/Runtime/LabInfoBoard.cs` | 기존 LabArea/LabCanal 상태를 대표 TextMesh 판면에 표시. 새 맵 필수 퍼즐 로직이 아님 |
| `Assets/EverflowPuzzleLab/Runtime/LabShortLabel.cs`, `LabSignFont.cs` | 실험실 짧은 라벨 시점/거리 처리와 OS 한글 폰트 연결. 다른 PC 폰트 가용성·가독성 미검증. 새 맵 탐색에 복사하지 않음 |
| `Assets/EverflowPuzzleLab/Editor/PuzzleLabBuilder.cs` | 실험실 생성·기본 치수 출처. 메뉴 재생성으로 현재 씬을 덮어쓰지 않기 |
| `Assets/EverflowPuzzleLab/Editor/PuzzleLabValidation.cs`, `PuzzleLabChecks.cs` | 참조 검사 및 기존 T01/전체 Play 검사. 상태 API와 실제 입력/물리 검사를 구분해서 읽기 |

기믹 묶음은 프리팹 후보지만 현재 실험실은 씬 오브젝트들이다. 프리팹화만 하면 되는 상태라고 보지 않는다. Target↔Area↔PuzzleLab 순환 참조, 외부 FLOW 대상, 문과 물 표시, 세계 좌표 레일/Bounds를 분리해야 한다. 슬롯/입력/커서/전역 결과 관리자는 씬에서 하나만 두고, 각 묶음은 새 씬 관리자를 주입해야 한다. 전체 지하 회로는 하나의 reset 단위로 유지한다.

## 6. 검사 도구와 이번 검증 결과

### 이번에 실제 수행한 검사

| 분류 | 결과·근거 | 정확한 한계 |
| --- | --- | --- |
| 새 맵 Play | **미실행** | 현재 Windows 토큰 home\CodexSandboxOffline과 사용자 환경 vzd12 차이. CLI status는 연결 인스턴스 0 및 탐지 불완전 경고. 기존 라이선스 IPC 실패 기록도 있음. 라이선스 만료로 판정하지 않음 |
| C# 오프라인 컴파일 | Runtime·Editor·UNITY_EDITOR 없는 플레이어 조건 **통과** | 동일 설치 버전 Roslyn과 Library/Bee 캐시 참조. Unity import/씬 실행/실제 빌드는 아님 |
| 새 씬 직렬화 | 내부 참조, 플레이어 prefab 인스턴스 오버라이드, 카메라 양방향 연결, 외부 스크립트 GUID 검사 **통과** | FBX·prefab 내부 fileID를 실제 importer로 해석한 검사는 미실행. Missing Script/NullReference 런타임 0이라고 단정하지 않음 |
| 기하학 | FBX 단위/축/회전·피벗을 읽어 시작 바닥 5점, 최저 지형, 대표 계단 단차 검사 **통과** | Unity Physics/CharacterController가 통과한 증거 아님 |
| 실험실 상태 | 실제 LabState 소스의 관리 코드 검사 **38개, 실패 0** | 클릭/레이캐스트/탑승/물리 Play 검사가 아님 |
| 보존 | 기존 파일 1,802개 중 새 맵 씬만 변경, 1,801개 동일. 원래 FBX 인스턴스 직렬화와 조명, Camera 본체 설정 보존 | 작업 전부터 수정/미추적/삭제된 파일은 그대로 유지. 기존 씬 Play 회귀는 미실행 |

증거는 [static-results.json](v2-exploration/static-results.json), [preservation.json](v2-exploration/preservation.json), [Runtime 컴파일](v2-exploration/Assembly-CSharp-compile.txt), [Editor 컴파일](v2-exploration/Assembly-CSharp-Editor-compile.txt), [플레이어 조건 컴파일](v2-exploration/Assembly-CSharp-player-compile.txt), [상태 검사](v2-exploration/managed-state-checks.txt), [실행 환경](v2-exploration/unity-execution-status.json)에 있다. 과거 실험실 Play 45개 기록의 시점/한계는 다음 작업 문서 3절에 설명했다.

### Unity에서 이어서 실행할 검사

새 씬을 하나만 열고 저장한 뒤 `Tools > EVERFLOW > V2 Exploration > Run Open Scene Play Smoke Checks`를 실행한다. Play 진입 후 테스트 입력으로 시작·접지·보행·점프·카메라 회전·R·낙사·복귀 후 이동·대표 바닥/벽 광선·Esc를 검사하고 종료한다. 실행 시각과 결과는 `docs/v2-exploration/play-smoke-checks.txt`에 새로 기록된다. 90초 제한/중단은 INCOMPLETE로 기록한다. **이번 작업에서 이 결과 파일을 통과 내용으로 미리 만들지 않았다.**

자동 검사의 바닥/벽 광선은 실제 전 구간 보행을 대체하지 않는다. 사용자가 계단·경사·다리·주요 벽을 직접 걸어보고, 리스폰 후 조작·카메라·콘솔을 확인해야 한다. 기존 맵도 별도로 직접 실행해 오프닝 이후 이동/점프/능력/복귀를 확인한다. 기존 `Run Physical and State Tests`는 현재 타이틀 라우트와 맞지 않으면 스스로 중단하는 가드가 있으므로 그 중단을 새로운 회귀로 오인하지 않는다.

터미널 재검사는 프로젝트 루트에서 `python tools/verify_v2_exploration.py`다. Unity를 시작하지 않는다. 기존 .rsp 캐시·동일 Unity 설치·.NET SDK가 필요하다. 결과를 docs/v2-exploration에, 중간 DLL/.rsp를 Temp/V2ExplorationCompile에 쓴다. Bake를 저장하거나 이후 맵을 편집하면 작업 시작 해시와 달라져 보존 검사가 중단될 수 있다. 이 경우 옛 기준을 현재 작업의 증거로 덮어쓰지 말고 별도 세션 기준을 만든다.

## 7. 변경 내역·복구·문제 확인 순서

수정한 기존 에셋은 `Assets/Scenes/Everflow_Map_v2_test.unity` 하나다. 플레이어·호스트·SpawnPoint와 참조를 추가하고 카메라 시작 위치를 조정했다. 기존의 플레이어 부재, camera.player 누락, 충돌 부재, 독립 시작/복귀 관리자 부재를 이번 탐색 구성에서 해결했다. 실제 런타임 발견 오류를 고쳤다고 주장하지 않는다.

새 파일은 Runtime 1개(`EFMapExploration.cs`), Editor 2개(`EFMapExplorationTools.cs`, `EFMapExplorationChecks.cs`)와 각 meta, 문서 2개, 분석/설정/검사 Python 3개(`inspect_v2_fbx.py`, `setup_v2_exploration.py`, `verify_v2_exploration.py`)다. 분석 스크립트는 Assets 밖에 있으며 게임 빌드에 포함되지 않는다. 증거·작업 전 상태는 `docs/v2-exploration/`에 보관했다.

새 씬 작업 전 사본은 `Backups/V2-Exploration-20261105/Everflow_Map_v2_test.unity`다. `tools/setup_v2_exploration.py`는 이번 1회 파일 연결용이며 호스트가 있으면 재적용하지 않는다. Unity에서 조정한 씬을 다시 생성하는 도구로 사용하지 않는다. 기존 플레이어/카메라/World/PuzzleLab 소스·프리팹·기존 씬·모델·빌드 씬 목록·버전·패키지는 수정하지 않았다.

| 증상 | 확인 순서 |
| --- | --- |
| 플레이어가 없거나 둘임 | 정확한 새 씬인지 → EFMapPlayer 인스턴스 수 → 여러 씬 동시 실행 여부 → Console 초기화 오류 |
| 움직이지 않음 | Game 포커스 → Esc 재개 → EFMapPlayer/EFMapCamera enabled → puzzleLabLocomotion=true/world=null → view/follow 연결 |
| 바닥을 통과함 | 초기 Console의 FBX 이름 오류 → solidMeshNames 포함 여부 → Play Collider.sharedMesh/Enabled/Is Trigger → Default/플레이어 레이어 충돌 → 모델 단위/축 변환 |
| 벽을 통과함 | 해당 벽의 정확한 이름/Collider → 얇은 면 방향과 메시 구멍 → 장식으로 제외된 항목인지. FBX 전체 BoxCollider로 덮지 않기 |
| 계단·다리에서 막힘 | 실제 충돌 형상·모델 스케일·도달 높이 → 끝부분 바닥 연결 → 캡슐 radius/stepOffset/slopeLimit 순으로 확인 |
| 카메라가 멈춤/벽 관통 | player 상호 참조 → Esc 정지 → collisionMask Default 포함 → mapRoot가 collisionIgnoreRoots에 들어갔는지 → terrain collider |
| R 뒤 시점/이동 이상 | spawnPoint 위치/회전 → Teleport와 Snap 참조 → 이전 입력/진행 관리자가 추가됐는지 |
| 다른 씬으로 옮김 | 플레이어 하나·Camera/Listener 하나·양방향 참조·독립 이동 모드·그 씬의 SpawnPoint/맵 루트/충돌 목록/낙사 높이까지 함께 연결. 원본 World/PuzzleLab을 빈 채 추가하지 않기 |

현재 남은 문제는 Play/물리·사람 조작성 미검증, 임시 시작 위치, 닫힌 G/D4 문과 바닥 공백 등 원래 맵 연결의 미확정 상태, 실제 퍼즐 미통합이다. 이들을 탐색 코드 컴파일 오류와 구분하며, 다음 순서는 [후속 작업](EVERFLOW_V2_Next_Steps.md#5-권장-개발-순서와-완료-기준)에 정리했다.
