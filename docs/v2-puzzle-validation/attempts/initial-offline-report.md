# v2.3 맵 퍼즐 구현 및 검증 기록

## 현재 판정

**독립 씬과 퍼즐 구현 초안을 만들고 오프라인 검사를 통과했다. Unity 초기화가 막혀 에셋 임포트, 실제 Play, 일반 입력에 의한 완주 및 수정 후 재Play는 수행하지 못했다. 요청한 최종 완료 기준은 아직 충족하지 않았다.** 아래의 구현 항목은 런타임 코드에 연결한 동작이며 실제 플레이 통과를 뜻하지 않는다.

## 여는 방법

- Unity 6000.3.21f1에서 `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`를 연 뒤 Play.
- 또는 `Tools > EVERFLOW > V2 Puzzles > Open Puzzle Scene`.
- 복제 씬 GUID: `815ede8c18e65ad5be5be5781bb09ea9`. 원본과 다르다.
- 원본의 FBX/플레이어/Cinemachine 인스턴스 YAML을 유지하고 탐색 관리자만 `EFV2PuzzleWorld`로 교체했다. 이 관리자는 Play의 Awake에서 모델 이름을 찾아 장치와 보조 형상을 구성한다. 퍼즐 보조 계층은 Play 중 `EVERFLOW V2.3 Puzzle World / V2 Puzzle Runtime...` 아래에 보인다. Edit Mode에 모든 장치를 미리 구운 씬은 아니다.
- 빌드 씬 목록을 변경하지 않았다. 새 씬은 에디터에서 단독 Play하는 용도이며, 기존 타이틀에서 자동 진입하거나 기존 플레이어 빌드에 포함되지는 않는다.

## 조작

WASD 이동, Space 점프, 마우스 시점, 1 BLOOM, 2 FREEZE, 3 FLOW. 좌클릭으로 사용한다. FLOW는 첫 클릭으로 방향을 표시하고 E/Tab/휠로 선택한 뒤 같은 대상을 다시 클릭해 확정한다. FREEZE는 다시 클릭하거나 X로 해제/해동한다. Q 수동 회수는 없다. R은 가까운 안전 지점으로 복귀하며, A/B/C의 해당 시작 지점에서는 해당 이동 발판만 복구한다. Esc는 일시정지/커서 해제다. 복원 후 Enter 또는 일시정지 메뉴에서 재시작한다.

구체는 세 개만 생성한다. 유효한 새 대상으로 즉시 경로를 바꾸며, 배치 후 플레이어와 24m를 넘으면 자동 귀환한다. 두 경우 모두 완료된 장치 상태는 유지한다. 비행 중 반복 클릭은 비행을 다시 시작하지 않고, 다른 대상 재사용은 이전 비행을 취소한다. 리스폰은 비행을 취소하고 구체를 회수한다. 선택·구체 상태·조준 대상·FLOW 방향·해동 여부를 HUD에 표시한다.

## 구간별 연결한 해법과 모델

| 구간 | 구현 초안 | 재사용한 모델 / 보조 형상 |
| --- | --- | --- |
| S / A | 약 6m 둔덕에 뿌리 계단 또는 물살을 탄 잎 + 고정으로 접근. 두 경로의 착지는 공통 둔덕 | `S_OverlookPaving`, `PH_A_Root01_Withered`, `PH_A_Leaf01`; 성장 계단과 공통 착지판. 전망 시작 yaw 225°로 G/W 사이를 향함 |
| B | 뿌리 다리 또는 풍차 FLOW로 돌 발판을 운반하고 FREEZE | `PH_B_RootBridge_Withered`, `PH_B_MovingStone`, `PH_B_Windmill_Rotor`; 뿌리 다리/건너편 착지판/G 상승 계단. 원래 외곽의 뿌리 모델을 실제 다리 입구로 옮김 |
| C | 잎 BLOOM으로 넓힌 뒤 FLOW로 위 선반 이동, 필요하면 FREEZE | `PH_C_LeafPlatform`; 확장 잎 지지판, 약 11.85m 선반 및 G 접속 계단 |
| B ↔ C | 두 입구 사이의 중간 보행로로 경로 변경 | 기존 `TEMP_Bridge_MoundB/C`를 A에서 각 입구로 연결하는 길로 유지. 추가 연결로는 B 건너편과 C 높은 선반에 닿지 않게 구성 |
| G | 작은 수차 FLOW 또는 문 아래 뿌리 BLOOM. 완전 개방 후 걸쇠 유지 | `PH_G_StoneDoor`, `PH_G_SmallWheel`, `PH_G_DoorRoot_Withered`, `PH_G_Latch`, `W_G_ToWheel`. 문과 충돌체가 같은 이동 부모 아래 있음 |
| G → D1 | 기존 탑의 왕복 계단 → 터널 → 발코니 → D1 계단 | `TWR_Flight1...6`, 탑 랜딩, `TUN_Floor`, `D1_Walkway/Balcony/Stairs`; 중간 안전 지점 |
| D3 | D1 발코니에서 덩굴 길, 또는 아래 수조 FREEZE 후 부두 계단. 작업대 도착 시 복귀 보행로 유지 | `PH_D3_VinePath_Withered`, `D3_Workbench`, `D3_DockStairs`; 덩굴 계단/얼음 지지판/복귀 계단 |
| D2 부두 잎 | D5 공급 후 D2 부두 방향을 선택하면 잎 운반 | `PH_D2_Leaf`. 초기 물은 추가하지 않았다. 잎은 복구 후 지름길이며 덩굴/얼음으로 D5 전 작업대 접근을 제공 |
| D5 | FLOW로 수로 정렬 후 구조물 FREEZE, 또는 BLOOM으로 우회 문 열기 | `PH_D5_MovingChannel`, `PH_D5_BypassDoor`, `PH_D5_BypassRoot_Withered`, 기존 고정/우회 수로와 물. 구조물 고정 표적과 내부 물 동결 표적을 분리 |
| D2 / D4 | 정비 부두/작은 수차/주 출구의 세 방향. 수차 또는 뿌리로 수문을 끝까지 열고 걸쇠 유지 | `PH_D2_BranchGate` 주변 조준 표적, `PH_D4_Gate/SmallWheel/GateRoot_Withered/Latch`, `W_D2_Dock/Wheel/Exit`. 기존 분기 모델을 배경으로 사용하며 물 표시·잎 동력·수문 동력은 선택에 따라 바뀜 |
| W / 엔딩 | D5 공급 + D4 개방 + 주 출구 선택 + 주 물길 해동이 3초 유지되면 복원. 물길→물레 4초 카메라 뒤 조작 복귀, D4 회랑으로 정원 보행 | `PH_W_Waterwheel`, `W_Exit_Tunnel/W_Gorge/W_R_WheelFeed`, 정원 물 모델; 안전 회랑·난간·정원 착지판·식생 초안 |

지형은 기존의 명시적 solid mesh 목록으로 MeshCollider를 만든다. 움직이는 문·잎·돌 발판은 FBX 월드 bounds에 맞춘 단위 스케일 부모로 옮기고 그 부모 아래에 충돌체를 둔다. 수로는 실제 피벗을 보존하는 부모와 MeshCollider를 사용한다. 물레/수차 회전은 bounds 중심에 피벗을 만들며 물레는 월드 X축으로 돈다. 런타임 Console의 `MODEL BINDINGS`에 임포트된 좌표·스케일을 출력한다. 이번 환경에서는 이 로그를 실제로 생성하지 못했다.

누수, 분기 목적지, 수문 걸쇠, 얼음 차단을 각 현장 표지와 수면/이동하는 물 표식으로 구분한다. 주 수조 얼음은 보행 지지판이자 최종 물 차단이다. 작업대에서 복귀 보행로를 확보한 뒤 얼음을 녹여 중앙과 수문으로 돌아올 수 있게 했다.

## 주요 추가 파일

- `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`와 `.meta`: 원본에서 독립 복제.
- `Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs`: 입력, 조준/LOS, 구체 비행·회수, 안전 지점, 회로 결과, 연출, HUD, 재시작.
- `EFV2PuzzleAssembly.cs`: 기존 모델 탐색, 충돌/피벗/레일 연결, 구간 보조 지형과 현장 표시.
- `EFV2PuzzleState.cs`: 구체 소유권과 D5 공급의 순수 상태 코드.
- `EFV2Checkpoint`, `EFV2ChannelPose`, `EFV2WaterHazard`, `EFV2IceVisual`, `EFV2Plant`, `EFV2Billboard`: 장치/보행/표시 보조.
- `Assets/EverflowMap/Editor/EFV2PuzzleBuild.cs`, `EFV2PuzzlePlayChecks.cs`, `EFV2PlayRecorder.cs`: 씬 참조 검사, 강제 상태 검사, 실제 Input System 이벤트 스모크 검사, 조작을 변경하지 않는 Play 기록기.
- `tools/setup_v2_puzzles.py`, `compile_v2_puzzles.py`, `verify_v2_puzzles.py`, `verify_v2_puzzle_geometry.py`, `tools/v2-puzzle-state-checks/`: 재현 가능한 오프라인 구성/검사.

`LabTarget`, `LabMover`, `LabGate`, `LabRootLift`, `LabStreamMarker`, `LabWorldState`, `LabGateState`, `LabArrivalState`, `LabCircuitRules`를 수정 없이 재사용했다. Lab의 구체 슬롯 관리와 Q 수동 회수는 가져오지 않았다. 기존 공용 코드와 사용자가 작업 중이던 코드에는 이번 작업의 수정을 넣지 않았다.

## 실행과 검증 결과

| 검증 종류 | 결과 / 범위 |
| --- | --- |
| 기존 에셋 보존 | 원본 v2 씬/meta, PuzzleLab 씬, FBX, 타이틀, Build Settings SHA-256 일치. 작업 시작 때의 tracked Git diff도 그대로 일치 |
| 씬 파일 정적 검사 | 독립 GUID, 새 에셋 GUID 13개 충돌 없음, 로컬 fileID 참조 모두 존재. 복제의 다른 플레이어·카메라·FBX YAML은 원본과 일치 |
| C# | Unity 6000.3.21f1 설치본의 Roslyn + 캐시된 프로젝트/패키지 참조로 전체 런타임, 에디터, 플레이어용 소스 3종 컴파일 통과. Unity 임포트/플레이어 빌드는 아님 |
| 순수 상태 코드 | 69개 검사 및 회로 조합 256개 통과. 즉시 재대상 지정·거리 회수·효과 유지·비행 재사용·리스폰 취소·동결/해동·걸쇠·3초 연속 공급/중단/재시작 |
| FBX 좌표 검사 | 요구 모델 이름 46개 존재. 생성 계단 9개 경로의 중앙선 높이 검사에서 수정 후 매몰 후보 0개. 기존 계단 8그룹의 단차 ≤0.3m. 실제 CharacterController 충돌 검사는 아님 |
| Unity 에디터 시작 | 6000.3.21f1 배치 실행을 시도했으나 `LicenseClient-vzd12` 채널 연결 거부 후 Licensing 초기화가 진행되지 않음. 이 작업에서 시작한 멈춘 에디터 프로세스만 종료 |
| 강제 상태 Runtime 검사 | **미실행**. 메뉴 도구만 제공. 강제 Apply/Tick/pose 변경을 사용하므로 통과해도 일반 입력 완주가 아님 |
| Input System Runtime 스모크 | **미실행**. 도구는 키보드/마우스 이벤트로 A 조준·발사·FLOW 방향·이동·점프·R을 검사. 전체 완주는 아님 |
| 일반 플레이어 입력 S→정원 완주 | **수행하지 못함. 실제 완주 경로 없음** |
| 첫 Play 오류 수정→재Play | **수행하지 못함**. 아래의 수정은 코드/기하 검사에서 찾은 문제를 고친 결과 |
| 실제 플레이 시간 | **측정 없음**. 15~20분은 기획 목표이며 이번 작업의 실측치가 아님 |

오프라인 재검증 명령: `python tools/verify_v2_puzzles.py`. 상태/참조 결과는 `offline-results.json`, 기하 검사는 `geometry-checks.json`, 개별 컴파일 로그와 `managed-state-checks.txt`에 있다.

## 검사 중 발견하고 고친 문제

1. C 유적 접속 계단이 유적 포장과 지형에 최대 약 0.65m 파묻힘: 고도를 먼저 확보하고 수평 접속판을 추가.
2. D3 복귀 계단이 작업대 바닥/기존 부두 계단 내부로 내려감: 기존 계단 위의 수평 출구를 거쳐 외측에서 하강하도록 이동.
3. D5 우회 접근 계단이 D5 ledge 내부에서 하강: ledge 끝 바깥으로 하강 시작점을 이동.
4. 복원 카메라 중 관리자 조기 return으로 물레 회전이 멈춤: 회로/물레 갱신을 연출 중에도 유지.
5. 주 수조 동결 뒤 하류 물 모델이 계속 보임: 해동 조건을 하류 표시에도 반영하고 현장 차단 문구 추가.
6. 일시정지 중 발판이 계속 움직여 재개 시 플레이어를 이탈시킬 수 있음: 이 씬의 일시정지는 시간 스케일도 정지하고 종료 시 복구.
7. 인접 안전 지점 트리거가 경계에서 서로 교대로 선택될 수 있음: 더 가까운 지점을 선택하는 여유 거리를 추가.
8. 재시작 후 Ready 구체가 예전 위치에서 천천히 돌아올 수 있음: 재시작 때 세 구체를 시작점에 재배치.
9. 퍼즐 관리자의 LateUpdate가 CinemachineBrain보다 먼저 조준할 수 있음: 실행 순서를 Brain 뒤로 옮겨 해당 프레임 카메라로 조준.

수정 뒤 전체 C# 3종, 상태 검사, 기하 검사, 보존/씬 참조 검사를 다시 통과했다. 실제 Play 재검증을 대신하지 않는다.

## 임시 값과 남은 검증

- 회수 24m, 조준 사거리 18m, 구체 속도 32m/s, 연속 입력 간격 0.18초.
- 시작 `(-46, 1.2, -6)`, yaw 225°. G와 W가 함께 잘 읽히는지 실제 Cinemachine 구도로 확인 필요.
- A 공통 착지 상면 약 6.05m, C 높은 착지 11.85m, G 약 15.1m. 보조 계단 단차 ≤0.28m, 폭 2.2~2.7m를 초안으로 사용.
- D5 피벗 이동 `(-0.5, 0, -0.5)m`, 자세의 월드 회전 identity를 정렬 자세로 사용. 실제 Unity 임포트 피벗과 맞는지 반드시 확인. 위치 허용 0.4m, 회전 허용 3°, 구조물 고정 필요.
- 물레 복원 3초, 복원 카메라 4초, G 문 상승 8m, D4 수문 상승 6.5m.
- D3 잎은 복구 후 지름길로 채택했다. 초기 부두 물은 넣지 않았다. 작업대 도착 후 안전 복귀 보행로가 남는다.
- A→C 바람 지름길은 구현하지 않았다. A 두 기본 해법의 실제 Play 검증을 먼저 완료해야 한다.
- 기존 TEMP 다리와 추가 연결로가 의도치 않은 우회를 만드는지, B/C 자연 지형으로 퍼즐을 건너뛸 수 있는지는 **미검증**.
- A 두 해법, B만/C만 G 도달, 중간 갈아타기, G 두 해법, D3 덩굴/얼음/복구 후 잎, D5 정렬/우회, D4 수차/뿌리, 구체 두 자동 회수, 비행 중 재사용, 낙사/R/국소 복구, 얼음 위에서 안전한 해동, 문 통과, 발판 접지, 가림/카메라, 최종 회랑, 엔딩 후 탐험/재시작은 실제 Play가 남아 있다.

Unity 실행이 가능한 환경에서는 먼저 새 씬 단독 Play로 A부터 확인한다. `Validate Saved Scene References`, `Run Forced-State Runtime Checks`, `Run Synthetic Input Smoke Checks` 메뉴를 보조로 사용하되 각 검사 종류를 분리해 기록한다. 일반 Play에서는 관찰 기록기가 `observed-play-*.jsonl`에 이벤트와 위치/시간을 저장한다. 기록기 자체는 입력이나 장치 상태를 변경하지 않으며, 로그만으로 실제 사람이 조작했음을 증명하지는 않는다.
