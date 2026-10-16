# EVERFLOW FBX 맵 통합 결과

후속 작업으로 자유 시점 카메라와 중앙 조준을 적용했다. 현재 카메라 조작·Inspector·검증 결과는 [자유 시점 카메라 문서](EVERFLOW_third_person_camera.md)를 따른다. 아래 우클릭/포인터 조작 설명은 최초 통합 당시의 기록이다.

작업 대상은 `Assets/Scenes/Everflow_Map.unity`이다. Unity API로 씬과 플레이어 프리팹, 재질 및 이동 보정 메시를 **실제로 생성·저장했다**. 적용 스크립트만 제공한 상태가 아니다. 아래의 자동 Play Mode 검사는 사람의 마우스 플레이나 8~9분 플레이 시간 측정을 뜻하지 않는다.

## 전달받은 설명과 직접 확인한 사실

| 항목 | 전달 내용 | 프로젝트에서 확인한 결과 |
|---|---|---|
| 작업 지침 | AGENTS.md 우선 | 프로젝트 및 확인한 상위 경로에서 AGENTS.md 없음. 기존 `지시사항.txt`를 UTF-8로 읽음. 이 파일의 이전 목표인 6Puzzles 진입보다 이번 요청의 Map 진입을 우선함 |
| Unity | Unity 프로젝트 | 6000.3.21f1, URP 17.3.0, Input System 1.20.0, activeInputHandler=2(Both) |
| 주요 씬 | Map, Graybox, 퍼즐 검증 씬 | 세 씬 모두 존재. 타이틀은 `Assets/EverflowMVP/Scenes/Everflow_Title.unity` |
| 개별 모델 | 37개 | `Assets/map_ver1/fbx`에 정확히 37 FBX. 참고용 전체 FBX는 폴더 밖에 별도로 1개. 통합 씬에 참고용 FBX를 추가하지 않음 |
| 기존 배치 | 사용자가 이미 배치 | Map에 개별 FBX 인스턴스 37개, 카메라와 광원 존재. 게임 플레이어/퍼즐 매니저는 없었음 |
| 지형 충돌 | Generate Colliders 필요 | EF_Terrain은 이미 활성화되어 있었음. 중복 지형 콜라이더를 추가하지 않음 |
| 단위 | 미터, 캐릭터 2m | globalScale=1, fileScale=0.01, 배치 루트 scale=1. 기존 캡슐/CharacterController 높이 2m 사용. 실제 모델 내부 높이는 0/3/7.5/24/10/15m 구성과 대응 |
| 애니메이션 | 개별 프레임 범위 | 애니메이션 FBX 13개 모두 실제 24 FPS, Take 001, 1~120프레임, 길이 4.958333초, Loop 꺼짐. Transform 곡선 경로 확인 |
| 재질 | 파이프라인에 연결 | 조회된 모델 재질은 URP/Lit. 누락된 Renderer 재질/셰이더 참조 없음. 제공 폴더에는 별도 텍스처 파일이 없으며 기존 단색 재질을 유지 |
| FREEZE 제한 | 기존 규칙 확인 필요 | 기존 MVP/Graybox에 동시 유지 개수·구체 회수 제약 없음. 새 맵도 여러 파편을 동시에 동결 가능 |
| Restore_P4 | 폭포와 물가 풀 | 실제 FBX 계층·클립에는 `R4_Waterfall`의 스케일 애니메이션만 확인. 물가 풀 모델/곡선은 확인되지 않음 |

원본 상세 조사: [model-audit.txt](map-integration/model-audit.txt), [지형 높이 이미지](map-integration/terrain-height.png). 지형 이미지는 Physics Raycast 샘플이며 카메라 캡처가 아니다.

## 변경한 실제 에셋과 코드

- `Assets/Scenes/Everflow_Map.unity`: 기존 모델을 EF_World 아래 월드 Transform 보존 상태로 정리. EF_MapSystems, 플레이어 인스턴스, 퍼즐 참조, 도착/체크포인트, 카메라, 물 표현 연결.
- `Assets/EverflowMap/Prefabs/EFMapPlayer.prefab`: 기존 캡슐 플레이어의 이동 방식을 옮긴 전용 프리팹. 높이 2, 반경 .4, 이동속도 6, 중력 24, 일반 점프 높이 1.35. stepOffset .55, 능력 사거리 10(기존 15).
- `Assets/EverflowMap/Runtime/`: EFMapPlayer, EFMapCamera, EFMapNode, EFMapWorld, EFMapAnimation, EFMapCheckpoint. 입력은 기존 `EverflowGraybox.EFInput`을 그대로 사용한다. 기존 이동·카메라·능력/진행 방식을 FBX 피벗·애니메이션과 새 완료 조건에 맞춰 분리했다.
- `Assets/EverflowMap/Materials/`: Ice, Flow. 슬라이드의 `IceSlide_Surface`에만 얼음 재질 적용.
- `Assets/EverflowMap/StairWalkSurface.asset`, `TerrainStairOpening.asset`: 계단 이동 보정용 씬 참조 메시. 원본 FBX와 임포터는 바꾸지 않음.
- `Assets/EverflowMap/Editor/`: 모델 조사, 멱등 적용, 지형 보정, 참조/회귀 검사, 실제 플레이어 물리 테스트.
- `Assets/EverflowMVP/Runtime/EFTitleScreen.cs`: 기존 시작 버튼 이벤트가 `Everflow_Map`을 로드하도록 경로 변경. 타이틀 씬의 로고·버튼 배치는 보존.
- `Assets/EverflowMVP/Editor/EFReleaseSetup.cs`: 기존 릴리스 검증이 새 맵 플레이어와 6개 구간 참조도 검사하도록 확장. 기존 MVP 검사 분기 유지.
- `ProjectSettings/EditorBuildSettings.asset`: 타이틀 다음에 Map을 활성 등록. 기존 6Puzzles 등록도 보존.
- `tools/map_inspect.py`, `tools/verify_map_integration.py`: 로컬 지형 분석 및 원본 보존 검사. 추가 패키지 설치 없음.

기존 씬은 런타임 로드·복제하지 않는다. Graybox 생성기는 메뉴 실행용이며 기존 씬 덮어쓰기를 거부한다. MVP의 InitializeOnLoad는 재질 준비용이고 맵을 재생성하지 않는다. 새 씬에서도 런타임 회색 맵 생성은 없다.

## 여섯 구간 연결

| 구간 | 연결·완료 판정 | Unity Play Mode 검사 |
|---|---|---|
| ① | 꽃 BLOOM 완료 후 돔 충돌 활성. 돔에서 Space 도약. 덩굴 BLOOM 완료 후 실제 손잡이 E 탑승, Space 이탈. 꽃/덩굴 사용 및 동쪽 착지 모두 필요 | 시작 바닥에서 일반 점프로 돔 접근, 꽃 도약, 덩굴 접근·탑승·이탈·동쪽 착지 및 다음 구간 진입 통과 |
| ② | 3개 파편의 현재 모델 위치를 정답 높이로 보존. 위에서 내려오며 반복 재시도. FREEZE 허용 오차 .55m 안에서 정답에 맞춤. 3개 모두 올바른 동결·실제 밟기, 석판 정렬/동결, 출구 도착 필요 | 세 파편 연속 점프, 석판 보행, 오동결 거부·R 초기화, 동시 FREEZE 유지 통과 |
| ③ | 전원 PULSE 후 회전. 모델 하위 다리 피벗의 **원래 회전 상대 0°**에서 동결, 서쪽 출구 도착 | 무전력 동결 방지, 정렬·보행 통과. 회전 중에도 플레이어가 다리 충돌체를 따라가는 검사 통과. 능선으로 24m 상단까지 보행 통과 |
| ④ | PULSE → 수문 클립 완료 → 두 수로 물 표시 → 슬라이드만 FREEZE → E 잎 탑승. 경로 가속/캡슐 sweep 충돌 확인, 착지 시 하차/컨트롤러 복구. 실제 탑승·착지와 얼음 상태 필요 | 건조 수로 동결 방지, 옆 수로 물 유지, 전체 활강·비행·⑤ 착지·조작 복구 통과 |
| ⑤ | 문 옆 뿌리가 BLOOM/PULSE 공용 단자. Wire 클립 완료 전 전력 차단. 원격 전원 모델은 능력 대상 아님. 뿌리 PULSE 후 문짝 클립, 문 너머 도착 필요 | 성장 전/중 PULSE 우회 방지, 문 개방·통과·계단 보행 통과 |
| ⑥ | 전원 PULSE 및 수문 완료 + 뿌리/수로 조각 BLOOM 완료 + 마지막 수로 올바른 동결 → 물을 구간별로 표시 → 바퀴 가동 → Restore_P6 → 엔딩 카메라 | 전원과 뿌리에 실제 보행 접근 및 정상 사거리/가림 검사 통과. 마지막 정렬 전 바퀴 정지, 정렬 후 물길/회전/엔딩 통과. Wheel만 회전하고 Body는 고정됨 확인 |

꽃·덩굴·수리·문·수문의 기능 완료는 외형 애니메이션 종료 후 성립한다. 회전은 원본 하위 모델의 로컬 회전을 기준으로 하므로 월드 회전값을 0으로 강제하지 않는다. 소스 전력은 이 맵에서 퍼즐 진행용으로 켜짐 상태를 유지한다.

## 애니메이션·복원·재시도

수동 PlayableGraph를 사용하여 프레임을 실제 clip.frameRate로 변환한다. Take의 시작 프레임 1을 빼므로 프레임 1은 클립 시간 0이다. 예를 들어 꽃은 49/24초 동안 동작한다. 시작 시 첫 자세를 평가하며 자동 재생하지 않고, 마지막 자세를 유지한다. 덩굴의 흔들림은 스케일 성장 곡선이 없는 상위 피벗에만 적용하여 Transform 충돌을 피한다.

동작 범위는 제공 명세를 적용했다: 꽃/전선/Restore_P1 1~50, 덩굴 1~40, 문/수문 1~30, 수로 수리 1~42, Restore_P2 1~55, Restore_P4 1~110, ValleyWater 20~120, Restore_P5 1~75, Restore_P6 1~80.

도착 영역과 해결 조건을 함께 검사한다. ①/②/④/⑤의 지정 도착에서 해당 복원, ⑥은 실제 물 도달·바퀴 가동 후 복원한다. Restore_P3는 만들지 않았다. Begin 반복 호출은 완료된 클립을 다시 재생하지 않는다. 모델 교체는 노드의 actions 및 단계의 restoration 참조를 교체하면 된다.

추락은 체크포인트 복귀이며 퍼즐 상태를 보존한다. R은 현재 미완료 구간의 상태와 해당 체크포인트 등록만 초기화한다. 이전 복원은 유지한다. 엔딩의 Restart world는 씬을 다시 로드하여 모델·애니메이션·회전·전력·탑승·진행·카메라를 모두 초기화한다.

## 실제 검사로 보정한 부분

1. **시작 카메라:** 원래 생성 위치 뒤 절벽 때문에 카메라가 캡슐에 지나치게 접근했다. 생성 스폰만 남쪽으로 2m 이동하고 추적 거리/방향을 보정했다. 별도 3초 오프닝 카메라로 중앙 물레방아를 보여준다. 일반 시작 추적 시점에서는 지형에 가려 바퀴가 보이지 않는다. 주요 경로 모든 시점의 목표물 가시성은 검증하지 않았다.
2. **계단:** Play Mode에서 문을 열고도 첫 계단에 걸렸고, CapsuleCast로 T_Stairs의 수직 면을 확인했다. 기존 계단 MeshCollider 하나의 sharedMesh만 이동용 경사면으로 대체했다. 이후 중간에서 지형과 계단의 겹침이 발견되어, 계단 폭 3.9m 구역의 지형 삼각형 36개를 잘라낸 씬 전용 메시를 Renderer와 기존 Collider에 함께 연결했다. 원본 재질/submesh와 모든 모델 루트 Transform을 유지했다. 수정 후 문→계단→고원 보행 통과.
3. **꽃/덩굴 이동:** 서로 다른 월드 축으로 배치된 새 지형에 맞춰 목적지 방향의 도약 보조를 적용했다. 물리 로프 시뮬레이션은 아니며 기존 회색 맵처럼 조작 가능한 이동 보조다. 일반 점프 성능은 유지했다.
4. **잎 이동:** 최초 잎 위치, 슬라이드 상단/하단, 비행 구간, ⑤ 착지를 별도 경로 참조로 연결했다. 최대 18m/s, 가속도 8. 라이딩 중 캡슐 sweep으로 벽 충돌 시 체크포인트로 복귀한다. 자유 물리 탈것이나 조향 시스템은 아니다.

## 물 표현과 임시 요소

기존 모델의 슬라이드/옆 수로 수면, ValleyWater 상승, Restore_P4 폭포, Restore_P6의 3갈래 물/폭포 애니메이션을 사용한다. ⑥의 수리 수로를 따라 바퀴까지 도달하는 물은 얇은 수면 메시 8개를 순차 표시하는 임시 표현이다. 복잡한 유체 시뮬레이션을 추가하지 않았다. 얼음은 단색 URP 재질이며 플레이어 외형도 기존 캡슐 방식이다.

## 검증 증거와 한계

- [Play Mode 원문](map-integration/play-test-results.txt): **89개 검사 통과**. 실제 EFMapPlayer.Update와 CharacterController, 1/60초 입력 스크립트를 이용한 물리/상태 검사. 꽃 위/각 구간 시작으로 테스트용 이동을 시키지 않고 시작 바닥부터 검사한다. 의도적인 추락 시험과 게임 자체의 R/재시작은 포함한다. 능력 선택 키, Space, E는 플레이어 입력 경로로 검사했다. 능력 적용은 대부분 노드 API, 일부는 CastAt의 실제 거리/가림 판정으로 실행했다. 실제 OS 마우스 클릭/우클릭 드래그는 자동 조작하지 않았다.
- [Edit Mode 원문](map-integration/editor-test-results.txt): Apply 두 번 후 씬 바이트와 오브젝트 수 동일, 매니저 1개, 13개 클립·6구간 참조, 기존 타이틀 릴리스 검사, MVP 상태 검사, Graybox 참조 검사, 원본 6Puzzles 로드 통과.
- [정적 보존 검사](map-integration/preservation-results.json): 83개 원본 파일 해시에서 Map 씬만 변경. FBX·기존 .meta GUID·Graybox/6Puzzles 원본 보존. 기존 37개 모델의 위치/회전 직렬화 값 보존, 적용 시 Unity에서도 월드 스케일까지 비교했다. 기존 로고/URP 아이콘 사용자 변경은 그대로 둠.
- Unity 컴파일 오류 및 최종 Play Mode 누락 참조/예외 없음. 라이선스 갱신 관련 404 로그는 있었으나 각 배치 실행과 테스트가 정상 종료했다. 이를 게임 컴파일 오류로 집계하지 않았다.
- 기존 Graybox/MVP는 원본 코드/씬을 유지했다. 회귀 확인은 상태/참조/씬 로드 범위이며 기존 두 씬의 전체 물리 플레이를 재실행하지는 않았다.
- 사람의 연속 마우스 플레이, 모든 점프·덩굴 이탈 타이밍, 카메라 조준/가림, 모든 해상도/프레임레이트, 비정상 경로 우회, 모든 잎 충돌 실패 경로는 미검증이다. **8~9분 플레이 시간은 미측정**이며 자동 테스트 시간을 플레이 시간으로 보고하지 않는다.
- Windows 플레이어 빌드는 만들지 않았다. 프로젝트 버전은 **1.0.2 그대로**, 신규 빌드 출력 없음. 기존 릴리스 빌드 도구의 성공 시 패치 증가/실패 시 롤백은 유지된다.

실제 Unity 카메라 캡처: [오프닝 목표](map-integration/opening-goal-camera.png), [시작 추적 카메라](map-integration/start-camera.png), [꽃 착지](map-integration/p1-flower-landing.png), [④ 착지](map-integration/p4-landing.png), [최종 물길·바퀴](map-integration/ending-camera.png). Camera.Render 캡처여서 IMGUI HUD/버튼은 이미지에 포함되지 않는다.

## Unity에서 사용하기

별도 적용 메뉴를 실행할 필요 없이 타이틀 씬을 열고 Play → 게임 시작으로 Map에 진입한다. Map을 직접 열고 Play해도 ①에서 시작한다. WASD 이동, 우클릭 드래그 시점, 1/2/3 능력 선택, 좌클릭 사용, Space 점프/덩굴 이탈, E 탑승, R 현재 구간 재시도. ⑤는 **문 옆 뿌리에 BLOOM 후 같은 뿌리에 PULSE**한다.

도구는 `Tools > EVERFLOW > Map`에 있다. Apply Integration은 이미 적용되어 있으며 반복 시 현재 씬을 재생성하지 않는다. Validate Saved Integration은 Map을 연 상태에서 사용한다. Run Physical and State Tests는 Play Mode에 진입하므로 저장 안내를 확인한 뒤 실행한다. Run Saved Scene and Regression Checks는 씬/참조·중복·기존 검사를 실행한다.

## 팀원에게 요청할 모델 수정

1. EF_Terrain의 계단 입구 높이와 중앙 고원 절벽이 T_Stairs의 실제 계단 경로를 가리지 않도록 모델링 수정. 현재는 원본을 보존하는 씬 메시 오버라이드로 통과시켰다. FBX 교체 후 이동/렌더 보정 메시를 제거하고 재검사해야 한다.
2. Restore_P4의 명세상 물가 풀 모델과 애니메이션을 제공하거나, 폭포만 제공하는 것이 맞는지 확인. 현재 파일에서는 물가 풀을 찾지 못했다.
3. 향후 교체 시 현재 피벗/Transform 경로와 24 FPS 프레임 원점을 유지하거나 변경표 제공. 특히 Vine_Handle, IceSlide_Surface, 문짝/수문판, SM_Waterwheel_Wheel 경로 변경은 연결 갱신이 필요하다. 현재 이 부위들의 피벗 오류 때문에 구현이 막힌 상태는 아니다.
