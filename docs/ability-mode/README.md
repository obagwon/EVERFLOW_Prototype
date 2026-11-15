# EVERFLOW 카메라·능력 선택 모드 변경

대상: `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`.
Unity 6000.3.21f1 / Unity CLI 1.0.0-beta.17에서 구현 및 Play 검증.

## 조작법

- 탐색: WASD 이동, Space 점프, 마우스 시점 회전. 커서는 잠김·숨김. 좌클릭은 능력을 발사하지 않음.
- 1 BLOOM / 2 FREEZE / 3 FLOW: 해당 능력을 선택하면서 세계 정지. 커서로 대상 선택. 정지 중에도 1/2/3으로 전환 가능.
- BLOOM/FREEZE: 유효한 대상 좌클릭 → 탐색 복귀 및 구체 발사. FREEZE 재사용은 해동/고정 해제. 기존 X도 선택 모드에서 유지.
- FLOW: 대상 클릭 → 오른쪽 방향 버튼 선택 → 사용 확정 버튼. E/Tab/휠로 변경하고 같은 대상을 재클릭해도 확정 가능.
- 능력 모드 Esc / 취소 버튼: 효과 적용 없이 탐색 복귀. 탐색 Esc: 기존 일시정지.
- R 안전 지점 복귀, 정원 복원 후 Enter 재시작 유지.

## 구현

`EFV2PuzzleWorld`가 Exploration / Ability / Paused / Cinematic 상태와 커서·플레이어·카메라 입력을 관리한다. `Paused`는 일반 메뉴에만 사용하고, `SimulationPaused`가 일반 정지와 능력 선택을 함께 표현한다.

능력 선택 중 Time.timeScale=0, 플레이어와 Cinemachine 입력 중단, 기존 Brain 잠시 정지. 같은 카메라 상태를 보존해 복귀하고 커서 재잠금 직후 마우스 워프 델타를 한 프레임 차단한다. 별도 추적 카메라를 추가하지 않았다.

커서 위치 Raycast는 기존 대상 프록시·충돌 규칙을 사용한다. 유효한 대상만 선택 브래킷 표시. 능력 UI 영역 및 EventSystem UI는 월드 클릭을 차단한다. 사거리 18m와 플레이어→대상 장애물 검사를 유지한다. 선택·방향 미리보기는 LabTarget 상태를 변경하지 않으며, 확정 시 기존 구체 슬롯의 Launch→이동→Arrive→Apply/Undo 경로를 사용한다.

물 네트워크의 기존 로컬 시간은 그대로 사용하고 정지 조건을 확장했다. 현재 재생 중이던 입자만 보관 후 재개하며, AudioListener 정지와 예외 오디오 보관으로 음향을 멈춘다. 피드백 컴포넌트의 별도 Pause/Play 중복 제어를 제거해 종료된 효과가 모드 종료 때 다시 재생되지 않게 했다. 엔딩 카메라의 시간도 게임 시간에 맞춰 일시정지를 따른다.

## 변경 파일

| 파일 | 변경 |
|---|---|
| `Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs` | 상태, 세계 정지·재개, 커서 Raycast, 확정·취소 및 초기화 |
| `Assets/EverflowMap/Runtime/EFV2Guidance.cs` | 능력 선택 안내, 유효 대상 피드백, FLOW 버튼, UI 클릭 차단, 녹화용 커서 표식 |
| `Assets/EverflowMap/Runtime/EFMapCinemachineRig.cs` | 기존 Brain 정지·복귀 연결 |
| `Assets/EverflowMap/Runtime/EFDemoFeedback.cs` | 통합 정지 상태 사용, 중복 재생 제거 |
| `Assets/EverflowMap/Runtime/EFWaterImpact.cs` | 물보라 정지 상태 연결 |
| `Assets/EverflowMap/Runtime/EFV2WaterIntegration.cs` | 기존 물 로컬 시간의 능력 모드 정지 |
| `Assets/EverflowMap/Editor/EFV2PuzzlePlayChecks.cs` | 실제 숫자 키·커서 선택·확정을 거치는 자동 검사, 카메라 측정, 녹화 시퀀스 |
| `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity` | 기존 Cinemachine ScreenPosition (0,.02), 회전 Composer Damping (0,0), CameraTarget 높이 .15 |

씬은 Unity API로 저장했다. `scene-preservation.json`에서 이 세 가지 구도 값 외의 씬 내용이 작업 직전과 동일함을 확인했다. 충돌, 퍼즐 조건, 최근 물길 메시·머티리얼, Deoccluder, 오프닝·엔딩 설정 보존. 유료 에셋이나 패키지 추가 없음.

## 검증과 한계

검사는 실제 Unity Play에서 Input System 키보드·마우스 이벤트를 주입한다. 연속 경로는 Teleport, Apply, 강제 문 개방·장치 위치 변경, 선택·확정 우회를 사용하지 않는다. 사거리·벽 검사의 임시 벽과 리스폰·재시작 수명 주기 호출은 별도 입력 점검에만 사용한다.

자동 입력에 의한 경로 검증이며 사람이 처음 플레이하는 난이도·체감 평가는 별도다. 운영체제 수준 Alt+Tab과 다양한 화면 비율의 수동 점검은 별도다.


| Unity Play 검사 | 결과 | 증거 |
|---|---|---|
| A 잎 → C 성장 잎 → G 수차 → D3 얼음 → D5 정렬·고정 → D4 수차 → 해동 → 정원 → Enter 재시작 | 61개, 실패 0 | runtime-route-alternate.txt |
| A/B 뿌리 → G 석문 → D3 덩굴 → D5 우회 → D4 뿌리 → D2 주 출구 → 정원 → Enter 재시작 | 46개, 실패 0 | runtime-route-bloom.txt |
| 능력 선택·취소·사용, 잘못된 능력·빈 공간·사거리·벽·UI 차단, FLOW E/Tab/휠/마우스, 반복 입력, 리스폰·재시작, 포커스 콜백 | 50개, 실패 0 | runtime-synthetic-input-smoke.txt |
| 녹화 시퀀스: 탐색 → BLOOM 선택/확정 → 이동 → FLOW 마우스 방향 선택/확정 → 탐색 | 9개, 실패 0 | runtime-tactical-review.txt |

BLOOM 연속 경로에서 Cinemachine 업데이트 이후 31,379프레임(이동 19,395프레임)을 측정. 몸통 기준 가로 중심 오차 최대 0.00008, 세로 목표 0.48에 대한 오차 최대 0.00015 (정규화 화면 좌표). 실제로 열리는 중인 G 석문과 진행 중인 물레방아 복원 타이머의 정지도 별도 검사했다. 각 능력 사용 전 이동체·문·물 네트워크 시간·퍼즐 타이머 정지를 검사했다.

포커스 검사는 Unity의 OnApplicationFocus(false/true)를 주입하고 실제 Esc로 복귀했다. 이 Editor에서 Game/Scene 탭 전환은 OS 포커스 변경을 발생시키지 않아 물리적인 Alt+Tab 시험을 대신하지 않는다. 기존 장치의 충돌·공급·동결 조건과 구체 자동 회수는 연속 경로에서 유지됐으며, 강제 상태 검사를 연속 완주로 계산하지 않았다.

영상: `interaction-review.mp4` — 12.53초, 1280×720, 30fps, 음향 포함. 실제 최종 MP4를 Unity VideoPlayer로 디코딩해 3초 BLOOM 선택, 8초 FLOW 선택, 11초 탐색 복귀 프레임을 확인했다. 선택 화면 PNG도 함께 보관했다. `video-metadata.json`은 실제 MP4의 영상·음향 트랙과 길이 확인 결과다.

남은 수동 확인: 운영체제 Alt+Tab, 여러 화면 비율, 사람이 직접 조준·취소할 때의 체감. 확인된 게임 로직 실패는 현재 없다. 테스트 후 Play 종료, 씬 저장 상태·Time.timeScale=1·오디오 정지 해제·Cinemachine Brain 활성 상태를 확인했다.
