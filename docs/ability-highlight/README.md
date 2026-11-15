# 실제 물체 능력 강조와 간결한 HUD

대상: `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`. `Everflow_Map` 원본 씬은 UI 참고만 했으며 변경하지 않았다.

## 동작

- BLOOM 연두색, FREEZE 하늘색, FLOW 주황색. 능력 모드에서 사거리·사용 조건을 만족하는 실제 물체에 표면/가장자리 강조를 표시한다. 커서 또는 FLOW 선택 대상은 더 선명하다.
- 기존 원본 재질과 물 셰이더를 교체하지 않고, 동일한 메시 위의 깊이 검사를 사용하는 강조 렌더러를 재사용한다. 벽 너머에 강조를 그리지 않는다. 탐색·일시정지·연출에서는 강조를 끈다.
- 구체 대상 표시와 날아가는 구체·궤적, 임시 블록형 3D 방향 화살표를 제거했다. FLOW 방향은 간결한 화면 버튼과 기존 E/Tab/휠로 선택한다. 원래 능력의 도착 시점·적용·효과 유지 로직은 보이지 않는 내부 상태로 유지해 퍼즐 조건을 보존한다.
- 실제 뿌리·잎·풍차·분기 장치·수로·물 표면을 커서로 선택한다. 기존 구체 트리거는 선택 Raycast에서 제외한다. 기존 장애물 검사와 UI 클릭 차단을 유지한다.
- 이동 물체와 구조물이 같은 모델을 공유할 경우 현재 능력에 맞는 대상을 선택한다. 원본 FBX의 Read/Write나 충돌체를 바꾸지 않고, 선택용 삼각형 데이터만 별도 저장했다.
- A와 D4의 충돌체 없는 마른 뿌리 외형은 기존 상호작용 지점에 맞춰 이동했다. A의 기존 장식은 둔덕 뒤에 있었고 D4 뿌리는 기존 발판 아래에 묻혀 있어, 구체를 없앤 상태에서 실제 모델 선택이 불가능했다. 발판이나 충돌체는 이동하지 않았다.
- HUD는 작은 세 아이콘·이름·얇은 안내 띠 형태. 탐색 중에는 세 아이콘의 크기/밝기/이름 색이 같으며 선택 밑줄을 그리지 않는다. 능력 모드에서만 선택한 이름과 아이콘을 강조한다.
- 큰 검은 HUD 패널과 연출 제목의 검은 박스를 제거했다. 카메라는 전체 뷰포트다. 중복 월드 디버그 안내문도 다시 켜지지 않게 했다. 도움말·일시정지는 별도의 반투명 녹색 계열로 유지한다.

## 변경 파일

- Runtime: `EFV2AbilityTargets.cs`, `EFV2TargetMeshCache.cs` 신규. `EFV2PuzzleAssembly.cs`, `EFV2PuzzleWorld.cs`, `EFV2Guidance.cs`, `EFV2ScenePresentation.cs`, `EFV2Billboard.cs`, `EFDemoFeedback.cs` 변경.
- Shader/material/data: `EF_AbilityTargetGlow.shader`, `AbilityTargetGlow.mat`, `AbilityPickingData.asset` 신규.
- Editor: `EFV2AbilityTargetTools.cs` 신규, `EFV2PuzzlePlayChecks.cs` 실제 표면 선택 검사·녹화 업데이트.
- 대상 씬: 강조 재질과 선택 데이터 참조만 추가. 기존 카메라 설정·물길·충돌·퍼즐 설정 보존. Cinemachine이 에디터 미리보기 카메라의 계산된 위치를 저장할 수 있다.

FBX 모델의 메시 자체를 수정했다면, 대상 씬의 Edit Mode에서 `Tools > EVERFLOW > V2 Puzzles > Rebuild Ability Surface Picking Data`를 실행해 선택 데이터를 다시 생성한다. 현재 데이터는 34개 메시 / 9,992개 정점이며 원본 임포트 설정은 변경하지 않았다.

## 검증

실제 Unity Play에서 Input System 키보드·마우스를 주입했다. 실제 물체 표면 선택, 능력 모드 정지, 사용/방향 확정, 기존 적용 과정을 거친다. 연속 경로는 Teleport·직접 Apply·강제 문 개방·장치 위치 지정으로 우회하지 않는다.

- 입력·취소·UI/벽/사거리 차단·리스폰·재시작·강조 해제·구체 미표시: 55개, 실패 0 (`runtime-synthetic-input-smoke.txt`).
- A 잎 / C 잎 / G 수차 / D3 얼음 / D5 정렬·고정 / D4 수차 / 해동 / 정원 / Enter 재시작: 63개, 실패 0 (`runtime-route-alternate.txt`).
- A/B 뿌리 / G 석문 / D3 덩굴 / D5 우회 / D4 뿌리 / D2 주 출구 / 정원 / Enter 재시작: 46개, 실패 0 (`runtime-route-bloom.txt`).
- 세 색상과 새 HUD 녹화 시퀀스: 10개, 실패 0 (`runtime-tactical-review.txt`).

`review-bloom-selection.png`, `review-flow-selection.png`, `review-freeze-selection.png`, `exploration.png`와 `interaction-review.mp4`에 실제 화면을 보관한다. 카메라 중심 구도, 물 공급·동결 조건과 최근 수로 체적 개선은 유지한다. 유료 에셋이나 패키지 추가 없음.

사람의 직접 조작 체감, 여러 화면 비율과 OS Alt+Tab은 별도의 수동 점검 범위다. 포커스 상태 복구는 Unity 콜백 주입으로 검사했다.

최종: 총 174개 검사, 실패 0. 최종 MP4는 약 14.57초, 1280×720 / 30fps / 음향 포함. 실제 파일의 3·8·12초 프레임을 Unity VideoPlayer로 디코딩해 확인했다. Play 종료, 원래 Run In Background 설정 복구.
