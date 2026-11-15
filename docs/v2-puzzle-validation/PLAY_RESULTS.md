# EVERFLOW v2.3 실제 Unity Play 결과

**새 씬을 Unity에서 열고 Play했으며, 두 경로 모두 합성 키보드·마우스 입력으로 시작부터 복원된 정원까지 완주하고 Enter 재시작을 통과했다.** 사람이 직접 조작한 플레이테스트와 구분한다.

## 실행 조건과 시간

Unity 6000.3.21f1, 정상 Windows 사용자 에디터 PID 31112에서 `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`를 단독 Play했다. Input System의 테스트 키보드·마우스 이벤트를 실제 게임 Update/LateUpdate에 전달했다. Cinemachine 시점과 레이캐스트, 기존 CharacterController, 구체 비행·도착·장치 코드가 실행됐다.

두 연속 경로의 검사 코드는 초기 순간이동, `LabTarget.Apply`, `Gate.Tick` 또는 장치 pose 설정을 호출하지 않는다. 게임 자체의 R·낙하 복귀·Enter 재시작은 일반 입력으로 실행했다. 분리 검사에는 강제 초기 설정이 있으므로 별도로 표시했다.

| 실행 | 결과 | 정원까지 시간 | 근거 |
| --- | --- | --- | --- |
| 뿌리 중심 연속 경로 | 18개 통과, 재시작 포함 | 136.33초 | [runtime-route-bloom.txt](runtime-route-bloom.txt) |
| 잎·바람·얼음·수차 연속 경로 | 28개 통과, 재시작 포함 | 138.55초 | [runtime-route-alternate.txt](runtime-route-alternate.txt) |
| 강제 상태 회로 검사 | 18개 통과 | 측정 대상 아님 | [runtime-forced-state.txt](runtime-forced-state.txt) |
| 실제 입력 이벤트 스모크 | 15개 통과 | 측정 대상 아님 | [runtime-synthetic-input-smoke.txt](runtime-synthetic-input-smoke.txt) |
| 출구 분리 물리 검사 | 4개 통과, 강제 초기 설정 있음 | 측정 대상 아님 | [runtime-exit-isolated.txt](runtime-exit-isolated.txt) |
| 발판·연결길·낙하 분리 검사 | 15개 통과, 강제 초기 설정 있음 | 측정 대상 아님 | [runtime-platforms-isolated.txt](runtime-platforms-isolated.txt) |

시간은 World.Start부터 정원 도착까지의 Unity unscaled 경과 시간이다. 미리 정한 경로와 목표 좌표를 따라 결과를 확인하며 입력하는 자동화이므로 사람의 첫 탐색·퍼즐 이해·힌트·재시도 시간을 나타내지 않는다. **15~20분은 여전히 기획 목표이며 사람 실측값은 없다.**

## 실제로 완주한 경로

1. S → A 뿌리 계단 → B 뿌리 다리 → G 뿌리 개방 → 탑 계단·터널·D1 → D3 덩굴 → D5 우회 뿌리 → D2·D4 뿌리 개방 → 중앙 분기대로 복귀 → 주 출구 → W 복원·연출·조작 복귀 → 열린 D4 문·회랑 → 정원 → Enter 재시작.
2. S → A 잎 동승·FLOW·FREEZE → C 잎 성장·바람·고정·고지 착지 → G 수차 → D1 계단 → D3 얼음·부두 계단 → D5 FLOW 정렬·FREEZE 구조물 고정 → D2 작은 수차로 D4 개방 → 주 출구 → 얼음 차단 확인·해동 → W 복원 → 문·회랑·정원 → Enter 재시작.

## 확인한 조건

- A 두 해법이 동일한 공통 안전 지점에 도착했다. B만 거쳐도, C만 거쳐도 G에 도착했다.
- 잎·돌 발판 위 실제 동승과 고정·착지를 확인했다. B 풍차 해법과 공급 복구 후 D3 부두 잎은 강제 초기 위치/공급을 둔 분리 물리 검사에서 확인했다.
- B↔C 중간 연결길을 양방향으로 보행했다. 모든 자연 지형에서 의도치 않은 우회가 없는지까지 탐색한 결과는 아니다.
- G 뿌리/수차, D5 우회/정렬 고정, D4 뿌리/수차를 각각 입력으로 작동시켰다. 문과 충돌체가 함께 이동하고 실제 문을 통과했다.
- 주 출구 전환 후에도 D4 걸쇠를 유지했다. 얼음은 W 공급을 막았고 FREEZE 재사용으로 해동한 뒤 공급이 재개됐다. 이때 D5 구조물 고정은 유지됐다.
- FREEZE/FLOW 재대상 지정 후 A 상태 유지, 거리 회수 후 C 상태 유지, R 뒤 다른 구역 상태 유지를 확인했다.
- C 선반에서 실제로 걸어 떨어진 뒤 가까운 안전 지점으로 자동 복귀했고, 별도 구역의 성장·고정은 유지됐다. 이 검사의 시작 위치는 강제로 설정했다.
- 유효하지 않은 BLOOM 입력이 구체를 소비하지 않고 고정된 돌의 상태를 유지했다.
- W의 3초 연속 공급, 물레/정원 변화, 연출 종료 후 조작, 실제 출구 보행, 엔딩 후 재시작을 확인했다.

## 실제 Play에서 발견하고 수정한 문제

1. 배경 에디터가 첫 프레임에서 멈춤: 테스트 세션에서 Run In Background와 Player Loop 갱신 적용. 정상 사용자 에디터 내부에서 실행하며 라이선스·Windows 보안 설정을 변경하지 않았다.
2. B FLOW 표적이 뿌리 표적을 가림: 표적을 서로 다른 위치에 배치했다.
3. 큰 월드 문구가 겹침: 크기를 줄이고 대상 이름은 조준 중 표시한다. HUD에 현재 목적지와 실패 원인을 유지한다.
4. A 계단/둔덕 및 B 계단/G 접속 턱: 폭과 높이를 조정하고 같은 계단 그룹에 연속 지지면을 추가했다. 해결 전에는 계단과 함께 비활성 상태다.
5. 움직이는 모델 자체를 조준하기 어려움: 모델 충돌체를 대상에 연결하는 Proxy를 추가했다. 고정 표적을 발밑 중앙에서 옆으로 옮기고 실제 모델 색도 변경한다.
6. D5 수로 옆 복귀 동선: 서비스 보행판을 추가했다. 검사 이동도 마우스로 방향을 맞춘 뒤 W로 전진하도록 보완했다.
7. D4 보행로가 D1 계단 기초를 관통: 계단 하단 북쪽을 돌아가는 건조 보행로로 옮겼다. 분기대는 계단 뒤에서 원격 조준하지 않고 중앙으로 돌아와 조작한다.
8. 정원 착지판과 절벽 충돌: 실제 계곡 바닥을 따라 물레 동쪽 통로와 북쪽 정원으로 이동했다. 끝 난간을 열고 장식 식생 충돌을 제거했다. 식생은 기존 잎 메시를 재사용한다.
9. 정상 D1 계단 하강에 낙하 복귀가 먼저 발생: 계단 중간 안전 지점을 추가했다.
10. 카메라 감쇠가 남은 검사 클릭 누락: 안정된 조준을 확인하고 실제 적용 결과가 없으면 정상 입력으로 재시도한다. 강제 상태로 통과시키지 않는다.
11. 복원 연출 중 물레 정지, 얼음 뒤 하류 표시, 일시정지 중 발판 진행, 재시작 구체 위치, 카메라보다 이른 조준 순서도 수정했다.
12. 일반 Play 시작 시 비활성 창의 마우스가 시점을 변경: 새 씬에만 입력 포커스 게이트를 추가해 기존 Cinemachine 입력 처리 전에 중앙 관리자를 일시정지한다. 카메라 위치를 별도로 제어하지 않으며, 수정 뒤 입력 스모크 15개를 재통과했다.

수정 후 두 연속 경로의 Play 검사를 통과했다. 실패 재현 일부는 `attempts/`, 최신 결과는 `runtime-*.txt`, 추가 실행 이력은 `runs/`에 있다.

## 임시 값과 남은 항목

- 자동 회수 24m, 조준 18m, 구체 속도 32m/s, 입력 간격 0.18초.
- 시작 `(-46,1.2,-6)`, yaw 225°. A 상면 약 6.05m, C 상면 11.85m, G 약 15.1m. A 계단 폭 3.2m, B 폭 2.8m, 생성 계단 단차 ≤0.28m.
- D5 피벗 이동 `(-0.5,0,-0.5)m`, 정렬 자세 identity, 위치 허용 0.4m/회전 허용 3°. 실제 입력으로 정렬·고정·공급을 통과했다.
- W 연속 공급 3초, 복원 카메라 4초, G 상승 8m, D4 상승 6.5m.
- 정원 안전 지점 `(-84,-17.28,0)`. 보조 계단·다리·정원은 프로토타입 표현이다.
- D3 잎은 공급 복구 후 지름길이다. 초기 부두 물은 추가하지 않았고 공급 전에는 덩굴/얼음으로 접근한다.
- A→C 추가 바람 지름길은 미구현이다.
- 사람의 조작 감도·초심자 이해·장시간 탐험·과도한 연타 스트레스·모든 비정상 경로의 우회 가능성은 남은 플레이테스트 범위다.
- 원본 씬·PuzzleLab·팀원 FBX·타이틀·Build Settings 및 기존 사용자 변경분은 보존한다. 새 씬의 타이틀/빌드 배포 연결은 추가하지 않았다.

## 주요 파일과 재실행

- 새 씬: `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`. GUID `815ede8c18e65ad5be5be5781bb09ea9`.
- 런타임: `EFV2PuzzleWorld`, `EFV2PuzzleAssembly`, `EFV2PuzzleState`, `EFV2TargetProxy`, `EFV2Checkpoint`, `EFV2ChannelPose`, `EFV2CameraInputGate`와 시각/물 보조 코드.
- 에디터: `EFV2PuzzleBuild`, `EFV2PuzzlePlayChecks`, `EFV2PlayRecorder`, `EFV2EditorBridge`.
- PuzzleLab의 Target/Mover/Gate/RootLift/StreamMarker와 상태·회로 규칙은 수정 없이 재사용했다. Lab의 구체 슬롯과 Q 수동 회수는 사용하지 않는다.
- `python tools/verify_v2_puzzles.py`: 오프라인 보존·씬·C#·상태·기하 검사.
- `python tools/request_v2_editor.py route` / `alternate`: Unity 연속 입력 검사.
- `forced`, `input`, `exitcheck`, `platforms`: 각각 다른 범위의 분리 검사.
- 일반 Play에서는 관찰 기록기가 `observed-play-*.jsonl`을 쓴다. 기록기는 입력·상태를 바꾸지 않으며 로그만으로 사람이 조작했음을 보증하지 않는다.

## 실제 화면

- [뿌리 경로 정원](route-bloom-garden.png)
- [대안 경로 정원](route-alternate-garden.png)
- [복원 연출](route-bloom-restored.png)
- [실제 임포트 모델 bounds/스케일](unity-model-bindings.txt)
- [Play 연결 방법](PLAY_SETUP.md)
