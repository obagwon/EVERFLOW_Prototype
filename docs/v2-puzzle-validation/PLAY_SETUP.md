# Unity Play와 테스트 연결

Initial Ctrl+R import and live Editor connection are complete. Both continuous synthetic input routes passed. See PLAY_RESULTS.md for current evidence.

현재 작업 도구는 `home\CodexSandboxOffline` 계정에서 실행되지만 Unity Hub/라이선스는 정상 Windows 사용자 `vzd12` 세션에 있다. 같은 프로젝트의 정상 사용자 에디터에 연결하면 이 계정 차이로 새 에디터를 실행할 필요가 없다. 라이선스 파일, 파이프 권한, Windows 보안 설정은 변경하지 않는다.

## 지금 열린 Unity에서 한 번 할 일

`Ctrl+R`로 새 `EFV2EditorBridge.cs`를 임포트한다. 이후 에이전트가 `Temp/v2-puzzle-bridge/status.json`의 실제 에디터 상태를 확인하고 고정된 요청을 보낼 수 있다. 이 초기 갱신은 현재 도구에 Unity 창의 키보드 제어 기능이 없어 필요한 단계다.

## 이후 자동으로 할 수 있는 것

- 새 퍼즐 씬 열기와 임포트된 참조 검사
- Play 시작/중지
- 강제 장치 상태 검사, 실제 Input System 이벤트 스모크 검사
- Play 상태/장치 좌표 조회, Game 화면 캡처
- 이후 코드 수정의 AssetDatabase 갱신

허용 명령은 위 작업으로 제한되어 있다. 임의 C#·셸 실행, 원본 씬 저장, 라이선스 변경, 빌드/게시 명령은 없다. 현재 요청에 대한 허용 파일은 2시간 후 만료된다. Unity 메뉴 `Tools > EVERFLOW > V2 Puzzles > Disable Test Bridge`에서 끌 수 있다.

## 직접 실행할 때

새 씬: `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`.

Unity가 닫혔을 때는 정상 Windows 탐색기에서 `tools/Open_V2_Puzzle_Tests.cmd`를 실행한다. 기존 에디터나 프로젝트 잠금이 있으면 새 에디터를 띄우지 않고 보존한다. Python이 설치된 현재 환경을 사용하며 실행 정책이나 관리자 권한을 바꾸지 않는다.

에이전트/터미널 명령:

```text
python tools/request_v2_editor.py grant
python tools/request_v2_editor.py status
python tools/request_v2_editor.py open
python tools/request_v2_editor.py forced
python tools/request_v2_editor.py input
python tools/request_v2_editor.py play
python tools/request_v2_editor.py inspect
python tools/request_v2_editor.py screenshot
python tools/request_v2_editor.py stop
```

요청을 보냈다는 출력은 실행 성공이나 테스트 통과를 뜻하지 않는다. `status`의 결과와 `docs/v2-puzzle-validation/runtime-*.txt`를 확인한다. 강제 상태 검사와 합성 입력 스모크는 시작부터 엔딩까지 일반 입력으로 완주한 결과와 구분한다.
