# 타이틀과 퍼즐 진행

## 기존 구조와 변경 범위

Unity 6000.3.21f1 / URP 17.3.0 / Input System 1.20.0 / uGUI 2.0.0이며 activeInputHandler는 Both다. 실제 게임은 `Assets/Scenes/Everflow_Map.unity`, 타이틀은 `Assets/EverflowMVP/Scenes/Everflow_Title.unity`다. 기존 Graybox/MVP 씬과 원본 FBX·이미지 픽셀, 플레이어 이동·카메라·BLOOM/FREEZE/PULSE 규칙을 유지했다. 기존 사용자 변경사항과 빌드 씬 등록도 보존했다.

맵의 진행 주체는 `EFMapWorld`다. `Completed`, `StageIndex`, `Checkpoint`, 각 단계의 nodes/requirements/restoration이 연결된다. 다음 퍼즐 제한은 `EFMapNode.CanApply`의 `stage != world.StageIndex || world.Completed[stage]` 검사에서 발생한다. 기존 중간 체크포인트인 `EFMapCheckpoint`의 요구 조건은 유지한다.

## 타이틀 이미지·버튼

지정 파일 둘을 실제로 열어 확인했다. `EVERFLOW_main.png`는 1672×941이고, 왼쪽 약 X=0.244에 로고가 포함되어 있다. `EVERFLOW_start_button.png`는 2172×724이며 한국어 '게임 시작'이 이미 포함되어 있다. 이미지 대체나 생성을 하지 않았다.

기존 Canvas에 `Main Composition`을 추가하고 AspectRatioFitter=FitInParent, 비율 1672/941로 설정했다. 기존 `EVERFLOW Logo` Image를 재사용해 전체 메인 이미지로 교체하고 composition 전체에 맞췄다. 원본 로고를 담은 별도 Image를 추가하지 않았다. 화면 비율이 달라지면 전체 이미지를 보존하고 남는 부분은 기존 배경색으로 표시한다.

기존 `Start Game` Button은 composition 내부 앵커 **(0.244, 0.44)**에 배치했다. 이미지 안 로고 아래, 왼쪽 로고의 중심선과 맞춘 위치다. 로고와 버튼이 같은 비율 유지 부모에 속하므로 16:9/16:10에서 상대 배치가 유지된다. 기존 Version은 composition보다 위에 그려진다. 기존 타이틀에 종료·설정 버튼은 없었다.

버튼의 클릭 Rect는 450×96, 자식 `Button artwork`는 460×153.33이다. 투명 여백을 포함하는 전체 이미지보다 클릭 높이를 줄여 보이는 버튼 몸체에 맞췄다. 자식은 부모 중앙 앵커다. 기존 Label은 참조를 보존한 채 비활성화해 텍스트 중복을 없앴다. 배경·메인 이미지·버튼 artwork의 Raycast Target은 꺼져 있고, Button 부모의 투명 Image만 입력을 받는다.

기존 Button→`EFTitleScreen.StartGame` 영구 이벤트를 유지했다. 목적 씬은 이미 사용자가 연결한 `Everflow_Map`을 그대로 사용한다. `loading`과 `startButton.interactable=false`로 연속 로드를 막는다. Single 비동기 로드로 기존 타이틀 Canvas/EventSystem/Camera가 해제된다. 타이틀은 커서를 표시·해제하고, 게임에선 기존 `EFMapWorld`가 인트로→플레이 전환 시 커서를 잠그고 숨긴다. hover=흰색, 평상시=.94, 눌림=.8의 작은 밝기 변화만 준다.

이미지 두 개는 씬에 직접 참조되며 Sprite 임포트 설정도 저장했다. `EFReleaseSetup`의 로고 검증 경로를 새 메인 이미지에 맞췄다.

배치 수정은 타이틀의 `Main Composition > Start Game` 앵커/Rect, `Button artwork > Image/Rect`, `EVERFLOW Logo > Image`, composition의 AspectRatioFitter에서 한다. 화면 전체 CanvasScaler 기준은 1920×1080, Expand다.

## 현재 6개 퍼즐과 통과 인정

기존에는 **출구에 도달한 후에도** 모든 `ConditionsMet` 조건이 참이어야 진행했다. 기존 정상 `TryArrive` 경로를 유지하고, 같은 출구 영역에 도달하면 세부 조건을 재검사하지 않는 `TryPassage`를 추가했다. 둘 다 `CompleteTraversal` 한 함수로 확정한다.

판정은 거리 반경이 아닌 arrival의 로컬 공간 3D 박스다. P1~P4는 실제 맵에 이미 저장된 출구/착지 영역을 재사용했다. P5는 문 너머의 기존 진입 경계를 유지하면서 진행 방향으로 넓혔다. 새로운 지름길 판정이나 추가 능력 사용 순서를 넣지 않았다. 아래 좌표는 **EF_Terrain 기준 상대 좌표**이며, 해당 지형 원점의 월드 위치는 (9.45035, 0, -3.14175)다. 크기는 반경이 아닌 전체 X/Y/Z 길이이다.

| 퍼즐 | 기존 정상 조건 + 출구 도달 | 통과 인정 위치와 중심 (X,Y,Z) | 전체 크기 |
|---|---|---|---|
| P1 꽃·덩굴 | 꽃/덩굴 성장 완료, FlowerUsed/VineUsed, 탑승 해제 | 덩굴을 건넌 동쪽 착지대, P2 진입 전 (44, 4.0824, 21) | 6×5×7 |
| P2 파편·석판 | 파편 3개 FREEZE 및 높이 오차 ≤0.55m, 세 파편 실제 밟기, 석판 FREEZE 및 각도 정렬 | 석판 남쪽 출구, P3 전원 접근 전 (44, 8.3867, -40) | 6×5×7 |
| P3 회전 다리 | 전원 PULSE 완료, 다리 FREEZE 및 로컬 0도 기준 허용 오차(8도) 내 정렬 | 다리 서쪽 끝 능선 진입부 (18, 8.5058, -42) | 6×5×7 |
| P4 얼음 슬라이드 | 전원 PULSE, 수문 애니메이션 완료, 물 공급·FREEZE, SlideUsed, 탑승 해제 | 실제 잎 착지대, P5 문 앞 구역 (-26, 10.9456, 8) | 6×5×7 |
| P5 뿌리·문 | 뿌리 BLOOM/전선 성장 완료, 같은 뿌리 PULSE를 통해 문 개방 완료 | 문을 통과한 계단 입구 (-13.5, 13.5, 5) | 4×6×6 |
| P6 최종 수로·물레방아 | 전원 PULSE, 최종 수문 완료, 수로 뿌리 수리 완료, 마지막 수로 FREEZE 및 각도 오차 7도 이내, 기존 물 전파 대기 | **통과 인정 없음** | 기존 참고 arrival는 엔딩 트리거가 아님 |

P2의 기존 석판 정렬 허용 오차는 씬의 Node tolerance를 따른다. 상세 모델/조건은 `EFMapNode.Satisfied`, `EFMapWorld.ConditionsMet`, 해당 씬의 requirements를 따른다. 옛 기획서의 퍼즐 구성으로 바꾸지 않았다.

P5는 이전 중심 (-14.5,13,5), 크기 2×5×5에서 변경했다. 새 영역의 진행 방향 시작 경계 X=-15.5는 이전과 같다. 문 위치 X≈-18.6보다 충분히 뒤쪽이며 문 앞 접근 (-20.5,11,5)은 포함하지 않는다. P1~P5 출발점, 근접한 이전 경로, 아래층은 박스에 포함되지 않고 각 박스끼리도 겹치지 않음을 수치 검사했다. 실제 착지 및 비정상 우회 경로는 Play Mode에서 추가 확인해야 한다.

`EFMapWorld.LateUpdate`는 실행 순서 -200으로 **플레이어 이동 후, 플레이어 능력 조준/클릭 LateUpdate 전**에 도달을 검사한다. 따라서 도착한 프레임의 다음 퍼즐 능력 클릭부터 진행 제한이 해제된다. 5~7m 규모 박스의 플레이어 중심을 검사하므로 일반 걷기·점프·슬라이드에서 한 점을 정확히 밟을 필요가 없다. Rigidbody/트리거 이벤트 의존성을 추가하지 않았다.

## 판정 영역 수정

Map 씬의 `EF_MapSystems > P1 arrival`부터 `P5 arrival`까지 선택한다. Scene View의 Gizmos를 켜면 연두색 박스와 대상 P번호/다음 P번호가 표시된다. `EFMapPassage`는 이를 시각화하는 컴포넌트이며 실행 판정·중복 상태를 별도로 갖지 않는다.

- 위치/방향: 해당 arrival의 Transform을 수정한다.
- 크기: `EF_MapSystems > EFMapWorld > Stages > 해당 index > Arrival Size`를 수정한다.
- 대상: 동일 stage의 Arrival 참조와 표시용 `EFMapPassage.stage`를 일치시킨다. index는 P1=0부터 P5=4다.
- 크기는 arrival 로컬 공간 기준이므로 Transform Scale은 1을 유지한다. 출구 이후의 안전한 지형과 점프 높이를 포함하고, 장애물 전 접근 지형이나 인접 층을 포함하지 않게 한다.
- P6에 통과 인정 구역을 추가하지 않는다. `TryPassage`와 `TryArrive` 모두 최종 단계의 접근 클리어를 거부한다.

월드 좌표·상대 좌표의 정확한 저장값은 `docs/ui-progress/passage-layout.json`에 있다.

## 클리어 유지·중복·초기화

`CompleteTraversal`은 먼저 `Completed[index]=true`로 확정하고 해당 복원 Begin, 다음 StageIndex, 다음 spawn 체크포인트와 힌트 갱신을 기존과 같이 실행한다. 플레이어를 이동시키거나 노드를 초기화하지 않는다. 두 진입 API는 현재 StageIndex인지, 아직 미완료인지, 일반 퍼즐인지 검사하므로 재진입이나 정상/통과 경로의 중복 확정이 불가능하다. 기존 애니메이션 Begin도 Started 기준으로 재시작을 막는다.

한번 인정한 Completed를 각도·FREEZE·사용 이력에 따라 다시 false로 만드는 코드를 추가하지 않았다. Respawn은 기존 퍼즐 상태를 보존하고 현재 체크포인트로 복귀한다. R은 현재 미완료 단계만 재시도하며 이전 완료와 복원은 유지한다. 별도 저장 시스템을 만들지 않았다. Restart world는 게임 씬을 Single 재로드하고, 타이틀 Start Game도 새 게임 씬을 로드하므로 기존 정책대로 Awake/Start에서 6단계 진행과 연출을 초기화한다. Continue exploring은 기존 진행을 유지한다.

P6의 `ConditionsMet(5)`→물 전파→물레방아 회전→복원/엔딩 카메라 조건은 유지한다. P5의 통과 인정은 P6 능력만 열고 P6 완료나 엔딩을 실행하지 않는다.

## 검증 결과와 남은 확인

실제 수행:

- Unity 6000.3.21f1에 맞는 로컬 DLL 참조로 런타임 33개, 에디터 23개 C# 파일 컴파일. UNITY_EDITOR를 제거한 플레이어 조건의 런타임 33개도 컴파일. 오류/경고 없음. **Unity 에셋 임포트나 실제 빌드 결과는 아님.**
- 저장된 씬의 로컬 참조, 스크립트 GUID, UI 계층, 이미지 직접 참조, 입력 차단 설정, 정확한 한국어 문자열, 16:9/16:10 및 축소 해상도 Rect 수치, 출구 전후/높이/박스 비겹침 등 226개 정적 검사 통과.
- 작업 전 맵 410개 객체가 모두 남아 있고, 프리팹 인스턴스 블록 38개(기존 플레이어 1개와 원본 모델 37개)는 줄바꿈 정규화 후 동일하다. 기존 맵 블록의 변경은 다섯 영역의 표시 컴포넌트 등록, HUD 부모 자식 목록, P5 도달 영역 위치/크기로 제한했다.
- 원본 이미지들을 직접 열어 구도 확인. 아이콘 알파 영역을 측정했으며 PNG 자체는 변경하지 않았다.

실행 제한:

Unity 배치 에디터가 LicenseClient-vzd12 IPC 연결 거부/초기화 실패로 멈춰 작업용 프로세스를 종료했다. `docs/ui-progress/unity-execution-status.txt`에 근거를 남겼다. 따라서 **이번 변경의 Play Mode 결과, 새 콘솔 오류 부재, 실제 스크린샷, Windows 플레이어 빌드는 검증하지 못했다.** 씬은 Unity 텍스트 직렬화로 저장하고 별도 참조/컴파일 검사를 수행했다.

Unity에서 이어서 실행할 검사:

1. 씬을 열어 에셋 재임포트/스크립트 컴파일 완료와 Console 오류를 확인한다. 지정 이미지가 Sprite로 인식되는지 확인한다.
2. `Tools > EVERFLOW > Presentation > Run UI and Passage Regression Tests`: 실제 타이틀 Button 포인터 이벤트/레이캐스트, 중복 시작 방지, Map 로딩, 한국어 글리프와 1/2/3 동기화, 도달 영역별 제한 해제, 재진입/상태 변경/리스폰/R 후 완료 유지, 새 게임 초기화, P6 근접만으로 엔딩이 실행되지 않는지 검사한다. 도달 위치 테스트는 명시적 텔레포트 fixture이며 물리 횡단 검증과 구분된다. 그래픽 장치가 있으면 실제 Canvas 캡처도 생성한다. 아직 실행하지 않았다.
3. 기존 `Tools > EVERFLOW > Map > Run Physical and State Tests`: 꽃/덩굴/파편/다리/슬라이드/문/최종 물 전파까지 기존 입력·CharacterController 횡단과 정상 엔딩을 회귀 검사한다. 기존 `Run Third Person Camera Tests`도 사용할 수 있다. 이번 작업에서는 재실행하지 못했다.
4. 수동으로 각 퍼즐을 충분히 건넜지만 각도/사용 이력이 부족한 상태, 출구의 가장자리/점프/착지, 가까운 옆길·아래층·문 앞, 왕복·재시작·연속 클릭, 16:9/16:10의 UI와 커서/시점 조작을 확인한다. 최종 조건 미충족/정상 충족에서 엔딩을 각각 확인한다.
5. 필요한 Windows 빌드를 생성해 직접 참조된 이미지, 한국어 OS 폰트, 씬 이동과 진행을 최종 확인한다.

검사 자료: `docs/ui-progress/compile-results.json`, `static-results.json`, `static-results.txt`, `map-before-object-hashes.json`. 재검사 도구는 `tools/check_ui_progress_compile.py`, `tools/check_ui_progress_scene.py`다.

## 변경 파일

| 파일 | 내용 |
|---|---|
| `Assets/Scenes/Everflow_Map.unity` | 하단 HUD, 출구 표시 5개, P5 영역 확대 |
| `Assets/EverflowMVP/Scenes/Everflow_Title.unity` | 메인 구도/시작 버튼 이미지와 배치 |
| `Assets/EverflowMap/Runtime/EFMapAbilityHUD.cs` (+meta) | 실제 능력 이벤트 구독, 표시와 한국어 폰트 |
| `Assets/EverflowMap/Runtime/EFMapPassage.cs` (+meta) | 기존 도달 영역의 Scene View 표시 |
| `Assets/EverflowMap/Runtime/EFMapPlayer.cs` | 선택 이벤트, 기존 직렬화 선택값 호환 |
| `Assets/EverflowMap/Runtime/EFMapWorld.cs` | 통과 인정·공유 확정 경로·최종 단계 보호·하단 기존 텍스트 위치 |
| `Assets/EverflowMap/Editor/EFPresentationSetup.cs` (+meta) | 중복 없는 씬 적용/참조 설정 도구 |
| `Assets/EverflowMap/Editor/EFPresentationTests.cs` (+meta) | Unity에서 실행할 UI/진행 회귀 검사 |
| `Assets/EverflowMap/Editor/EFMapSetup.cs` | 기존 맵 적용 경로에 HUD/영역 표시 업그레이드 통합 |
| `Assets/EverflowMVP/Runtime/EFTitleScreen.cs` | 기존 Map 연결·로드 중복 방지 유지, 이미지 버튼에 맞춰 중복 텍스트/폰트 코드 정리 |
| `Assets/EverflowMVP/Editor/EFReleaseSetup.cs` | 검증 로고 경로를 메인 이미지로 변경 |
| `Assets/icon/{bloom,freeze,pulse}_icon.png.meta` | Sprite·알파·스케일 설정 |
| `Assets/logo/EVERFLOW_{main,start_button}.png.meta` | Sprite·알파·스케일 설정 |
| `docs/AbilityHUD.md`, `docs/TitleAndPuzzleProgress.md`, `docs/ui-progress/*` | 구조/조정 방법/검사 증거·제한 |
| `tools/check_ui_progress_compile.py`, `tools/check_ui_progress_scene.py` | 실행 검증과 구분된 로컬 정적 검사 |
