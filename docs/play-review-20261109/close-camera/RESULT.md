# 6/9 · 좁은 공간의 카메라와 캐릭터 페이드

`Everflow_Map_v2_test_with_puzzle`의 Cinemachine Deoccluder를 높이 보존 방식으로 설정했다. 장애물에 닿으면 같은 높이의 옆 공간도 탐색하며, 충돌 검사 반지름은 0.25m에서 0.20m로 줄였다.

카메라가 캐릭터 구도 기준점에서 2.35m 안으로 들어오면 스킨 메시를 픽셀 단위로 옅게 그린다. 0.85m에서 표시 비율의 하한인 8%에 도달한다. 화면과 깊이 패스에 같은 패턴을 적용해 캐릭터 뒤의 계단과 통로가 드러난다. 시야가 확보되거나 오프닝·복원 연출이 시작되면 원래 재질을 쓴다.

- `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`에 카메라 설정과 페이드 컴포넌트를 저장했다.
- `Assets/EverflowMap/Runtime/EFV2CharacterCameraFade.cs`가 실제 카메라 위치를 읽어 재질 전환을 담당한다.
- `Assets/EverflowMap/Shaders/EF_CharacterCameraFade.shader`가 URP Lit 조명과 원래 캐릭터 텍스처를 사용해 픽셀을 걸러낸다.

## Unity 검증

- 탑·터널의 네 지점에서 24개 수평각과 세 수직각으로 각 설정을 288회 비교했다. 1.25m 이내 밀착은 125회에서 11회로 줄었고, 최소 거리는 0.62m에서 1.15m로 늘었다. 양쪽 설정의 정지 표본에서 초점 직선 차단과 화면 앞 평면 충돌은 감지되지 않았다. `camera-sweep.txt`
- 근접 검사 11/11 통과. 실제 1키와 Esc로 능력 모드를 오갔으며 고정된 탑 위치에서 카메라 차이는 0.0000m와 0.000도였다. 페이드 진행, 원래 재질 복구, 일시정지 중 페이드, 오프닝 재질을 확인했다. `fade-checks.txt`
- 연속 입력 46/46 통과. 탑 계단과 터널, 지하 능력 사용, 물레방아 복원, 정원 도착과 재시작까지 진행했다. 조작 검사도 55/55 통과했다. `route-bloom.txt`, `input-smoke.txt`
- 연속 경로에서 수집한 탐색 표본 8,670개 중 497개에서 페이드가 작동했다. 화면 앞 평면의 검사 지점과 지형의 충돌은 0개였다. 풍차 기둥·탑 벽·계단 등이 카메라에서 캐릭터까지의 직선을 가린 표본은 14개였다. 연출 구간의 359개 표본에서는 캐릭터가 불투명했다. `route-camera-audit.txt`
- `before-tower-forward.png`와 `after-tower-forward.png`는 탑의 같은 캐릭터 위치와 같은 시점 입력으로 촬영했다.

검사 도중 Unity CLI가 응답을 멈췄다. 씬 저장을 확인하고 에디터를 다시 열어 연속 입력 검사를 완료했다. 당시 임시 복구 사본은 `editor-recovery.backup`에 보관했다.
