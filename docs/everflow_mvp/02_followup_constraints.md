**현재 구현 완료 보고가 아닌 후속 조사·설계 제약 문서다. 아래 P5 확장, P6 마지막 PULSE, 복원·스킵 API는 아직 구현하지 않았다.**

# P3 이후 통합 제약과 최소 인터페이스 제안

작성일: 2026-10-17. 담당: 구조 조사 하위 에이전트. 코드와 저장된 씬을 읽어 조사했으며 이 문서 작성 외의 파일 수정, Unity 실행, 씬 저장, 빌드 실행을 하지 않았다. 줄 번호는 조사 시점 기준이다.

## 1. 실제 역할과 진행 ID

기획에서 부르는 P 번호와 현재 게임 UI의 P 번호를 구분한다. 번호만으로 구현 대상을 결정하지 않는다.

| 기획 역할 | 현재 UI | 실제 stage index | 현재 대상 |
|---|---|---:|---|
| P1 꽃 | P1 | 0 | `SM_FlowerJumpPad` |
| P2 덩굴 | P1의 두 번째 이동 | 0 | `SM_Vine` |
| 보고된 6역할에 별도 대응 없음 | P2 | 1 | `SM_FallingDebris_01..03`, `SM_Slate` |
| P5 회전 연결기 | P3 | 2 | `SM_PowerSource_P3`, `SM_RotatingBridge` |
| P3 수로 + P4 잎 이동 | P4 | 3 | `SM_PowerSource_P4`, `SM_WaterGate_P4`, `SM_IceSlide`, rideVisual인 `SM_RideLeaf` |
| 회전 연결기와 다른 기존 구간 | P5 | 4 | `SM_VineRoot_P5`, `SM_Door_P5` |
| P6 종합·물레방아 | P6 | 5 | `SM_PowerSource_P6`, `SM_WaterGate_P6`, `P6_ChannelRepair`, `SM_Channel_Swing_P6`, `SM_Waterwheel` |

근거: `Assets/EverflowMap/Editor/EFMapSetup.cs:58–130`, `Assets/EverflowMap/Runtime/EFCanalExperiment.cs:11`, 저장된 실험 씬 `Assets/Scenes/Everflow_Map_P3_DesignTest_20261016.unity:13341–13430`. `EFCanalExperiment.Stage`는 3이다. 실험 씬은 기존 stage 순서를 유지하며 UI P4의 수로 배치를 확장한다.

## 2. 회전 연결기의 구동·동결·해제

현재 회전 연결기는 UI P3/index 2다. `EFMapSetup.cs:84–87`은 전원 Source와 Bridge를 연결하고, Bridge의 y축 회전, initialAngle 55도, speed 27도/초를 설정한다. 기본 정렬 허용치는 `EFMapNode.cs:29`의 8도다. 이 수치는 설정값이며 성공률이나 플레이시간 실측이 아니다.

실제 입력 경로는 다음과 같다.

1. `EFMapPlayer.LateUpdate()` → `Aim()`이 현재 카메라 중앙 ray로 타깃을 찾는다.
2. `EFMapPlayer.CastAt()` → `CanCastAt()`이 세계 입력 활성, 같은 world, 능력 조건, 10m 거리와 고체 가림을 확인한다(`EFMapPlayer.cs:109–128`).
3. `EFMapNode.CanApply()`가 현재 stage와 미완료 상태를 확인한다. Bridge의 FREEZE는 공급 Source의 SourceOn도 요구한다(`EFMapNode.cs:89–104`).
4. `EFMapNode.Apply()`의 FREEZE 분기가 `Frozen = !Frozen`으로 토글한다. 정렬 범위 안에서 얼리면 Angle을 0으로 보정한다(`EFMapNode.cs:121–130`).
5. `EFMapNode.Tick()`은 전원이 있고 Frozen이 아니며 stage가 완료되지 않았을 때만 Bridge Angle을 증가시킨다. 두 번째 FREEZE는 해제이며 이후 Tick에서 재회전한다(`EFMapNode.cs:137–152`).

동시에 얼릴 수 있는 타깃 수 제한은 없다. 후속 연결기 확장을 위해 새 전역 구체 회수 시스템을 만들 이유가 없다.

### A 발판과 B 연결의 제약

동일 Bridge의 단일 Frozen 값만 사용하면 B를 맞추려고 해제하는 순간 A도 함께 움직인다. A를 유지하려면 별도 고정 발판 또는 별도 latch 상태가 필요하다. 검증 비용이 커지면 A 고정 발판 + B 단일 연결로 축소하고 이유를 기록한다. 추가로 좁은 각도·프레임 성공 조건을 만들지 않는다.

## 3. 완료 유지와 보조 인정의 현재 경로

Bridge의 `Satisfied`는 `Frozen && Aligned`다(`EFMapNode.cs:37`). index 2의 requirements는 Source와 Bridge다(`EFMapSetup.cs:129`).

`EFMapWorld.LateUpdate()`는 현재 stage의 도착 영역에서 `TryArrive()`를 먼저 시도하고, 실패하면 `TryPassage()`로 보조 인정한다(`EFMapWorld.cs:108–115`). 정상 완료는 requirements를 확인하고, 보조 인정은 안전한 도착 증거를 사용하며 각도나 사용 이력을 다시 강요하지 않는다. 두 경로 모두 `CanCompleteTraversal()`을 거쳐 현재 미완료 stage 0..4만 완료할 수 있다(`EFMapWorld.cs:173–197`).

`CompleteTraversal()`은 Completed를 true로 고정한다. 실험 사본의 `stabilizeCompletedStages`가 true면 해당 노드의 `ApplyCompletedState()`를 호출하고 공급 순서 차이를 고려해 Tick을 한 번 더 실행한다(`EFMapWorld.cs:199–210`). `EFMapNode.ApplyCompletedState()`는 SourceOn, Frozen, Angle 0과 애니메이션 최종 상태를 적용하지만 최종 stage 5에는 적용하지 않는다(`EFMapNode.cs:76–86`).

원본 씬처럼 해당 옵션이 false면 완료 후 기존 자세에서 추가 회전만 멈춘다. 완료 후에는 현재 stage/Completed 잠금 때문에 지나간 Bridge를 다시 해제할 수 없다. `Retry()`는 미완료인 현재 stage만 초기화한다. `Respawn()`은 준비 상태를 보존한다(`EFMapWorld.cs:220–233`).

## 4. P6의 현재 자동 엔딩 호출 경로

현재 최종 준비 요구사항은 Source, Gate, Repair, Swing 네 노드다(`EFMapSetup.cs:117–130`).

1. `EFMapNode.Apply(Pulse)`가 `SM_PowerSource_P6`의 SourceOn과 Done을 true로 만든다.
2. Gate의 `EFMapNode.Tick()`이 공급 SourceOn을 감지해 수문 애니메이션을 Begin한다. `EFMapAnimation.Tick()`이 끝나면 Gate.Done이 true가 된다.
3. Repair에 BLOOM을 적용하고 성장 애니메이션이 끝나면 Repair.Done이 true가 된다.
4. Swing는 Slate 종류다. FREEZE 후 `Frozen && Aligned`이면 준비 조건을 충족한다. 현재는 전원이 없어도 이 Slate를 FREEZE할 수 있다.
5. `EFMapWorld.Update()`의 `StageIndex == 5 && ConditionsMet(5)`가 true가 되면 즉시 endingStarted를 설정한다(`EFMapWorld.cs:84–86`).
6. waterProgress가 증가하며 finalFlow 항목을 `i * 0.4초` 간격으로 표시한다. `finalFlow.Length * 0.4 + 0.5초` 이후 WheelRunning과 Completed[5]를 true로 만들고 restoration을 Begin한다(`EFMapWorld.cs:87–95`).
7. WheelRunning 상태에서 바퀴를 회전시키고 endingAt 기준 4초가 지나면 Ended를 설정해 `BeginCinematic(endingCamera)`를 호출한다(`EFMapWorld.cs:99–104`).

도착 보조 인정 API는 index 5를 제외하므로 최종 제어부에 도달하는 것만으로 완료되지는 않는다. 그러나 **준비 조건 충족이 곧 복원 시작이며, 별도 마지막 PULSE나 직접 횡단의 증거는 없다.** 최종 수문도 현재는 준비 Source에 PULSE할 때 이미 열린다. 따라서 마지막 입력 → 장치 반응 → 수문 개방 순서는 아직 충족하지 않는다.

현재 스킵 기능은 없다. `ResumeExploration()`은 Ended를 해제하지만 endingShown을 유지하므로 탐색 복귀 후 엔딩 카메라를 반복 재생하지 않는다. 이 기존 동작과 새 스킵·재시작의 관계도 검증해야 한다.

## 5. 마지막 직접 PULSE의 최소 opt-in 제안

아래 API 이름과 자료형은 제안이며 현재 존재하지 않는다. 기본값은 false/null로 두어 기존 원본 씬 동작에 영향을 주지 않는다.

| 소유 파일 | 제안 인터페이스 | 목적 |
|---|---|---|
| `EFMapWorld.cs` | `bool requireFinalPulse = false` | 실험 사본만 마지막 입력 요구 |
| `EFMapWorld.cs` | `EFMapNode finalControl = null` | 최종 입력 대상의 명시적 신원 |
| `EFMapWorld.cs` | private `finalPulseConfirmed`, 읽기 전용 `FinalPulseAccepted` | 중복 입력 방지와 연출 진행 latch |
| `EFMapWorld.cs` | `CanAcceptFinalPulse(EFMapNode, out string reason)` | 준비·입력·횡단 증거 검사 |
| `EFMapWorld.cs` | `TryConfirmFinalPulse(EFMapNode)` | 최종 직접 입력을 한 번만 확정 |
| `EFMapNode.cs` | `CanApply`/`Apply`의 지정 finalControl 분기 | 기존 카메라 타깃·거리·가림 경로 재사용 |

새 finalControl은 stage 5의 별도 Source 노드로 만들고 기존 requirements에 무조건 넣지 않는다. 처음부터 잠긴 최종 제어부가 준비 여부를 설명할 수 있어야 하며, 근처 trigger 진입만으로 confirmed를 바꾸면 안 된다.

`CanAcceptFinalPulse`는 opt-in 여부, 정확한 지정 타깃, 현재 stage 5, GameInputActive, 기존 준비 조건, 직접 횡단 뒤 안전 영역에 실제 접지한 증거를 확인해야 한다. 좁은 각도나 정확한 중앙점은 추가하지 않는다. 정상 플레이의 거리·가림은 기존 `EFMapPlayer.CanCastAt()`을 사용한다. API 확인 메서드도 현재 위치와 상태를 자체 확인해야 한다.

World.Update의 복원 진입은 준비 조건에 `!requireFinalPulse || finalPulseConfirmed`를 추가하는 방식으로 제한할 수 있다. 이것만 적용하면 마지막 직접 입력 P0는 보완하지만 P6의 전체 공간 인과가 완성된 것은 아니다.

### 수문 공급 변경 시 순환 조건 방지

수문을 마지막 입력 뒤 열려면 실험 사본에서 Gate.supply를 finalControl로 바꾸고, 최종 준비 requirements에서 Gate를 제거해 `{source6, repair, swing}`처럼 준비 상태와 복원 출력을 분리해야 한다.

**Gate가 finalControl을 기다리는데 finalControl의 CanAccept가 Gate.Done을 요구하면 순환 조건으로 영구 잠긴다.** Gate는 마지막 입력 후 시퀀스가 기다릴 출력으로 다룬다. 초기 source6 PULSE는 정비 장치의 준비 신호로 유지한다.

### 중복·추락·초기화

- finalPulseConfirmed 이후에는 후속 입력이 복원을 중복 시작하지 않게 한다.
- 복원 시작 후 FREEZE 해제나 예외 추락 때문에 이미 열린 물길이 다시 닫히지 않게 시퀀스를 latch한다. 복원 진행과 완료 상태는 World 또는 한 명의 전담 컴포넌트가 소유한다.
- 일반 Respawn은 준비 상태와 최종 확정을 보존한다. R Retry는 현재 stage 초기화와 별도로 finalPulseConfirmed·횡단 증거·시퀀스를 함께 초기화하거나, 복원 중에는 거부하는 정책을 명시한다.
- 전체 재시작과 새 게임은 scene reload로 모든 latch와 시각 상태를 초기화한다. 플레이어 비활성화·카메라·커서·일시정지도 정상 상태로 돌아오는지 확인한다.

## 6. 정상 종료와 스킵의 공통 최종 상태

정상 시퀀스와 초반/후반 스킵은 idempotent `ApplyFinalState()` 같은 하나의 종점 함수를 호출하도록 제안한다. 단순히 Ended만 true로 설정하는 스킵은 불충분하다.

공통 종점은 최종 수문 애니메이션의 마지막 샘플, 모든 물길 표시, 물레방아 가동, 대표 식생·빛 복원, Completed[5], endingShown, 엔딩 카메라·종료 안내 UI와 입력/커서 상태를 일치시켜야 한다. `EFMapAnimation.Begin()`과 `Tick(Duration)`은 기존 최종 샘플 적용 자원으로 활용할 수 있지만 예외·누락 참조를 포함한 실행 검증이 필요하다. 유체 시뮬레이션이나 새 전역 시스템은 도입하지 않는다.

## 7. 사본 보존과 통합 선행 조건

현재 `Assets/Scenes/Everflow_Map_P3_DesignTest_20261016.unity`는 실제 존재한다. 2026-10-16의 `p3-checks-20261016-132900-485.txt`는 17개 runtime/API fixture PASS 이후 출발대 CharacterController 지지 검사에서 overall FAIL이었다. 이 기록은 전체 물리 완주나 현재 수정본의 통과 증거가 아니다. 재실행 결과가 생기면 별도 검증 문서에 반영한다.

후속 통합은 다음 조건을 지킨다.

1. P3의 기술적 진행 검증이 통과하기 전 P5/P6를 통합 완료로 확대하지 않는다. 독립 조사·분리된 코드 검토는 계속할 수 있다.
2. 원본 `Everflow_Map`과 기존 P3 실험 사본을 모두 보존한다. 후속 단계는 고유 경로의 새 사본을 Unity `SaveScene(..., saveAsCopy: true)`로 만든다. 기존 P3 생성 메뉴는 이미 존재하는 사본을 덮어쓰지 않도록 거부한다.
3. 새 사본에서만 opt-in, finalControl, 안전 횡단 영역, Gate 공급과 requirements 및 stage.nodes 참조를 연결한다. stage index를 전역 재번호화하지 않는다.
4. 기존 `EFMapSetup.Apply`, Presentation Apply나 공유 프리팹 저장을 호출해 원본에 전파하지 않는다. 원본 타이틀·능력 HUD·모델·기본 이동/카메라와 공유 자산을 보존한다.
5. 생성 전후 원본, 기존 P3 사본, 타이틀/HUD, 공유 자산, 빌드 설정의 해시를 비교한다. 씬 YAML 직접 편집, 자동 삭제, reset/clean/push로 원복하지 않는다.
6. 원본 opt-in이 false이면 원본의 준비 자동 엔딩 결함도 그대로 남는다. 원본까지 고쳤다고 보고하지 않는다.

이 문서의 제안은 구현·컴파일·Editor 플레이·빌드 실행 검증을 대신하지 않는다. 초심자 이해도·재미와 목표 플레이시간도 검증하지 않았다.
