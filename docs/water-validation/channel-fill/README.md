# G 수로·폭포 물 채움 개선

`Everflow_Map_v2_test_with_puzzle`의 G 수로에 적용했다.

- 수면 높이 15.225 → 15.49. 기존 수로 벽 상단 15.63보다 14cm 낮게 유지.
- 기존 곡선 물길의 같은 메시를 닫힌 물 체적으로 확장. 별도의 겹친 평면이나 충돌체를 추가하지 않음.
- G 수로 약 35cm, 연결된 낙수 약 16cm 깊이. 앞·뒤·옆면과 끝단을 연결.
- 체적 면에서 배경이 과도하게 비치는 현상을 줄이고 기존 표면 흐름·물거품 유지.
- 작은 수차 쪽 수로도 같은 높이로 맞추되 기존 FLOW 공급 조건을 유지. 공급 전에는 원래처럼 비어 있음.
- 물 공급·동결 조건, 기존 입력·카메라 설정·충돌은 변경하지 않음.

변경: `EFWaterNetworkTools.cs`, `EF_WaterNetwork.shader`, `W_H_to_G_Fall_Network.asset`, `W_G_ToWheel_Network.asset`, 대상 씬의 물 낙하지점 높이.

Unity Play에서 전후·측면·공급 상태를 확인했다. 공급 상태 화면은 시각 검토를 위해 장치 상태를 설정한 화면이며 일반 입력 완주 기록이 아니다.

`closed-body.txt`: 두 메시 모두 열린 경계 0 / 비다양체 경계 0 (위치 기준 용접 검사).
`runtime-pause.txt`: 능력 선택 중 로컬 물 시간 정지, 취소 후 재개 확인.
`before.png` / `after.png`, `before-side.png` / `after-side.png`, `after-supplied.png`: 같은 카메라 위치에서 렌더링한 실제 Unity 화면.

추가 검증: 실제 Input System 키보드·마우스 회귀검사 50개, 실패 0. 전체 시작→정원 경로는 이번 시각 수정에서 재실행하지 않았다. Unity Play 종료, Run In Background 원래 값 복구, 씬 저장 완료.
