# Everflow_Map 능력 오류 조사·검증

검사일: 2026-10-16. Unity **6000.3.21f1**, URP 17.3.0, Input System 1.20.0, Both 입력 설정. 대상은 `Assets/Scenes/Everflow_Map.unity`이며 Graybox와 6Puzzles를 재생성하거나 변경하지 않았다. 현재 사용법은 [PlayGuide](Everflow_Map_PlayGuide.md)를 따른다. 기존 FBX 통합·카메라 문서는 이전 작업의 기록이다.

## 증상과 수정 전 재현

꽃 BLOOM과 꽃 점프는 동작하지만 이후 대상에 능력이 발동하지 않는 증상이 보고되었다. 수정 전 Unity Play Mode에서 **선택 키 → 중앙 카메라 Raycast → 클릭 → 능력 실행**을 사용하도록 기존 물리 검사를 보강했다. 꽃 개화, 일반 점프로 꽃 돔에 올라가기, 강화 도약 후 착지까지 통과한 뒤, 보이는 덩굴 끝을 조준할 때 `Hovered == null`로 실패했다.

원문: [수정 전 입력 실패](ability-validation/baseline-input-failure.txt), [수정 전 Collider 조사](ability-validation/baseline-colliders.txt). [작업 직전 씬 사본](ability-validation/Everflow_Map.before-ability-fix.unity.txt)도 보존했다. 자동 입력과 카메라 방향 설정으로 재현한 결과이며 사람이 마우스로 플레이했다는 뜻은 아니다.

### 확인한 원인과 원인이 아닌 항목

| 구분 | 실제 근거·판정 |
|---|---|
| **대상 탐지 실패: 덩굴** | 꽃에는 독립 Trigger가 모델을 덮고 있었다. 덩굴에는 물리 모델 Collider가 없고, 별도 Trigger의 월드 Y 범위가 **4~9m**였다. 미성장 덩굴 끝/손잡이의 Y는 **10.86m**라 보이는 새싹을 조준해도 맞지 않았다. 함수로 `vine.Apply`를 호출한 기존 검사는 이 오류를 검출하지 못했다. |
| **조건 차단: 이후 퍼즐** | StageIndex는 꽃 도약·덩굴 실제 탑승/이탈·도착을 모두 만족해야 증가한다. ①에서 멈추면 이후 대상을 사용하려 해도 이전 `Aim`은 단계 불일치를 조용히 무시했다. 이후 오브젝트 전부의 입력이 고장 난 것으로 느껴질 수 있는 연결 원인이다. 모든 후속 대상의 Collider가 누락된 것은 아니다. |
| 카메라 변경과 인과관계 | 중앙 조준은 이미 적용되어 있었다. 덩굴 Collider와 보이는 모델의 불일치는 카메라 종류와 독립적인 씬 연결 오류다. 카메라 변경을 단일 원인으로 단정하지 않는다. 다만 코드상 Player.Update의 조준은 Camera.LateUpdate보다 먼저 실행되어 이동·회전한 프레임에 이전 화면 기준 Ray를 사용했다. 이를 보정하고 회귀 검사했다. |
| 입력 | 1/2/3과 클릭은 정상 경로가 존재했다. Mouse 클릭은 Map 확장에서 읽고 선택은 기존 Graybox 입력을 재사용한다. 원본 입력 액션 누락이 원인이 아니다. |
| Layer·부모 검색 | 기본 대상은 Default 레이어, 플레이어와 체크포인트는 Ignore Raycast(2). 기존 `GetComponentInParent<EFMapNode>`는 여러 단계 FBX 부모까지 검색했다. 전체적인 부모 탐색 누락은 없었다. 기능 부모와 모델 부모를 분리할 경우도 지원하도록 명시적 수신 링크를 추가했다. |
| 거리·가림 | 사거리 10m와 플레이어 시야선 검사는 실제로 존재했다. 다리 중심 조준은 멀 수 있고 ④ 잎 옆에서 수면을 조준하면 수조 벽이 가릴 수 있다. 이런 정상 차단을 성공 처리하거나 벽을 무시하지 않았다. |
| 등록·참조·이벤트 | 6개 구간의 노드 등록·전력 supply·성장 actions·수문·문·물레방아 참조를 조사했다. 개별 능력은 UnityEvent 대신 `EFMapNode.Apply`와 supply 참조를 사용한다. 타이틀 버튼 이벤트는 Map 경로를 로드한다. |
| 구체·쿨다운 | 현재 Map에는 독립 구체 사용 중 상태, 구체 회수 키, 유지 개수 제한, 능력 쿨다운이 없다. 파편 3개와 석판을 함께 얼릴 수 있다. |
| 실행 후 외형 | 수동 PlayableGraph가 FBX의 지정 프레임만 샘플링하고 마지막 자세를 유지한다. 덩굴 흔들림은 성장하는 Rope/Handle 하위 스케일과 다른 상위 피벗에 적용한다. 물레방아는 Wheel만 회전한다. 실행 결과를 매 프레임 초기화하는 원인은 발견하지 않았다. |
| 시작·런타임 재생성 | 타이틀은 Single 로드로 Map에 진입한다. 저장된 Map 매니저·플레이어·카메라를 사용한다. Map 초기화에서 씬을 재생성하거나 Editor 연결을 제거하지 않는다. 기존 Graybox 생성기는 이 경로에서 실행되지 않는다. |

## 적용한 수정

- **EFMapPlayer.cs**: 조준과 클릭을 Camera.LateUpdate 이후 실행. 같은 프레임에 렌더할 화면 중앙을 사용한다. 대상 없는 클릭도 실패 이유를 표시한다. 대상 탐지와 사용 가능 여부를 분리하고, 거리·능력·단계·선행 조건·플레이어 시야선의 이유를 제공한다. 플레이어 하위 Collider 및 지정한 독립 구체 루트를 제외하고, 무관한 Trigger는 건너뛰며 실제 고체 장애물은 유지한다.
- **EFMapAbilityTarget.cs**: Collider의 수신 노드를 명시적으로 연결한다. 분리된 기능/모델 계층도 지원한다. 명시 링크가 없으면 기존 부모 검색을 사용한다. 링크의 receiver가 null이면 물리 장애물이며 능력 대상은 아니다.
- **EFMapNode.cs**: `CanApply`를 실행과 미리보기에서 공유한다. 능력 조건을 UI와 실제 실행에서 동일하게 유지한다. 모든 대상을 성공 처리하는 우회는 없다.
- **EFMapWorld.cs**: 하단 대상·이유 안내와 청록/노랑 조준점. 기존 단계·실제 탑승/통과 조건과 체크포인트를 유지한다. 저장된 구간 안내에도 파편 순차 접근, 가까운 다리 상판, 수조 테두리에서 수면 조준 방법을 반영했다.
- **EFMapAbilitySetup.cs / EFMapSetup.cs**: 기존 씬을 재생성하지 않는 보정. 덩굴 Trigger는 로컬 중심 `(0,-3.5,0)`, 크기 `(2,8,2)`로 새싹과 성장 후 손잡이를 덮고 흔들림 피벗을 따른다. 슬라이드는 입구 수면의 query Trigger와 물리 얼음 MeshCollider를 분리한다. 수조·수문·슬라이드 기둥·옆 수로·잎을 수면 능력 대상으로 취급하지 않는다. 콜라이더별 수신 링크는 한 개만 붙인다.
- **실제 저장 에셋**: `Assets/Scenes/Everflow_Map.unity`, `Assets/EverflowMap/Prefabs/EFMapPlayer.prefab`. 프리팹에는 조준 LayerMask·무시 루트·진단 옵션이 저장되어 있다. 새 능력 컴포넌트와 Editor 도구의 `.meta`도 포함한다.
- **검사**: `EFMapPlayTests.cs`는 통과에 필요한 14개 능력 대상 모두 선택/클릭 입력과 실제 카메라 Ray로 사용하도록 보강했다. `EFMapAbilityRegression.cs`는 계층·가림·입력·실패 이유 회귀 검사다. 일부 실패 조건·이동 플랫폼 추종 검사는 API로 따로 실행하며 실제 조준 검사로 집계하지 않는다. `EFMapVerification.cs`, `EFMapAbilityTests.cs`는 저장 참조와 재실행 멱등성을 검사한다.

진단은 EFMapPlayer의 `logAbilityAttempts`와 `drawAbilityGizmos`로 선택한다. 기본 OFF. 로그는 능력 클릭 시에만 출력하며 프레임마다 콘솔에 쓰지 않는다. Gizmo는 선택한 플레이어에서 카메라 Ray·플레이어 LOS·맞힌 Collider를 보여준다. 상태는 `Hovered`/`AimReady`/`AimReason`/`LastCastResult`로 구분하며 `LastCastFrame`으로 실행 프레임도 확인한다.

## 대상별 연결과 조건

아래 수신자는 모두 `EFMapNode`이며 Collider에 `EFMapAbilityTarget.receiver`로 연결했다. 대상의 FBX 루트 이름과 실제 수신 노드 이름은 같다.

| 구간·씬 오브젝트 | Kind / 수신 Collider | 능력·조건 | 기대 변화 |
|---|---|---|---|
| ① SM_FlowerJumpPad | Flower / 독립 Ability target Trigger | BLOOM, 현재 ① | 지정 프레임 개화 완료 → 돔 MeshCollider 켜짐 → 위에서 Space 도약 |
| ① SM_Vine | Vine / 피벗 하위 Ability target Trigger | BLOOM, 현재 ① | 성장 완료 → Handle E 접근 판정 → 흔들림 → Space 이탈 |
| ② SM_FallingDebris_01~03 | Debris / 이동 모델 하위 Trigger, 동결 후 실제 MeshCollider | FREEZE, 현재 ② | 낙하 높이 유지. 원래 배치 높이에서 ±0.55m 이내면 정답으로 맞춤 |
| ② SM_Slate | Slate / 모델 MeshCollider | FREEZE, 수평 ±8° | 올바른 자세로 고정, 보행 통과 |
| ③ SM_PowerSource_P3 | Source / Base·Crystal MeshCollider | PULSE, 현재 ③ | 전원 켜짐 → 다리 회전 |
| ③ SM_RotatingBridge | Bridge / 모델 MeshCollider | 전원 ON 후 FREEZE, 정답 ±8° | 동서 연결 자세 고정 → 서쪽 출구 통과 |
| ④ SM_PowerSource_P4 | Source / Base·Crystal MeshCollider | PULSE, 현재 ④ | supply → SM_WaterGate_P4의 Gate animation |
| ④ SM_IceSlide / IceSlide_Surface | Water / 별도 입구 수면 Trigger + 동결 후 Surface MeshCollider | 수문 animation 완료 후 FREEZE | 해당 수면만 얼음 재질·물리 충돌 활성 |
| ④ SM_RideLeaf | 별도 능력 수신 노드 없음 / Water.rideVisual | E, 잎 3.5m 이내, Filled && Frozen | 자동 경로 이동, 벽 sweep, 도착 시 컨트롤러 복구 |
| ④ SM_SideChannel | 능력 수신 없음 | 수문 완료 시 물 표시 | FREEZE 후에도 원래 물 재질 유지 |
| ⑤ SM_VineRoot_P5 | Root / Trigger·뿌리 MeshCollider | BLOOM → Wire animation 완료 → **같은 뿌리에 PULSE** | supply → SM_Door_P5 문짝 animation |
| ⑤ SM_PowerSource_P5 | 직접 능력 수신 없음 / 원격 수정은 뿌리 indicator | 뿌리 relay를 통해 공급 | 원격 접근이나 사거리 확장 없이 덩굴 조건 유지 |
| ⑥ SM_PowerSource_P6 | Source / Base·Crystal MeshCollider | PULSE, 현재 ⑥ | supply → SM_WaterGate_P6 animation |
| ⑥ P6_ChannelRepair | Repair / 독립 Trigger | BLOOM, 수리 animation 완료 | 수로 조각·뿌리 복구 |
| ⑥ SM_Channel_Swing_P6 | Slate / 모델 MeshCollider | FREEZE, 정답 ±7° | 마지막 수로 정렬 고정 |
| ⑥ SM_Waterwheel_Wheel | World.wheel Transform | ⑥ 수문 완료 + 수리 완료 + 올바른 동결 | 물 전파 → Wheel 회전 → Restore_P6 → 엔딩 |

Gate와 Door는 직접 능력을 받지 않는다. 슬라이드 Channel/Pillar에는 null 수신 링크를 둬 고체 차폐·충돌을 유지한다. 슬라이드의 표면 Trigger는 수문 전에도 존재하므로 물리 발판이 꺼져 있다는 이유만으로 능력 판정이 사라지지 않는다.

## 검증 방식과 결과

이번 수정에서 수행한 결과다. 이전 통합 작업의 89개 검사 기록과 별개다.

- **Unity 컴파일**: 실제 Editor 배치 실행 성공. C# 컴파일 오류·MissingReferenceException·NullReferenceException 검출 없음. 라이선스 그룹 조회의 404 메시지는 있었으나 검사 실행·종료는 성공했다.
- **Edit Mode 자동 확인: PASS**. [원문](ability-validation/editor-test-results.txt), [능력 연결·멱등성](ability-validation/saved-reference-results.txt). 기존 통합/카메라/능력 보정 각각 반복 실행 후 씬 바이트·오브젝트 수 유지, 능력 보정 반복 후 프리팹 바이트 유지, 매니저/플레이어/카메라 중복 없음, 노드 등록·13개 실제 클립·수신 링크 검증. 기존 MVP 상태/릴리스 트랜잭션 검사, Graybox 참조와 원래 6Puzzles 씬 로드도 통과했다. 기존 씬 전체 플레이 재검사를 뜻하지 않는다.
- **Unity 자동 Play Mode 전체 순회: 148개 PASS**. [원문](ability-validation/input-traversal-results.txt). 그래픽 렌더링을 켠 실행에서도 통과. 실제 타이틀 버튼 이벤트 → Map → 오프닝 → ①~⑥ 통과 → 최종 복원·바퀴·엔딩 → 탐색 복귀 → 전체 재시작 → 타이틀 재진입까지 검사했다.
- **카메라 자동 Play Mode: 34개 PASS**. [원문](ability-validation/camera-test-results.txt). 마우스 델타·상하 제한·카메라 기준 이동·C·벽 충돌·독립 구체 제외·일시정지·컷신·리스폰을 검사했다. 별도 능력 회귀에서는 회전과 클릭이 같은 프레임의 최종 카메라 Ray를 쓰는지 `LastCastFrame`까지 비교해 다음 프레임의 뒤늦은 성공을 배제했다.
- **원본 보존 정적 확인: PASS**. [원문](map-integration/preservation-results.json). 원본 기준 83개 파일 중 허용된 Map 씬만 변경, FBX 37개와 37개 원래 모델 루트의 Transform 직렬화 값 보존. 이번 수정 전 씬 사본도 따로 보존했다.
- **사람의 직접 플레이: 미검증**. 현재 도구로 Unity 네이티브 UI와 OS 키보드·마우스를 직접 조작하지 못했다. 자동 입력 검증과 구분한다.

| 구간 | 코드·저장 참조 확인 | Unity 자동 입력·조준·물리 이동 | 사람의 키보드·마우스 연속 플레이 |
|---|---|---|---|
| ① 꽃 / 덩굴 각각 | 확인 | **정상**: 둘 다 클릭으로 성장, 꽃 도약·E·Space 이탈·도착 | 미검증 |
| ② 파편 3개 / 석판 | 확인 | **정상**: 순차 접근·동결·각 파편 밟기·석판 통과. 동시 유지, 실제 클릭으로 해제·재사용, R과 추락 복귀 | 미검증 |
| ③ 전원 / 다리 | 확인 | **정상**: 수정 PULSE, 가까운 상판 FREEZE, 서쪽 통과. 이동 플랫폼 추종은 별도 API 동결 해제/복구로 검사 | 미검증 |
| ④ 전원 / 수문 / 수면 / 잎 | 확인 | **정상**: PULSE·수문 완료, 일반 점프로 수조 테두리 도달, 수면 FREEZE, 옆 수로 물 유지, 잎으로 복귀·E·활강·자동 하차·조작 복구 | 미검증 |
| ⑤ 뿌리 / 전선 / 문 | 확인 | **정상**: 성장 전/중 PULSE 클릭 차단, 뿌리 BLOOM 완료 후 같은 뿌리 PULSE, 문 개방·통과·계단 이동 | 미검증 |
| ⑥ 전원 / 수리 / 흔들 수로 / 바퀴 | 확인 | **정상**: 전원·수리·정렬 대상 클릭, 미정렬 시 종료 차단, 물 전파·Wheel만 회전·복원·엔딩 | 미검증 |

④에서는 잎 위치에서 수조 벽을 뚫고 동결하는 검사가 실패했고, 슬라이드 옆 경사로 내려가면 캐릭터 높이가 낮아져 수로 벽에 가리는 것도 확인했다. 정식 검사는 **수조 테두리에 일반 점프 → 수면 조준·동결 → 원래 잎으로 복귀**하는 경로로 통과했다. 벽 제거·레이어 예외·사거리 확장은 적용하지 않았다.

자동 검사는 EFInput/EFMapInput에 결정적 입력을 주고 실제 Update/LateUpdate·Physics Raycast·CharacterController·PlayableGraph를 실행한다. 카메라 방향은 시험 코드가 설정한다. 이는 실제 맵 Collider·입력 소비·상태·이동을 검사하지만 **사람의 수동 조준 난이도, OS 마우스 이벤트, 게임 창 포커스, 전체 플레이 시간**을 증명하지 않는다. 타이틀 버튼 이벤트 실행 역시 실제 OS 버튼 클릭 검증과 구별한다.

회귀 fixture는 원래 맵 밖 임시 바닥에 배치해 플레이어 자식·독립 구체·일반 Trigger·고체 장애물·분리 계층을 검사한 뒤 제거하고 시작 체크포인트로 돌아온다. 이 격리 시험의 순간이동을 퍼즐 통과로 집계하지 않는다. 퍼즐 순회는 시작부터 실제 이동·점프·E·Space로 진행한다.

검사 중 저장한 Camera.Render 이미지도 확인했다: [덩굴 탑승](ability-validation/camera-vine-ride.png), [수조 테두리의 수면 조준](ability-validation/p4-surface-aim.png), [잎 착지](ability-validation/p4-landing.png), [최종 물길과 물레방아](ability-validation/ending-camera.png). IMGUI 조준점·HUD는 Camera.Render에 포함되지 않으므로 이 이미지로 UI 표시까지 검증했다고 판단하지 않는다. 성장 전 덩굴 끝을 올려다본 시선을 유지하면 성장한 손잡이가 화면 아래로 벗어날 수 있어, 가이드에 시선을 아래로 내리는 절차를 적었다.

## Unity 후속 확인과 남은 범위

수정은 저장되어 있어 별도 Apply가 필요 없다. Unity에서 다음 순서로 확인한다.

1. 프로젝트를 열고 컴파일 완료 후 Console의 스크립트 오류·Missing Reference 여부를 확인한다.
2. 타이틀 씬 Play → 게임 시작 → 오프닝 이후 1/2/3, 마우스 회전과 중앙 조준을 확인한다.
3. [PlayGuide](Everflow_Map_PlayGuide.md)를 따라 ①~⑥을 진행한다. 특히 첫 꽃 다음 덩굴의 새싹, ③ 다리 가까운 상판, ④ 수면과 잎의 별도 판정, ⑤ 같은 뿌리에 BLOOM/PULSE를 확인한다.
4. 잘못된 능력, 장애물 뒤, 사거리 밖, 선행 조건 부족 상태에서 하단·상단 차단 안내를 확인한다. FREEZE 재사용 해제, 추락 복귀, R, 전체 재시작도 확인한다.
5. 자동 검사를 다시 실행하려면 작업 중 씬을 저장하고 Play를 종료한 뒤 `Tools > EVERFLOW > Map > Run Physical and State Tests`, `Run Third Person Camera Tests`, `Run Saved Scene and Regression Checks`를 실행한다. 검사 도중 임시 fixture가 생성되나 씬에는 저장하지 않는다.
6. 모델 교체 등으로 연결을 보정해야 할 때만 `Repair Ability Targets`를 실행한다. 기존 통합 Apply에서도 같은 보정을 호출한다. 저장 씬/프리팹 재실행 멱등성을 자동 검사한다.

미검증: 사람의 전체 수동 플레이, 모든 마우스 각도/프레임레이트/해상도, 실제 OS 포커스 전환, 모든 잎 충돌 실패 경로, 별도 Windows 빌드 실행. 플레이 시간은 측정하지 않았다. 수동 로프 물리·잎 조향·수동 하차·독립 구체 투척/회수는 현재 미구현이다. 이전 문서에 기록된 Restore_P4 물가 풀 모델 부재는 이번 코드 수정으로 새 모델을 만들지 않았으며 남아 있다.

기존 사용자 로고·아이콘 변경과 타이틀/빌드 설정 변경은 유지했다. FBX 원본, 모델 루트 배치, 기존 Graybox/6Puzzles를 보존한다. 이번 작업은 릴리스 빌드 생성이나 버전 증가를 수행하지 않는다.
