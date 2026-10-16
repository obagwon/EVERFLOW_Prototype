# 전시 실행·배포 흐름 통합 — 작업 범위

기준일: 2026-10-16

추천 근거는 `docs/EVERFLOW_project_analysis_2026-10-11.md`의 1번 작업이다. 그 문서는 Graybox를 게임 씬으로 제안했지만, 이후 사용자 작업과 `docs/TitleAndPuzzleProgress.md`는 `Assets/Scenes/Everflow_Map.unity`를 실제 게임 씬으로 지정했다. 현재 사용자 연결을 보존하며 타이틀·Release·엔딩 흐름의 남은 결함을 수정한다.

완료 기준:

1. 타이틀 시작과 Release 빌드가 같은 `EFTitleScreen.GameplayScene`을 사용한다.
2. Release 설정의 반복 적용이 타이틀→Map 순서를 유지하고 기존 검증 씬의 파일·목록 항목을 보존한다.
3. Map의 플레이어·카메라·퍼즐·물 전파·물레방아·엔딩 참조가 배포 전 검증 대상에 포함된다. 이전 MVP를 Map으로 잘못 인정하지 않는다.
4. 이미 연결된 엔딩의 전체 재시작과 타이틀 복귀 경로를 확인하고, 불필요한 런타임 수정은 하지 않는다.
5. 원본 사용자 씬, 이미지, FBX, 퍼즐 배치·판정·이동·카메라, 프로젝트 버전과 설정 파일은 보존한다.
6. 구현 담당의 수정 완료 후 독립 담당이 검증한다. 결함 반환에 따른 수정·재검증은 최대 2회다.

검증 범위:

- 이번 작업 시작 시점의 388개 파일 SHA-256을 `baseline-sha256.json`에 기록했다. 기존 Git 변경과 미추적 파일을 포함한다.
- Git LFS 필터가 `.git/lfs/tmp` 쓰기 권한 오류를 냈으므로 이후 상태 조회는 LFS 필터를 비활성화한 읽기 명령을 사용한다. 이 상태 조회만으로 이미지 보존 여부를 판정하지 않고 실제 파일 해시로 비교한다.
- 작업 전 확인한 Unity 계열 PID 29300은 독립 검증 담당이 실행 경로를 조회한 결과 Editor가 아닌 Unity Hub의 `resources/unity.exe`였다. 실제 Editor는 실행 중이지 않은 것으로 확인했다. 기존 프로세스는 종료하지 않으며, 저장 없는 단일 Editor 배치 검증을 시도한다.
- 로컬 Unity DLL을 참조한 C# 컴파일, 독립 검사, 코드·직렬화 참조 검토는 실제 Unity 임포트·Play Mode·Windows 빌드/실행 결과와 구분한다. 과거 보고서의 PASS를 이번 실행 결과로 재사용하지 않는다.

역할과 편집 소유권:

- 총괄: 범위·완료 기준·보존 기준·최종 보고.
- 구현 담당: `EFReleaseSetup.cs`, `EFReleaseBuild.cs`, `EFReleaseTests.cs`의 필요한 최소 수정.
- 독립 검증 담당: 전용 검증 도구와 `verification*` 증거 파일만 작성. 제품 코드 수정 금지.

실제 결과와 남은 확인은 `verification*` 및 `report.md`에 기록한다.
