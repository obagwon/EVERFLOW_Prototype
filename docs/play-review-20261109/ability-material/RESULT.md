# 7/9 · 탐색 중 원래 재질과 짧은 적중 파동

`Everflow_Map_v2_test_with_puzzle`에서 성장한 잎과 고정한 구조물은 원래 재질로 보인다. 이전에는 성장·고정 상태가 모델 전체의 기본색을 덮어썼다. C 잎은 성장한 크기와 지지면으로 상태가 드러나고, 물의 동결은 기존 얼음 표면과 발판으로 표현된다.

능력 모드의 색은 대상 가장자리에 집중시켰다. 구체가 실제로 도착해 효과를 적용하면 기존 표면 오버레이에서 BLOOM 연두색, FREEZE 하늘색, FLOW 주황색 파동이 약 0.55초 퍼진다. 같은 물체를 여러 능력이 공유할 때는 마지막에 도착한 효과의 파동 하나가 표시된다. 복귀와 재시작은 진행 중인 파동을 정리한다.

- `Assets/EverflowPuzzleLab/Runtime/LabTarget.cs`의 기본 표시 동작에 V2에서 사용하는 선택 항목을 추가했다. `EFV2PuzzleAssembly.cs`가 이 씬의 대상에 원래 재질을 사용하도록 설정한다.
- `Assets/EverflowMap/Runtime/EFV2PuzzleWorld.cs`는 구체 도착 시점을 `EFV2AbilityTargets.cs`에 전달한다. 기존 `EF_AbilityTargetGlow.shader`가 조준 윤곽과 적중 파동을 그린다.

## Unity 검증

- 재질·파동 수명 11/11 통과. 성장 크기·지지면, 동결과 해동, 얼음 발판 충돌, 기본 LabTarget 색, 파동 만료, 복귀·재시작 정리를 격리된 Play 상태에서 확인했다. `material-checks.txt`
- C 연속 입력 경로 63/63 통과. 잎 성장·이동·고정, D2 동결과 해동, D5 수로 정렬과 고정, 정원 도착 및 Enter 재시작을 확인했다. 조준·취소를 포함한 조작 검사도 55/55 통과했다. `route-alternate.txt`, `input-smoke.txt`
- 연속 경로에서 26,336프레임을 관찰했다. 조준 윤곽 558프레임과 적중 파동 1,315프레임을 기록했다. 모드와 어긋난 오버레이, 잎·D5 모델의 기본색 덮어쓰기, 같은 모델의 중복 파동은 각각 0회였다. `input-material-audit.txt`
- `before-grown-leaf.png`와 `after-grown-leaf.png`는 같은 위치에서 촬영한 성장 잎이다. `selection-bloom.png`는 조작 검사의 능력 모드이며, `input-c-bloom-wave.png`는 연속 입력 경로에서 실제로 BLOOM을 적용한 순간이다.
