# EVERFLOW 자유 시점 카메라 적용 결과

`Assets/Scenes/Everflow_Map.unity`와 `Assets/EverflowMap/Prefabs/EFMapPlayer.prefab`에 실제 저장했다. 별도 설정 메뉴를 실행할 필요 없이 타이틀 또는 Map 씬에서 Play하면 된다. 기존 3초 물레방아 오프닝 후 플레이어 뒤의 자유 시점으로 전환된다.

## 조사한 기존 구조

- Unity 6000.3.21f1, Input System 1.20.0, URP 17.3.0. Assets 및 Packages의 Cinemachine 검색 결과 사용/설치 항목 없음. 패키지 추가나 버전 변경 없음.
- 기존 `EFMapCamera`는 플레이어를 바로 따라가며 우클릭 중에만 포인터 좌표 차이로 회전했다. 오프닝/엔딩 카메라는 `EFMapWorld`가 전환했다.
- 기존 `EFMapPlayer`는 CharacterController를 사용하고, 카메라 수평 방향의 WASD 이동이 구현되어 있었다. 이동 방향으로 캐릭터를 회전시키는 기능은 없었다.
- 기존 능력은 마우스 포인터 위치의 화면 Raycast, 1/2/3 선택, 좌클릭, 플레이어 기준 10m 사거리와 Linecast 가림 검사였다. 신규 자유 시점에서는 포인터를 잠그므로 **화면 중앙 조준점 Raycast**로 변경했다. 능력 선택/사용 입력·거리·진행 조건은 유지했다.
- C/Esc는 기존 Map 조작에 배정되어 있지 않았다. 원본 Graybox/MVP 입력 클래스는 수정하지 않고 Map 전용 입력 확장을 추가했다.
- 조사 당시 Unity Editor는 실행 중이지 않았고 Hub만 있었다. 사용자의 기존 로고·맵·모델·다른 씬 변경사항을 되돌리지 않았다. 프로젝트와 확인한 상위 경로에 AGENTS.md 없음.

## 조작법

| 입력 | 동작 |
|---|---|
| 마우스 이동 | 우클릭 없이 좌우/상하 자유 시점 |
| WASD | 현재 카메라의 수평 방향 기준 이동 |
| C | 현재 캐릭터 등 뒤로 시점 정렬, 기본 아래보기 각도 복원 |
| Esc | 일시정지/복귀. 메뉴에서는 커서 표시·잠금 해제 |
| 1/2/3, 좌클릭 | BLOOM/FREEZE/PULSE 선택 후 중앙 조준점 대상에 사용 |
| Space, E, R | 기존 점프/덩굴 이탈, 탑승, 현재 퍼즐 재시도 유지 |

엔딩에서는 기존 Restart world/Title 외에 **Continue exploring** 버튼으로 플레이 카메라와 조작에 복귀할 수 있다. 복귀 후 엔딩 카메라가 반복해서 다시 켜지지 않는다.

## 카메라와 이동 동작

`Main Camera`의 `EFMapCamera`가 플레이 시점의 유일한 제어 주체다. 마우스 각도는 Update에서 먼저 반영하고, 이동은 그 프레임의 yaw를 읽는다. 카메라 위치는 플레이어 이동 후 LateUpdate에서 계산한다. 일반 이동 중 캐릭터 회전을 카메라에 다시 반영하지 않으므로 회전 피드백 루프가 없다. 제자리 마우스 조작은 캐릭터를 돌리지 않는다.

위치의 수평/수직 추적에는 별도 SmoothDamp를 사용한다. 마우스 각도에는 위치 추적 필터를 적용하지 않으며, 픽셀 델타에 deltaTime을 다시 곱하지 않는다. WASD는 pitch와 무관한 평면 벡터를 사용한다. 캐릭터는 실제 수평 이동량이 있을 때만 부드럽게 회전한다.

카메라와 플레이어 사이를 SphereCast로 검사한다. 벽이 끼면 즉시 안전 거리로 당기고, 장애물이 사라질 때만 부드럽게 거리를 복원한다. 플레이어 하위 콜라이더, Ignore Raycast 레이어, 트리거, 명시적으로 등록한 Collision Ignore Roots는 제외한다. 현재 Map에는 독립된 능력 공전 구체가 없으며, 향후 구체는 플레이어 자식 또는 해당 목록으로 제외할 수 있다. 테스트에서는 별도 구체 콜라이더도 제외되는지 검사했다. 매우 가까워져 캡슐 내부를 보게 되는 경우 플레이어 Renderer만 임시 숨기고, 컷신/카메라 비활성화 때 원상 복구한다.

덩굴·잎 탑승 중에는 진행 목표 쪽으로 천천히 보조 회전하고 전방에 추적 지점을 둔다. 마우스 입력을 주면 즉시 수동 회전이 우선하며, 설정한 지연 이후 보조 회전이 다시 작동한다. 일반 보행에는 이 자동 회전이 적용되지 않는다. 보조 지연은 게임 시간 기준으로 일시정지 중 진행하지 않는다.

리스폰/순간이동은 Snap으로 위치와 추적 속도를 즉시 초기화하고 새 위치의 캐릭터 뒤로 재배치한다. 오프닝/엔딩 등 컷신은 World의 BeginCinematic/EndCinematic으로 추적 컴포넌트와 입력을 함께 전환한다. 종료 시 새 플레이어 위치로 Snap한다. 커서 소유권은 World에 모았고, 카메라가 별도로 커서를 잠그지 않는다.

## Inspector 조절 항목

Hierarchy의 **Main Camera → EFMapCamera**에서 설정한다. 각도 값 Pitch/Yaw는 플레이 중 현재 시점 값이기도 하다.

| 항목 | 기본값 | 의미 |
|---|---:|---|
| Distance | 7m | 장애물이 없을 때 추적 거리 |
| Look Height | .7m | 플레이어 중심 위의 바라보는 높이. 2m 캡슐 기준 지상 약 1.7m |
| Shoulder Offset | .6m | 중앙 조준을 위한 옆 구도 |
| Sensitivity | .14 | 마우스 1픽셀당 수평 회전 각도 |
| Vertical Sensitivity | .85 | 수직 감도 배율 |
| Min Pitch / Max Pitch | -35° / 70° | 위/아래 시점 한계 |
| Invert Y | 끔 | 수직 입력 반전 |
| Recenter Pitch | 18° | C 정렬 시 각도 |
| Follow Smooth Time | .10초 | 수평 위치 추적 부드러움 |
| Vertical Smooth Time | .14초 | 점프/경사/발판의 수직 추적 부드러움 |
| Max Follow Lag | 1.5m | 추적 지점이 목표에서 늦게 따라가는 최대 거리 |
| Collision Radius / Padding | .25m / .12m | 벽 검사 반경과 여유 |
| Collision Return Smooth Time | .25초 | 장애물 제거 후 거리 복귀 부드러움 |
| Collision Mask / Ignore Roots | Ignore Raycast 제외 / 플레이어 | 검사 레이어 및 제외할 오브젝트 루트 |
| Hide Player Distance | 1m | 아주 가까운 시점의 캡슐 가림 방지 |
| Assist Rides | 켬 | 덩굴/잎의 진행 방향 보조 구도 |
| Ride Assist Delay | 1.25초 | 수동 마우스 조작 후 보조 복귀 지연 |
| Ride Turn Speed | 90°/초 | 탑승 중 보조 회전 속도 |
| Ride Look Ahead | 1.5m | 탑승 중 전방 추적 거리 |

플레이어의 **EFMapPlayer → Turn Sharpness(14)**는 이동 방향으로 회전하는 반응 속도다. 값이 클수록 빠르게 돌아선다. Distance 등의 저장값은 카메라 적용 메뉴를 다시 실행해도 초기화되지 않는다.

## 변경 파일

- 수정: `Assets/EverflowMap/Runtime/EFMapCamera.cs`, `EFMapPlayer.cs`, `EFMapWorld.cs`.
- 신규: `Assets/EverflowMap/Runtime/EFMapInput.cs` 및 기존 입력 재사용.
- 실제 에셋 수정: `Assets/Scenes/Everflow_Map.unity`, `Assets/EverflowMap/Prefabs/EFMapPlayer.prefab`.
- 신규 편집 도구/검사: `Assets/EverflowMap/Editor/EFMapCameraSetup.cs`, `EFMapCameraTests.cs`.
- 검사 확장: `Assets/EverflowMap/Editor/EFMapPlayTests.cs`, `EFMapVerification.cs`.
- FBX, 모델 루트 배치, 기존 .meta GUID, Graybox/6Puzzles 씬, Packages, 게임 버전은 유지했다. 빌드는 생성하지 않았다.

## 검증 결과와 한계

**Unity 컴파일/저장:** 새 코드의 Unity 컴파일 및 실제 씬/프리팹 저장 성공. 참조 검사 통과. 카메라 적용 메뉴와 기존 적용 메뉴를 반복 실행해도 씬 바이트·오브젝트 수가 같고 시점 설정이 유지됨을 확인했다. 기존 MVP 상태 검사와 Graybox/6Puzzles 참조·로드 검사도 통과했다.

**카메라 전용 Play Mode: 34개 통과.** 실제 Update/LateUpdate, CharacterController와 물리 쿼리를 사용했다. 일반 이동·캐릭터 회전·독립된 자유 시점·상하 제한·C 정렬·위치 감쇠·정지 시 안정화·벽 회피/거리 복귀·구체 제외·점프·중앙 Raycast 선택/사용·10m 사거리·가림·Esc 일시정지·컷신 복귀·리스폰을 검사했다. 벽/능력 대상은 런타임 전용 테스트 오브젝트이며 씬에 저장하지 않았다. [원문](camera/camera-test-results.txt)

**새 카메라로 퍼즐 이동 Play Mode: 94개 통과.** 시작 바닥부터 꽃·덩굴·파편·석판·회전 다리·능선·슬라이드·문/계단·최종 복원까지 실제 플레이어 이동을 재검사했다. 덩굴과 활강 중 착지 지점의 화면 좌표가 화면 안에 있음을 검사하고 카메라 캡처도 확인했다. 실제 엔딩 전환, Continue exploring의 복귀 콜백과 엔딩 재진입 방지, 전체 재시작, 타이틀 버튼 진입도 검사했다. 능력은 대부분 API 호출이며 실제 마우스 조준 검사는 별도 전용 테스트의 중앙 Raycast 경로에서 수행했다. [원문](map-integration/play-test-results.txt)

**정적/Editor 검사:** [Editor 원문](map-integration/editor-test-results.txt), [원본 보존 검사](map-integration/preservation-results.json). `git diff --check` 공백 오류 없음.

**미검증 범위:** 사람이 마우스로 직접 연속 플레이한 조작감, 모든 지형/기둥에서의 가림과 순간 변화, 모든 탑승 중 수동 시점 조합, 여러 FPS/해상도, 실제 Alt-Tab과 포커스 복귀, OS 커서 포획은 미검증이다. 자동 검사는 입력 델타·키·클릭을 주입했다. 배치 Editor에는 포커스된 Game View가 없어 `Cursor.lockState` 실측값은 None, visible=False였다. 게임/메뉴의 잠금 요청 상태와 전환 로직 통과를 **OS에서 실제 커서가 잠긴 것까지 확인한 결과로 해석하면 안 된다**. 대화형 Play에서 Game View를 클릭하고 확인할 수 있다.

정지 상태 위치 안정화와 실제 점프/경사/회전 발판 통과는 검사했지만, 눈으로 느끼는 모든 떨림이 없다고 보증하지 않는다. 독립 실행 파일 빌드/실행도 하지 않았다.

캡처는 Camera.Render 결과이므로 중앙 조준점·IMGUI HUD/버튼은 이미지에 포함되지 않는다: [시작](camera/start-follow-camera.png), [리스폰](camera/respawn-follow-camera.png), [덩굴](map-integration/camera-vine-ride.png), [활강](map-integration/camera-leaf-ride.png).

검사 메뉴는 `Tools > EVERFLOW > Map > Run Third Person Camera Tests`, `Run Physical and State Tests`, `Run Saved Scene and Regression Checks`이다. `Apply Third Person Camera`는 이미 실행·저장되어 있다. 이 문서가 앞선 FBX 통합 문서의 우클릭/포인터 카메라 설명을 대체한다.
