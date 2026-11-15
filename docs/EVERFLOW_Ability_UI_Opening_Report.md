# EVERFLOW 능력 이미지 UI와 시작 카메라

## Unity에서 바로 확인

`Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`를 열고 **Play → Game View**에서 확인한다.

1. 시작 시 지상 경로·유적·물레방아를 보는 원경이 **3초** 유지된다. 약 **1.25초** 동안 캐릭터의 기존 플레이 시점으로 접근한 뒤 이동·시점·능력 조작을 돌려준다. 원경 중 Esc로 멈추면 연출 시간도 멈춘다.
2. 시작 전망대에서 화면 하단 중앙의 **1 BLOOM / 2 FREEZE / 3 FLOW** 이미지와 선택 표시를 확인한다. 키를 바꾸면 선택한 이미지가 조금 커지고 밑줄·구체 상태·능력 설명이 함께 갱신된다.
3. 기존 **왼쪽 위 목표와 다음 행동**, **왼쪽 아래 이동·복귀 안내**, **F1 도움말**, **지하 오른쪽 위 물길 상태판**은 유지한다.
4. A 연못의 물살에 3번으로 조준·클릭하면 오른쪽에 FLOW 방향 선택창이 열린다. E / Tab / 휠과 재클릭으로 확정한다. 일반 조준 대상 설명은 능력 UI 위에 표시한다.

## 참고한 원본과 적용 방식

실제 `Assets/Scenes/Everflow_Map.unity`의 `EFMapAbilityHUD`에 연결된 Sprite 세 개와 `EFMapWorld.openingCamera`를 읽었다. 원본은 FOV 52의 원경을 3초 보여준 뒤 플레이어 카메라로 돌아온다. 하단 중앙에 세 능력 이미지, 선택한 능력의 설명과 밑줄을 표시하는 구성을 재사용했다.

- 이미지: `Assets/icon/bloom_icon.png`, `freeze_icon.png`, `pulse_icon.png`. 기존 Sprite/GUID를 직접 참조하며 이미지나 임포터를 수정하지 않았다. 투명 여백은 전용 UV 설정으로 보정한다.
- 세 번째 **PULSE 이미지의 그림만 재사용**하고 이름·설명·동작은 현재 v2의 **FLOW**를 유지했다. 단계별 능력 잠금이나 옛 PULSE 기능을 가져오지 않았다.
- 새 맵의 지형·시작점에 맞춰 원경을 월드 `(-16, 30, 20)`, 시선 목표를 `(-49, -2, -35)`로 잡았다. 원본과 맵 배치가 달라 원본 카메라의 절대 좌표는 복사하지 않았다.
- 원본의 Camera/EFMapCamera를 복사하지 않고 시작 전용 **CinemachineCamera**의 우선순위로 현재 Brain을 사용한다. 접근 마지막에는 기존 플레이 카메라의 실제 상태에 맞춘다. Main Camera의 Transform을 별도 스크립트가 동시에 제어하지 않는다.
- 연출 동안 World의 기존 입력 게이트를 잠근다. 시작 중 입력은 저장해서 나중에 발사하지 않는다. Enter 엔딩 재시작과 R 국소 복귀는 기존처럼 즉시 처리하며 원경을 반복 재생하지 않는다. Play를 다시 시작하면 원경부터 재생한다.

## 겹침 정리와 수정

능력 이미지는 하단 중앙, 이동 안내와 이벤트 알림은 왼쪽 아래로 분리했다. 기존 조준 대상 설명을 능력 패널 위로 올렸고, 더 긴 FLOW 선택창은 오른쪽으로 이동해 중앙 조준점과 능력 이미지를 피했다. F1을 열면 같은 자리에 뜨는 조준 설명을 잠시 숨기며 게임 상태는 유지한다.

검토 중 FLOW 방향을 고르는 상태에서 조준을 벗어나면 목표 방향 표식과 선택창이 겹칠 수 있는 경우를 발견했다. 방향 선택 중에는 목표 표식 그리기를 보류하도록 수정했다. 목표 조건이나 FLOW 선택 상태는 바꾸지 않는다. 목표 표식의 아래쪽 제한도 새로운 능력 패널 높이에 맞춰 올렸다.

초기 컴파일에서 현재 Cinemachine 3의 CameraState 접근자가 이전 형태와 다른 오류를 확인했다. 설치된 패키지 소스의 `GetFinalPosition()` / `GetFinalOrientation()`으로 수정한 뒤 런타임·Editor·플레이어 구성 컴파일과 실제 Unity 임포트를 다시 확인했다.

## 변경 파일과 유지보수

| 파일 | 역할 |
| --- | --- |
| `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity` | 기존 World에 표현 컴포넌트, Sprite 참조와 시작 구도 설정 저장 |
| `Assets/EverflowMap/Runtime/EFV2ScenePresentation.cs` | 세 이미지/UV 설정, 원경 유지·접근·Cinemachine 전환 |
| `Assets/EverflowMap/Runtime/EFV2Guidance.cs` | 이미지·선택 능력 설명, 기존 안내 UI 위치 조정, 시작 자막 |
| `Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs` | 시작/재시작 시 표현 호출과 시작 연출 입력 게이트 |
| `Assets/EverflowMap/Editor/EFV2PresentationTools.cs` | 원본을 읽고 복제 씬에만 설정을 저장하는 메뉴 |
| `Assets/EverflowMap/Editor/EFV2PresentationChecks.cs` | 실제 시작 연출·입력·UI 캡처 검사 |
| `EFV2PuzzlePlayChecks.cs`, `EFV2GuidanceChecks.cs`, `EFV2WaterPlayChecks.cs` | 기존 검사가 실제 시작 연출 종료를 기다리도록 조정 |
| `EFV2EditorBridge.cs`, `tools/request_v2_editor.py` | 고정된 `presentation-setup` / `presentation-check` 명령 추가 |
| `tools/verify_v2_presentation.py` | 작업 시작 해시·기존 Git diff·게임 메서드·GUID 보존 검사 |

Hierarchy의 **EVERFLOW V2.3 Puzzle World → EFV2ScenePresentation**에서 원경 위치·시선·시간과 이미지 참조를 확인한다. 능력 설명 문구는 `EFV2Guidance.AbilityDescription`, 배치는 `DrawAbilities` / `Draw`에서 조정한다. 이 구성은 기존 안내와 같은 화면 비율 계산을 사용한다.

설정을 다시 연결하려면 Play를 멈추고 `Tools → EVERFLOW → V2 Puzzles → Configure Ability Images And Opening`을 실행한다. 원본 씬은 읽기만 하며, 저장되지 않은 씬 변경이 있으면 덮어쓰지 않고 중단한다. 셋업 메뉴는 기존 세 이미지로 참조를 다시 맞추므로 임의로 바꾼 이미지 설정을 유지하려면 메뉴를 다시 실행하지 않는다.

## 검증 근거

실제 Unity **6000.3.21f1**, URP **17.3.0**, 기존 PC Renderer / D3D12의 **1920×1080 Game View**를 사용했다. 캡처는 게임이 그린 화면이다. 작은 해상도·다른 종횡비의 실제 Game View와 사람의 초행 조작성은 이번 검사에 포함하지 않았다.

검사 결과와 제한은 아래 보고서에 남긴다. 자동 입력 검사는 Input System의 합성 키보드·마우스를 사용하며, 사람이 직접 조작한 플레이로 기록하지 않는다. 상태를 직접 설정하는 안내 검사는 연속 보행 검사와 구분한다.

| 검사 | 최종 결과 |
| --- | --- |
| 시작 연출·능력 UI | 28개 통과, 0 실패. 추가 입력 누적 검사까지 포함해 재실행 |
| 기존 안내·FLOW·지하·엔딩 표시 | 24개 통과, 0 실패. 겹침 수정 후 재실행 |
| 뿌리 연속 경로 | 18개 통과, 0 실패. 정원 도착까지 142.61초 |
| 잎·수차·얼음 연속 경로 | 28개 통과, 0 실패. 정원 도착까지 142.91초 |
| 기존 물 통합 Play 검사 | 23개 통과, 0 실패. 공급 없음/시작/분기/동결·해동/재시작과 셰이더 확인 |
| C# | 실제 Unity 임포트/Play 오류 없음. 설치된 Roslyn으로 런타임·Editor·플레이어 구성 컴파일도 통과 |

연속 경로 시간은 World 시작 기준 unscaled 시간으로 원경 연출과 합성 입력으로 미리 정한 경로를 따라간 시간을 포함한다. 사람의 초행 탐색 시간이나 기획의 15~20분 실측값이 아니다. 뿌리 경로는 첫 UI 확인 뒤 실행했고, 마지막 목표 표식 겹침 수정 뒤 대체 경로와 안내 검사를 다시 실행했다.

- 시작/UI 검사: [runtime-presentation.txt](ability-intro-validation/runtime-presentation.txt). 실제 카메라, 원경 중 입력 차단, Esc 일시정지/재개, 전환 뒤 입력 지연·회전 튐, 1/2/3, F1, 이동·복귀를 검사한다. 카메라나 장치 상태를 강제로 바꾸지 않는다.
- 기존 안내 검사: [runtime-guidance.txt](ability-intro-validation/runtime-guidance.txt). 능력 키·FLOW 선택은 실제 합성 입력이고, 후반 지하/엔딩 사례는 검사용 카메라·위치·장치 상태 설정을 포함한다.
- 뿌리 연속 경로: [runtime-route-bloom.txt](ability-intro-validation/runtime-route-bloom.txt). S → A 뿌리 → B → G 뿌리 → D1 → D3 덩굴 → D5 우회 → D4 뿌리 → D2 주 출구 → W → 정원 → Enter. 강제 위치/장치 변경으로 통과시키지 않는다.
- 대체 연속 경로: [runtime-route-alternate.txt](ability-intro-validation/runtime-route-alternate.txt). A 잎 → C 성장 잎 → G 수차 → D3 얼음 → D5 정렬/고정 → D4 수차 → 주 물길 동결/해동 → W → 정원 → Enter. 같은 실제 입력·물리 검사다.
- 기존 물 통합 검사: [runtime-water-integrated.txt](ability-intro-validation/runtime-water-integrated.txt). 실제 URP Play에서 검사용 카메라와 명시적인 퍼즐 상태 설정으로 표현·공급 연동을 검사한다. 보행 완주로 계산하지 않는다.
- 정적 보존: [preservation-results.json](ability-intro-validation/preservation-results.json). 원본 씬·FBX·아이콘/임포터·물·충돌/배치·기존 사용자 변경과 패키지/파이프라인/빌드 설정을 비교한다. World의 능력·구체·공급·복귀·엔딩 등 16개 메서드는 동일하고, 시작/Update/일시정지/재시작은 시작 연출을 연결하는 변경으로 제한한다. 정적 검사는 Play 결과가 아니다.

## Game View 캡처

| 장면 | 캡처 |
| --- | --- |
| 이전 능력 UI | [이전 안내 작업에서 보관한 시작 화면](ability-intro-validation/before-ability-hud.png) |
| 원경 / 캐릭터로 접근 | [원경](ability-intro-validation/01-opening-overview.png) / [접근](ability-intro-validation/03-opening-approach.png) |
| 1 / 2 / 3 선택 | [BLOOM](ability-intro-validation/04-ability-1.png) / [FREEZE](ability-intro-validation/04-ability-2.png) / [FLOW](ability-intro-validation/04-ability-3.png) |
| F1 도움말과 이미지 | [도움말](ability-intro-validation/05-help.png) |
| FLOW 선택창과 능력 이미지 | [방향 선택](ability-intro-validation/flow-choice.png) |
| 기존 지하 목표·물길 상태판 | [D5 안내](ability-intro-validation/d5-leak.png) |
| 연속 입력 경로의 기존 Cinemachine 화면 | [지하 D1](ability-intro-validation/input-route-bloom-D1_BALCONY.png) / [D5](ability-intro-validation/input-route-bloom-D5_SERVICE_LEDGE.png) / [정원 도착](ability-intro-validation/input-route-bloom-W_RESTORED_GARDEN.png) |

이전 화면은 이전 작업의 보관본이며 같은 시점·각도의 엄밀한 전후 촬영은 아니다. 시작/UI 검사의 카메라는 실제 Cinemachine이고, 지하 안내 전용 캡처에는 명시적인 검사용 배치가 포함된다. 실제 입력 경로 캡처와 구분한다.

검사 후 Play를 종료하고 대상 씬을 열어 둔다. 테스트를 위해 켠 백그라운드 실행 설정은 작업 시작 값으로 복구한다. 현재 PC의 1920×1080 외 해상도, 실제 초행 사용자의 가독성 평가는 남아 있다.
