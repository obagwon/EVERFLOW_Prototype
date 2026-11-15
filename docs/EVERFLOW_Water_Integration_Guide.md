# EVERFLOW 물 표현 통합 가이드

## 2026-11-08: 맵 전체 연속 물길 적용

현재 통합 씬에는 기존 39개 물·거품 구간을 **15개 연속 표면**으로 묶은 새 표현이 적용되어 있다. 아래 기존 통합 설명의 상자형 메시·구간별 투영 제한은 초기 버전 기록이며, 최신 메시/셰이더 구성은 `water-validation/network/README.md`를 기준으로 한다.

- 지상 상류→G→누수 S1/S2/S3→낙수→A 연못 입구를 한 표면으로 연결했다.
- 지하 정상 공급·우회·누수·D2 각 분기·협곡·복원 정원은 서로 다른 활성/동결 조건을 유지하면서 표면을 연결했다. 원래 GameObject와 퍼즐 회로는 유지하고 중복 Renderer만 숨긴다.
- 공통 전용 재질은 `Assets/EverflowWater/Network/ContinuousWater.mat`, 셰이더는 `EVERFLOW/Water/Continuous Network`이다. 정지 연못과 D2 수조는 별도 표면이며, D2 방향 변경/동결은 기존 상태와 연동된다.
- 재적용: **Tools → EVERFLOW → Water → Network → Apply All Continuous Waterways**.
- 이번 전체 적용만 되돌리기: **Tools → EVERFLOW → Water → Network → Restore Before Network**. 승인된 S3 시제품까지 포함한 직전 표현으로 돌아간다.
- 이전 Prototype/Continuity 메뉴는 과거 단계용이다. 전체 적용 후에는 위 Network 메뉴를 사용한다.

검토 영상과 구역별 화면은 `docs/water-validation/network/`에 저장했다. 공급/분기/동결/해동/복원 강제 상태 검사는 28개 통과했다. 실제 입력 경로 검사 결과는 해당 폴더의 최종 보고서를 참고한다.

## 열어 볼 씬과 위치

통합 씬은 `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`이다. Project 창에서 열고 Play한 뒤 Game View를 클릭한다. 기존 플레이어·Cinemachine·능력 조작을 사용한다. 물을 보기 위한 새 입력 관리자는 추가하지 않았다.

| 장소 | 대상 / 대략적인 월드 위치 | 확인할 표현 |
| --- | --- | --- |
| 시작 지점 앞 A 연못 | `W_A_Pond`, `(-45, -0.075, -21)` | 잔물결, 수심 색, 잎 아래 물. 정지한 연못에는 하류 이동을 강제하지 않음 |
| 지상 누수 수로 | `W_Leak_S1` → S2 → S3 → S4 | `+Z → +X → +Z → +Z`로 꺾이는 흐름과 낙수 |
| 지하 D5 | `W_D5_Fixed`, `(-56, -5.925, -66.5)` | 정렬·고정 공급과 내부 물 동결의 차이. 우회 수로는 별도 경로 |
| 지하 D2 | `W_D2_Basin`, `(-56, -11.925, -53)` | 정비 부두 / 작은 수차 / 주 출구 분기, 얼음과 해동 |
| 복원된 W 정원 | `W_R_WheelPool`, `(-85, -17.85, -5.75)` | 물레로 오는 낙수, 복원 후 수면과 섬 아래 폭포 |

격리 테스트 씬은 `Assets/Scenes/Everflow_Water_Test.unity`이다. 기존 `W_D2_Exit`의 13m × 3m 수로 메시를 형태·스케일 그대로 원점으로 옮긴 독립 씬이다. Play 후 `1` 공급 켜기/끄기, `2` 흐름 반전, `3` 동결/해동, `4` 표현 초기화를 시험한다. 이 입력은 테스트 씬에만 존재하며 맵의 FREEZE 범위와 무관하다. 빌드 씬 목록에는 두 씬을 추가하지 않았다.

## 적용 범위와 원본 보존

`EF_Map_v2`의 `GRP_Water` 22개와 `GRP_Water_Restored` 17개, 총 **39개**의 기존 물·거품 메시를 연결했다. 후자는 D5 우회/정상 공급, 주 출구, 협곡, 정원처럼 복원 과정에서 켜지는 물을 포함한다. 수면 위에 같은 크기의 새 평면을 포개지 않고 기존 MeshFilter/Renderer의 메시와 재질 참조를 씬에서 교체했다. 기존 위치·회전·높이·폭은 유지한다.

FBX 물 메시에는 정점 색이 없었다. 전용 메시 에셋에 원본 정점·삼각형·노멀·UV를 복제하고 정점 색을 흰색으로 명시했다. 원본 셰이더의 G 가시성 규칙이나 R 거품 마스크 때문에 전체가 사라지거나 하얗게 덮이는 문제를 피한다. 원본 FBX, 임포터, 외부 재질/텍스처/셰이더는 수정하지 않는다.

`W_*_Foam`은 수면이 아닌 기존 낙수 아래 거품 메시로 분류한다. 전용 셰이더의 거품 전용 모드로 그 메시 자체를 움직이는 무늬로 표시한다. 수직 낙수는 월드 X/Y 또는 Z/Y 투영과 `Vector3.down`을 사용한다. 긴 경사 수로 `W_Gorge`는 수평 계열 투영을 유지한다. 일반 물에 충돌체를 추가하지 않았고, 기존 FREEZE 얼음 지지대의 충돌체·크기·활성 조건은 그대로다.

원래 `Everflow_Map_v2_test.unity`, PuzzleLab, 팀원 FBX, 타이틀, 빌드 씬 목록, 패키지, 기존 사용자 변경은 작업 시작 해시와 비교한다. 보존 근거는 `water-validation/baseline-sha256.json`, `baseline-git.diff`와 최종 검사 보고서다.

## 현재 렌더링 설정

- 실제 Editor: **Unity 6000.3.21f1**, URP **17.3.0**, Cinemachine **3.1.7**.
- 현재 PC 품질: `Assets/Settings/PC_RPAsset.asset` → `PC_Renderer.asset`, **Forward+**, Direct3D12, Linear 색 공간.
- PC 파이프라인은 작업 전부터 Depth Texture와 Opaque Texture가 모두 활성화되어 있었다. 기존 URP/Renderer/색 공간을 변경할 필요가 없었다.
- 맵 Main Camera는 기존 URP Additional Camera Data의 파이프라인 설정을 따르며 실제 depth/opaque가 모두 활성 상태다. near/far는 `0.1 / 500`이다. 테스트 카메라만 두 텍스처를 명시적으로 요청한다.
- 외부 `RoadsideTalesBackroadBridges_ProjectSetup`은 실행하지 않았다. 데모 파이프라인으로 교체하지 않았다.

## 추가·변경 파일

| 파일 | 역할 |
| --- | --- |
| `Assets/EverflowWater/Shaders/EF_CoolWater.shader` | 원본 EA_CoolWater의 HLSL 노이즈/거품/빛무늬 코드를 이용한 전용 파생 셰이더. 이름 `EVERFLOW/Water/CoolWater` |
| `Assets/EverflowWater/Materials/EF_CoolWater.mat` | `Water_Experimental.mat`을 Unity CopyAsset으로 복사한 전용 재질. 새 셰이더/텍스처 GUID를 참조 |
| `Assets/EverflowWater/Textures/EF_WaterNormal.png` | `WaterNMP.png`의 별도 사본. Normal Map, sRGB off, Repeat, mipmap on |
| `Assets/EverflowWater/Textures/EF_WaterCaustics.bmp` | `CausticsRender_004 — kopia.bmp`의 새 GUID 사본. 빛무늬 계산에 실제 사용 |
| `Assets/EverflowWater/Meshes/W_*.asset` | 39개 원본 메시의 색 보정용 독립 사본 |
| `Assets/EverflowMap/Runtime/EFV2WaterIntegration.cs` | 씬의 구간 설정, 연속 이동 위상, 공급/동결 표시 연결 |
| `Assets/EverflowMap/Runtime/EFV2WaterPreview.cs` | 격리 테스트 씬 전용 입력 |
| `Assets/EverflowMap/Editor/EFV2WaterTools.cs` | 실측 감사, 사본 생성, 테스트 씬 생성, 통합·원복·참조 검사 |
| `Assets/EverflowMap/Editor/EFV2WaterPlayChecks.cs` | 실제 Play/URP 캡처와 표현 인터페이스·통합 상태 검사 |
| `Assets/EverflowMap/Editor/EFV2EditorBridge.cs` | 기존 프로젝트 전용 브리지에 고정된 물 작업 명령만 추가 |
| `Assets/EverflowMap/Runtime/EFV2PlayRecorder.cs` | 기존 관찰용 기록기를 Editor 폴더 밖으로 이동. GUID 유지, Editor 전용 컴파일. 일반 Play에서 부착을 거부하던 오류 수정 |
| `tools/create_everflow_water_shader.py` | 설치된 원본 셰이더에서 전용 사본을 재현하는 생성 도구 |
| `tools/verify_everflow_water.py` | 원본 보존·GUID·씬 참조·셰이더 제어 경로의 정적 검사 |
| `tools/request_v2_editor.py` | 물 검사 명령을 기존 Unity Editor에 전달 |

셰이더의 누락된 원본 CustomEditor 참조와 사용되지 않던 `_FoamFlowX/Z` 등 복사 재질의 오래된 속성을 제거했다. `_WaveHeight`는 이제 월드 미터 단위 정점 변위에 실제로 쓰인다. 맵에서는 구간별 MaterialPropertyBlock으로 덮어쓴다.

## Inspector 조절

Play를 끈 상태에서 Hierarchy의 **`EVERFLOW Water Presentation` → EFV2 Water Integration → Segments**를 펼친다. 각 요소의 Label이 원본 메시 이름이다.

| 항목 | 의미 / 기본값 |
| --- | --- |
| Direction | 월드 하류 벡터. 크기는 정규화하며 Speed를 별도로 사용. 예: `(0,0,1)`, `(-1,0,0)`, 낙수 `(0,-1,0)` |
| Speed | 무늬 이동 속도, m/s. 수로 `0.55`, 넓은 D2 수조 `0.38`, 낙수 `1.2`. 정지 연못은 `0` |
| Wave Height | 월드 단위 물결 높이. 기본 `0.018m`. 낙수/거품은 `0`. 기존 맵 수로의 약 0.15m 두께보다 작게 조정 |
| Foam | 구간별 표면 거품 양. 기본 수면 `0.24`, 연못 `0.07`, 낙수 `0.38`, 거품 전용 메시 `0.65`. 공통 Foam Color alpha와 함께 불투명도를 조정 |
| Kind | Surface / Waterfall / Foam. 수직 낙수는 다른 투영을 사용 |
| Ice | None / D5Internal / D2Basin. 기존 두 물 대상에만 연결. 플레이 가능한 새 FREEZE 대상을 생성하지 않음 |
| Arrival Delay | 같은 공급 그룹 안의 짧은 시각적 도착 지연. D5/협곡의 하류에 `0.08 / 0.16s` |
| Direction Smoothing | 속도/방향 전환 완화 `0.35s`. UV 좌표를 초기화하지 않음 |
| Arrival Seconds | 구간 내 상류→하류 노출 시간 `0.32s`. 퍼즐 판정이나 3초 성공 타이머를 지연시키지 않음 |

D2 Basin 방향은 공급 중 실제 D2 선택에 맞춰 갱신한다. 이 구간에서는 Inspector Direction 대신 회로의 방향을 따른다. 공급 전에도 있던 수조 물은 정지 상태로 남는다.

공통 색·광택·무늬 크기는 `EF_CoolWater.mat`에서 조절한다. Normal Tiling `0.5`, Foam Tiling `0.8`, Caustics Scale `0.65`는 모두 월드 미터 기준이다. 연결 구간에 서로 다른 재질을 복제하여 무늬 크기를 바꾸기보다 공통 재질을 유지한다. Refraction `0.005`, Specular Strength `0.12`, Caustic Strength `0.12`로 과한 반짝임을 낮췄다. 큰 거품 얼룩은 원본의 이중 임계값을 제거해 완화했다.

물결 속성 Ripple Rate는 잔물결 주기이며 하류 속도는 Segments의 Speed다. 방향을 바꾸면 누적 월드 이동 좌표는 이어지고 속도 벡터만 부드럽게 바뀐다. 셰이더 어디에서도 전역 `_Time`을 사용하지 않는다. 공급이 끊기면 표시는 즉시 사라지고, 동결되면 물결·거품·굴절 노멀·빛무늬가 공유하는 시간/이동 좌표가 멈춘다. 해동은 그 위상에서 이어진다.

## 퍼즐 상태 연결

`EFV2PuzzleWorld`의 실행 순서 300 이후에 표현 관리자가 순서 600으로 갱신된다. 물 공급 공식을 새로 계산하지 않고 기존 World가 실제 물 GameObject에 적용한 활성 상태를 읽는다. 시각 관리자는 능력 Apply, 문 판정, 성공 타이머, 물리, 해당 GameObject의 활성 상태를 변경하지 않는다.

| 기존 상태 | 연결되는 표현 |
| --- | --- |
| `supplyWater`, `bypassWater`, `leakWater` | 정상 공급 / BLOOM 우회 / 누수 경로. 원래 회로의 활성 결과를 그대로 사용 |
| `dockWater`, `turbineWater`, `mainWater` | D2 선택의 세 물길. 선택하지 않은 구간의 물·거품을 숨김 |
| `exitWater` | D5 공급 + D2 주 출구 + D4 개방 + 주 물길 얼음 없음이라는 기존 Arriving 결과 |
| `gardenWater` | 기존 Restored 판정. 복원이 완료되어 잠긴 정원 표시를 따름 |
| `ruinWater` | G의 실제 수차 방향 선택 결과 |
| `mainIce.Frozen` | `W_D2_Basin`의 모든 물 애니메이션 정지와 기존 얼음 지지대의 전용 얼음 재질 |
| `upperIce.Frozen` | D5 내부 물 표시 정지와 기존 상부 얼음 지지대. 하류 중단은 World의 원래 결과 |
| `channelStructure.Frozen` | 구조물 고정일 뿐 물 동결로 취급하지 않음 |

A/B/C의 발판 고정은 연못 동결로 확장하지 않았다. 공급 전에 존재하는 A/H 연못, D2 수조와 상류 누수는 맵의 기존 물 상태를 유지한다. D3 부두 물은 기존 World가 공급+부두 선택일 때만 켠다. 원본 상시 물에 새로운 공급 퍼즐을 만들지 않았다.

기존 `LabStreamMarker`의 노란 움직이는 검사 상자는 통합 씬의 런타임에서만 끈다. PuzzleLab 원본이나 공유 장치 스크립트는 수정하지 않는다. 게임 내 재시작에서는 World의 경과 시간 초기화를 감지해 표현 위상과 도착 표시도 초기화한다.

## 검증과 수정 이력

정적 검사와 실제 Unity Play 결과를 구분한다. 모든 캡처는 실제 URP Game View에서 얻은 1920×1080 이미지다.

| 검사 | 결과 / 근거 |
| --- | --- |
| 격리 수로 Play | 16개 통과. `water-validation/runtime-isolated.txt`. 공급/건조/도착/반전/동결/해동/초기화와 두 카메라 거리 |
| 통합 상태 Play | 23개 통과. `water-validation/runtime-integrated.txt`. **장치 상태를 강제로 설정한 검사**이며 보행 완주와 구분 |
| 일반 입력 연속 경로 회귀 | 뿌리 경로 18개, 잎·얼음·수차 경로 28개 통과. `water-validation/runtime-route-bloom.txt`, `runtime-route-alternate.txt`. 실제 키보드·마우스 이벤트로 수행하며 강제 장치 상태 검사와 구분 |
| C# | Unity 실제 임포트 컴파일 및 별도 Roslyn 런타임/Editor/플레이어 구성 검사. 플레이어 실행 파일을 빌드한 결과는 아님 |
| 셰이더 | Unity ShaderUtil과 실제 D3D12 렌더링. `shader-import.txt`, `unity-asset-verification.txt` |
| 파일·참조 보존 | `offline-water-results.json`. 정적 결과이며 Play 통과의 대용으로 쓰지 않음 |

격리 수로에서 흐르는 프레임의 실제 RGB 변화와 동결된 두 프레임의 RGB 차이 0을 확인했다. 정확한 수치는 최종 `runtime-isolated.txt`에 기록한다. 공급이 없을 때 물·거품이 사라지는 것도 건조 캡처와 비교했다.

연속 입력 경로는 Input System 테스트 키보드·마우스로 게임의 이동·조준·능력 사용·문 통과·정원 도착·Enter 재시작을 수행한다. 강제 순간이동이나 장치 Apply/문 Tick을 호출하는 경로가 아니다. 뿌리 경로는 S→A 뿌리→B→G 뿌리→D1→D3 덩굴→D5 우회→D4 뿌리→D2 주 출구→W→정원이고, 다른 경로는 A 잎→C→G 수차→D3 얼음→D5 정렬·고정→D4 수차→주 물길 동결·해동→W→정원이다. 물 표현 적용 후 정원까지 각각 **138.11초 / 136.08초**를 기록했다. World.Start부터 정원 도착까지의 unscaled 경과 시간이며, 사람이 처음 탐험하는 15~20분 기획 목표의 실측값이 아니다. 두 경로 모두 Enter 재시작까지 통과했다.

검증 중 고친 문제:

1. MaterialPropertyBlock을 MonoBehaviour 필드 초기화 시 생성하면 Unity의 역직렬화 단계에서 거부했다. Start의 초기화 단계에서 생성하도록 옮겼다.
2. 복원 후 물길은 별도 `GRP_Water_Restored`에 있었다. 최초 목록을 22개에서 39개로 확장했다.
3. 폭보다 높이만 비교하면 긴 경사 협곡을 폭포로 잘못 분류했다. 긴 변과 높이를 비교하도록 수정했다.
4. 복사 재질의 사용되지 않는 방향 속성, 원본의 누락된 CustomEditor, 원본 노멀의 일반 sRGB 임포트를 전용 사본에서 정리했다.
5. 원본의 거품 이중 임계값이 큰 얼룩을 만들었다. 한 번만 임계값을 적용하고 낙수는 세로 무늬로 바꿨다. 중첩된 뒷면 렌더도 줄였다.
6. 기존 관찰용 Play 기록기가 Editor 폴더에 있어 일반 Play에서 부착이 거부됐다. 원래 GUID로 Runtime 폴더에 옮기고 Editor 전용 컴파일 조건을 유지했다.
7. 첫 통합 검사 코드가 동결 해제를 `Apply(Freeze)`로 호출해 실패했다. 실제 게임이 쓰는 `Undo(Freeze)`로 검사 코드를 수정한 뒤 재실행했다. 게임 규칙은 바꾸지 않았다.
8. 원래 존재하던 연못/수조에는 공급 도착 컷을 적용하지 않아 재시작 순간의 물 깜빡임을 없앴다. D2 미선택 상태에서는 출구로 향하는 흐름을 표시하지 않는다.

테스트 프레임을 백그라운드에서도 실행하기 위해 일시적으로 켜진 PlayerSettings.runInBackground는 마지막 검증에서 작업 시작 값 0으로 원복했다. 렌더링 설정과 색 공간의 최종 변경은 없다. 이전 중단/실패 보고서는 `.previous` 파일로 남기며, 최종 결과는 이름이 고정된 최신 보고서를 기준으로 한다.

시각 근거: `test-flow-a/b.png`, `test-ice-a/b.png`, `test-dry.png`, `test-arrival.png`, `test-low-camera.png`, `test-distant-camera.png`, `map-d2-flow.png`, `map-d2-ice.png`, `map-restored-pool.png`, `map-restored-waterfall.png`. 통합 상태 캡처는 검사용 카메라를 별도로 배치했고, 플레이어를 해당 위치로 걸어 옮긴 결과가 아니다.

현재 제한:

- D3D12/PC/현재 URP에서 검증했다. Mobile 품질, 다른 그래픽 API, XR, 배포 빌드의 시각·성능은 미검증이다.
- 굽은 물길은 기존 S1/S2/S3/S4 등 모델 단위로 방향을 나눈다. 유체 시뮬레이션이나 굽이 전용 flow-map은 아니며 급한 연결부의 미세한 무늬 위상 차이가 남을 수 있다. 공통 월드 무늬 크기와 같은 수로 속도를 사용한다.
- 기존 상자형 물 메시와 맵의 보조 발판/회랑 형태는 그대로다. 큰 수면의 정점 수를 늘리는 세분화나 물 표면 반사는 추가하지 않았다.
- 상태별 자동 검사와 합성 입력 경로는 사람의 첫 플레이 가독성·장시간 탐험·모든 카메라 위치를 보장하지 않는다.

## 원래 표현으로 되돌리기

Play를 끄고 통합 씬을 연 다음 **Tools → EVERFLOW → Water → Restore Original Water In Puzzle Scene**을 실행한다. 각 구간에 저장한 Original Mesh / Original Materials를 복원하고 표현 관리자만 제거한다. 씬을 저장한다. 실행 직후에는 Unity Undo로 취소할 수 있다. 단순히 관리자 컴포넌트만 삭제하면 씬의 재질/메시 교체가 남으므로 이 메뉴를 사용한다.

전용 `Assets/EverflowWater` 에셋과 테스트 씬은 이 메뉴가 삭제하지 않는다. 다른 테스트/참조가 필요 없을 때 사용자가 별도로 정리할 수 있다. 원본 맵 씬과 FBX에는 애초에 변경이 없다.

재검증 도구: Unity 메뉴의 Water 항목 또는 기존 브리지의 `water-test`, `water-check`, `water-verify` 명령. `python tools/verify_everflow_water.py`는 정적 검사만 실행한다. 이전 `verify_v2_puzzles.py`의 “씬이 호스트 치환 외에는 원본 YAML과 동일” 검사는 물 통합 이전의 조건이므로, 물 통합 후 씬 보존 검사는 이 전용 검사와 Unity 참조 검사를 사용한다.
