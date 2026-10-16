# Ability HUD

## 적용한 씬과 구조

실제 게임 씬은 `Assets/Scenes/Everflow_Map.unity`다. 기존 `EF_MapSystems` 아래에 씬 소유의 `Ability HUD` Canvas를 하나 저장했다. 런타임 생성, Resources 검색, DontDestroyOnLoad를 사용하지 않는다. 타이틀에는 HUD가 없으며 Single 씬 로드/재시작 시 이전 HUD와 이벤트 구독도 정리된다.

```
EF_MapSystems
└─ Ability HUD (Canvas, CanvasScaler, CanvasGroup, EFMapAbilityHUD)
   └─ Bottom Center (margin 24)
      ├─ Description backing
      ├─ BLOOM / FREEZE / PULSE
      │  ├─ Icon (Image)
      │  ├─ Shortcut (Text)
      │  ├─ Ability name (Text)
      │  └─ Selected underline (Image)
      └─ BLOOM / FREEZE / PULSE description (Text 각각 별도 요소)
```

기존 상단 진행 정보·조준점·일시정지·엔딩 UI는 `EFMapWorld.OnGUI`에 유지했다. 기존 하단 대상/조준 사유 텍스트는 능력 HUD보다 위로 올렸다. 모든 HUD Graphic의 Raycast Target은 꺼져 있고 CanvasGroup의 Blocks Raycasts/Interactable도 꺼져 있으며 GraphicRaycaster를 추가하지 않았다.

## 실제 선택 능력과 연결

`EFMapPlayer`의 기존 1/2/3 입력이 `selected`를 바꾼다. 같은 공개 API를 프로퍼티로 바꾸고 직렬화 필드 `selectedAbility`에 `[FormerlySerializedAs("selected")]`를 적용해 기존 프리팹의 선택값을 보존했다. 값이 실제로 바뀔 때만 `AbilityChanged`가 발생한다.

`EFMapAbilityHUD`는 OnEnable에서 이 이벤트를 구독하고 즉시 `player.selected`로 최초 화면을 동기화한다. OnDisable에서 구독을 해제한다. UI에는 선택 상태나 키 입력 처리를 별도로 만들지 않았다. 기존 조준·사거리·능력 적용·이동·점프·카메라 입력은 그대로다.

선택 아이콘은 1.1배, 밝기 1, 흰 밑줄로 표시한다. 비선택 밝기는 0.78이며 투명도는 1을 유지한다. 설명은 실제 선택 능력에 해당하는 Text 하나만 활성화한다. 인트로·일시정지·엔딩에는 `world.GameInputActive`를 읽어 HUD를 숨긴다.

## Inspector에서 수정하는 위치

| 수정 대상 | 위치 |
|---|---|
| 아이콘 이미지 | 각 능력의 `Icon > Image > Source Image` |
| 단축키 문구 | `Shortcut > Text` |
| 능력 이름 | `Ability name > Text` |
| 한국어 설명 | 패널 아래 각 `... description > Text` |
| 전체 하단 여백·폭 | `Bottom Center (margin 24) > RectTransform` |
| 능력 간격 | BLOOM/FREEZE/PULSE RectTransform의 X: -150 / 0 / 150 |
| 아이콘 크기·중심 | 각 Icon의 RectTransform |
| 선택 확대·비선택 밝기 | `Ability HUD > EFMapAbilityHUD` |
| 글자 크기·색·그림자 | 각 Text 및 Shadow 컴포넌트 |
| 설명 배경 | `Description backing > Image / RectTransform` |

CanvasScaler는 Scale With Screen Size, 기준 1920×1080, Screen Match Mode=Expand다. 패널은 하단 중앙 고정, 아래 여백 24, 크기 600×176이다. 아이콘의 실제 원형은 약 76px, 선택 시 약 83.6px이다. 이미지 전체 Rect 크기는 BLOOM 약 94.36, FREEZE 약 98.56, PULSE 약 90.34px로 다르다. 원본 PNG를 자르거나 고치지 않고 알파 80/255 이상 영역을 측정해 투명 여백과 중심을 보정했다. 측정 자료는 `docs/ui-progress/icon-alpha-measurements.json`에 있다.

이미지는 지정한 `Assets/icon/*_icon.png`를 직접 직렬화 참조한다. Single Sprite, 알파 유지, NPOT 스케일 없음, mipmap 없음으로 설정했다. 원본 픽셀은 수정하지 않았다.

한국어는 현재 Windows 배포 환경에 설치된 **Malgun Gothic**을 런타임 동적 폰트로 사용한다. 이 세션에서 `C:/Windows/Fonts/malgun.ttf`의 존재를 확인했다. OS 폰트를 프로젝트에 복사하지 않는다. 다른 배포 환경에서는 해당 한국어 폰트를 설치하거나, 라이선스가 확보된 한글 Font 에셋을 각 Text에 지정하면 된다. 명시적으로 지정한 사용자 폰트는 유지한다. 대체 후보는 Apple SD Gothic Neo, Noto Sans CJK KR다. 에디터 비실행 상태의 기본 Text 미리보기는 LegacyRuntime 폰트이므로 한글 표시 검증은 Play Mode에서 해야 한다.

## 적용·검증

이미 씬에 저장되어 있어 적용 메뉴를 먼저 누를 필요가 없다. `Tools > EVERFLOW > Presentation > Apply Title, HUD and Passage Markers`는 기존 HUD를 발견하면 다시 만들지 않는다. Inspector에서 수정한 HUD 배치를 덮어쓰지 않는다.

수행한 검사: 설치된 Unity 6000.3.21f1의 실제 DLL을 참조한 Roslyn 컴파일(런타임 에디터/런타임 플레이어/에디터 코드), 씬 내 참조·계층·Sprite GUID, UI 입력 차단 여부의 직렬화, 1920×1080·1920×1200·1280×720·1440×900의 RectTransform 수치 검사. 결과는 `docs/ui-progress/compile-results.json`, `static-results.json`, `static-results.txt`를 참고한다.

**실제 Unity 실행 검증은 미수행이다.** 배치 에디터가 Licensing Client IPC 연결 거부로 초기화에 멈췄다. 소스 컴파일은 Unity의 에셋 임포트나 플레이어 빌드 성공을 뜻하지 않는다. 이 작업에서 실제 게임 스크린샷을 생성하지 않았다.

Unity가 실행되는 환경에서 `Tools > EVERFLOW > Presentation > Run UI and Passage Regression Tests`를 실행한다. 실제 플레이어 키 입력 경로로 1/2/3 선택과 표시 일치, 한국어 글리프, 타이틀 왕복/재시작 중복, UI 레이캐스트를 검사한다. 그래픽 장치가 있으면 실제 Canvas를 카메라로 렌더링한 16:9/16:10 캡처를 `docs/ui-progress`에 저장한다. 이 캡처는 OS 화면 스크린샷이나 사람의 플레이 증거와 구분한다.

추가 수동 확인: 각 설명의 한글 렌더링과 글자 간격, 밝은 배경 가독성, 16:9/16:10 Game View, 캐릭터/조준점 가림 여부, 마우스 회전·능력 사용·이동·점프, 인트로 후 커서 잠금, 타이틀 왕복과 Restart world 이후 HUD 하나만 존재하는지.
