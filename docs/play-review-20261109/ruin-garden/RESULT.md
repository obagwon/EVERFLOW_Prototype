# 9/9 유적 화단

`Everflow_Map_v2_test_with_puzzle`에서 물레 쪽과 정원 끝에 낮은 석조 화단 두 곳을 만들었다. 돌 테두리 안에 흙과 돌조각을 배치하고, 기존 `PH_A_Leaf02` 메시로 식물 여덟 군집을 구성했다. 발판 높이와 도착 지점은 기존 위치를 사용한다.

물레 복원 후 도착 지점을 밟으면 입구 쪽 식물부터 차례로 자란다. 약 1.8초에 걸쳐 잎과 줄기가 커지고 갈색에서 녹색으로 바뀐다. 기존 도착 효과음은 같은 도착 이벤트에서 재생된다. 일시정지는 성장도 멈추고, R 복귀는 성장 상태를 유지하며, Enter로 다시 시작하면 마른 상태로 돌아간다.

## 변경 파일

- `EFV2PuzzleAssembly.cs`는 화단과 식물을 구성한다.
- `EFV2Plant.cs`와 `EFV2PuzzleWorld.cs`는 도착 후 성장, 정지, 초기화를 처리한다.
- `EFV2PuzzlePlayChecks.cs`는 실제 도착 후 전체 성장과 Enter 초기화를 연속 입력 경로에서 확인한다.

## Unity 검증

- [정원 검사 20/20](garden-checks.txt). 통로 폭과 도착 발판 높이, 화단에 가리지 않는 물레 시선, 복원·도착 순서, 성장 중 일시정지와 재개, 복귀·재시작을 확인했다.
- [연속 입력 47/47](route-bloom.txt). 시작 지점에서 능력 입력으로 물레를 복원한 뒤 정원에 걸어가 성장 완료와 재시작을 확인했다.
- [4.6초 도착 영상](garden-arrival.mp4) · [도착 전](dry-approach.png) · [성장 중](growth-begins.png) · [성장 완료](restored-landing.png).

## 화면 점검

기존 로우폴리 유적에 맞춰 회녹색 석재, 갈색 흙, 두 가지 잎 색을 사용했다. 낮은 화단과 돌조각이 물레의 따뜻한 목재색과 절벽의 차가운 그림자 사이에서 읽히도록 배치했다.

| 점검 범위 | 이전 | 이후 |
| --- | --- | --- |
| 색 구성 | 2/5. 단색 발판과 작은 잎 | 3/5. 석재·흙·식생이 구분됨 |
| 환경 통일감 | 2/5. 발판 위에 식물이 흩어져 있음 | 3/5. 같은 유적 석재와 접지된 화단 |
| 도착 피드백 | 2/5. 정원에 닿기 전에 성장 완료 | 3/5. 도착 후 입구에서 안쪽으로 성장 |

Unity `craft-game-floors`의 `art-direction.md`, `game-feel.md`, `visual-scorecard.md`를 정원 범위에 적용했다. 에디터 캡처와 성장 보간은 설치된 Unity 버전의 API를 확인했다. [Application.runInBackground](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/runinbackground) · [Time.timeScale](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/time/timescale) · [Mathf.SmoothStep](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mathf/smoothstep)
