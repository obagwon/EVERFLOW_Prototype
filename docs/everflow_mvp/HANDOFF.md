# EVERFLOW 통합 MVP 인수인계

2026-10-17 정리 점검 갱신. 단일 에이전트로 수행했다. 최종 씬·타이틀 연결은 실제 파일로 재확인했다. **이번 수정 후에는 오프라인 컴파일·정적 검사만 완료했으며 Unity 임포트/Play/새 빌드는 미검증이다.** 아래의 249개 Play PASS와 1.1.1 빌드는 이전 통합 회차 결과다. 이전 P3 한정 인수인계는 `evidence/integration-20261017/handoff-before-integration.md`에 보존했다.

## 1. 열고 편집할 씬

**`Assets/Scenes/Everflow_Map_MVP.unity`** 하나를 편집한다. 이전 회차에 생성·저장·재로드·Play 검사를 마쳤으며 이번 정리에서 씬 바이트와 GUID를 보존했다. `MVP Integration - editable scene objects` 아래에서 새 발판·최종 제어부·표지를 수정하고, World의 `mvpFinale` 참조에서 최종 준비/연출을 확인한다.

원본 `Assets/Scenes/Everflow_Map.unity`와 기존 `Everflow_Map_P3_DesignTest_20261016.unity`는 보존됐다. 공용 프리팹에는 Apply하지 않았다. 기존 맵 생성/Apply Integration 도구로 이 씬을 다시 생성하지 않는다. `Create or Open MVP`는 기존 씬을 재생성하지 않으며, 최초 장문 표지에 한해서만 백업 후 짧은 표지로 이관한다. 이미 수정한 표지 문구는 보존한다.

## 2. 타이틀에서 실행

Unity 6000.3.21f1에서 `Assets/EverflowMVP/Scenes/Everflow_Title.unity`를 열고 Play → 기존 **게임 시작** 버튼. 이제 `Everflow_Map_MVP`가 열린다. Title/MVP가 첫 두 활성 빌드 씬이며 원본/유틸리티 씬은 비활성으로 남는다. 기존 타이틀 이미지·버튼·능력 HUD·이동/점프/추적 카메라를 유지했다.

Windows 실행 파일: **`Build/1.1.1/EVERFLOW.exe`**. 이전 통합 회차 빌드(오류0/경고485)이며 이번 정리 수정은 반영되지 않았다. 폴더의 Data/DLL과 함께 보존하고 exe를 실행한다. 이번에도 기존 실행 파일의 headless 타이틀→MVP 진입과 새 게임 2회, 종료 코드0을 확인했다(`evidence/cleanup-existing-player.json`). 화면 렌더링·전체 수동 완주는 미검증이다. 현재 작업 환경에서는 정상 사용자 Editor 연결이 없어 수정 후 임포트/Play/빌드를 실행하지 못했다. 이전 빌드의 smoke 옵션은 이 기존 실행 파일에서만 사용할 수 있다. 새 소스의 smoke 자동 실행 클래스는 `DEVELOPMENT_BUILD`에서만 컴파일되며 일반 릴리스에는 없다.

자동 재검사는 Play를 끈 상태에서 `Tools > EVERFLOW > MVP Integration > Run MVP Checks`. 검사가 실제로 Play를 켜고 종료한다. 이번 소스 수정으로 이전 `verified.json`의 소스 fingerprint는 오래된 상태이므로 Publish 전에 다시 검사한다. 과거 원본용 `Map > Run Physical and State Tests`는 타이틀이 원본 Map을 연다고 가정하는 역사 검사다. 현재 MVP 연결에서는 씬을 바꾸기 전에 중단하고 통합 검사 메뉴를 안내하도록 수정했다.

## 3. 완료·부분 완료·미구현

- 완료: 수로 접근/계단/옆길/물·얼음/잎 횡단/하행 계단, 회전기 고정 A+가동 B와 중간 체크포인트, 최종 BLOOM→정비 PULSE→FREEZE→직접 횡단→별도 마지막 PULSE, 복원/정상·스킵 공통 완료/연출 중 복구·재시작/반복 새 게임.
- 실제 Editor 검증: `evidence/integration-20261017/mvp-checks-20261017T070952791Z.txt` **249개 PASS**. 타이틀 버튼, UI P1~P6 실제 CharacterController 이동·레이/키/클릭·탑승, 잘못된 능력, 낙하·완료 유지, 최종 도달/먼 가장자리 우회 차단, 정상/초반 Space 스킵/후반 완료 API의 최종 상태 일치, 탐색 복귀, 연출 중 재시작과 반복 초기화를 포함한다. 스킵·우회·재시작 비교의 준비는 명시적인 API/위치 fixture다. 실제 사람의 마우스 플레이가 아니다.
- 수로 별도 실험 검증: `evidence/p3-checks-20261017-064730-116.txt` runtime77개 PASS. 이후 통합 씬에서도 실제 접근·횡단·출구와 다음 구간 접속을 검사했다.
- 부분 완료: 임시 도형 중심의 시각 표현, 대표 식생/빛과 짧은 합성음, 기존 조망 카메라. 회전기 A는 고정 발판으로 축소했다. 기존 모델의 단일 Frozen/Angle 구조에 별도 A/B 상태 장치를 추가하지 않기 위한 선택이다.
- 미구현/미검증: 완성 미술·전 구간 음향/접합 정리, 동적 A 걸쇠, 실제 초심자 테스트, 재미·이해도·8~9분 달성. 유체 시뮬레이션과 전역 구체 회수는 추가하지 않았다.

## 4. 조작과 퍼즐 해결 순서

WASD 이동 / 마우스 시점 / C 재정렬 / Space 점프·탑승 해제 / E 탑승 / 1 BLOOM·2 FREEZE·3 PULSE / 클릭 능력 / Esc 일시정지 / R 현재 퍼즐 재시도. **낙하는 준비 상태를 유지하고 R은 현재 준비를 의도적으로 초기화한다.** 새 게임은 월드 전체를 다시 로드한다.

| 기획 역할 | 실제 UI / stage index | 실제 대상·해결 순서 |
|---|---|---|
| P1 꽃+P2 덩굴 | P1 / 0 | `SM_FlowerJumpPad` BLOOM→꽃 위에서 Space→`SM_Vine` BLOOM→E/Space로 건너기 |
| 기존 잔해·석판 | P2 / 1 | `SM_FallingDebris_01..03` 순차 FREEZE·점프→`SM_Slate` FREEZE·횡단 |
| P5 회전 연결기 | P3 / 2 | `SM_PowerSource_P3` PULSE→고정 A→중간 안전대→파란 B 표지의 `SM_RotatingBridge` FREEZE→서쪽 횡단. FREEZE 재입력으로 B 해제/재가동해도 A 유지 |
| P3 수로+P4 잎 | P4 / 3 | `SM_PowerSource_P4` PULSE→`SM_WaterGate_P4` 개방·물 생성→낮은 수조 또는 높은 `SM_IceSlide` FREEZE→펌프 남쪽 옆길/측면 계단→출발대 E→`SM_RideLeaf` 이동→넓은 출구를 걸어서 통과→두 하행 계단 |
| 기존 뿌리·문 | P5 / 4 | `SM_VineRoot_P5` BLOOM 완료 후 같은 뿌리에 PULSE→`SM_Door_P5` 통과→기존 계단 |
| 최종 종합/복원 | P6 / 5 | 입구 뿌리 타깃(`P6_ChannelRepair`) BLOOM→접근 계단·녹색 길→`SM_PowerSource_P6` PULSE→`SM_Channel_Swing_P6` FREEZE→정비 측에서 얼음 연결을 직접 건너기→`Final control - last PULSE`의 수정에 **마지막 PULSE** |

마지막 입력 후 장치 반응→수문→단계적 물 표시→물레방아→기존 식생 클립/빛→조망/종료 안내. 연출 중 Space 또는 Skip으로 같은 최종 상태에 도달한다. 종료 화면의 Restart world/Title/Continue exploring을 사용한다. 도달만 하거나 준비만 해서는 엔딩이 시작되지 않는다.

## 5. 남은 문제·원복·다음 작업

현재 자동 검사 경로의 진행 차단은 발견되지 않았다. 모든 자유 이동/카메라 각도/화면 크기를 다룬 검사는 아니며, 새 길은 회색 도형과 영문 표지 중심이다. 랜드마크 시야·소리의 체감, 초심자 조준/이해도와 재미는 사람 검증이 필요하다. 빌드 렌더링/전 구간 수동 완주는 별도 검증 항목이다. 빌드 경고485개는 기존 `com.unity.ai.inference`의 ConvGeneric/Sentis 셰이더 경고로 분류했고 패키지는 교체하지 않았다. 상세 증거는 VERIFICATION 참조.

원복: 원본 `Everflow_Map.unity`를 직접 열면 기존 배치로 돌아간다. 타이틀 경로만 되돌리려면 `EFTitleScreen.GameplayScene`을 원본 경로로 바꾸고, `evidence/integration-20261017/build-settings-before-publish-20261017T070312805Z.txt`의 씬 목록을 검토해 복원한다. 새 MVP의 표지 수정 전 byte 백업은 `mvp-before-label-repair-20261017T070749354Z.unity`, P3 접근 수정 전 백업은 `evidence/resume-20261017/p3-before-access-repair-*.unity`다. 사용자 변경까지 checkout/reset/clean하지 않는다. 공유 런타임의 새 옵션은 원본 씬에서 꺼져 있다. 원본 Map의 과거 자동 엔딩 동작도 그대로 남으므로 이번 마지막 PULSE 개선은 MVP 씬에서 확인한다.

다음 작업은 사람 검증 기록을 바탕으로 표지·시야·모델 접합과 음향을 정리하는 것이다. `tools/mvp_staging`은 과거 준비 사본이며 실제 수정 대상이 아니다.

## 6. 먼저 확인할 5가지

1. 타이틀 이미지/버튼이 유지되고 시작 시 MVP의 P1·인트로·HUD가 정상인가.
2. UI P3의 A가 계속 남고 중간 안전대에서 B를 동결·해제·재연결할 수 있는가.
3. UI P4 펌프 옆길/측면 계단/얼음·잎·출구 하행이 연결되고 낙하 후 준비가 남는가.
4. P6 준비/최종 도달만으로 끝나지 않고 횡단 후 별도 PULSE로만 복원이 시작되는가.
5. 정상 복원과 Space/Skip 후 결과가 같고 새 게임을 두 번 반복해 장치·물·진행·카메라가 초기화되는가.

## 7. 이번 정리와 이후 편집 위치

- 기존 파일 삭제·이동은 하지 않았다. 기존 미커밋 통합 작업, 원본 Map/P3 사본, 팀원 FBX·이미지·라이선스·기존 증거/백업을 보존했다. 런타임/Editor 분리는 이미 적절해 대량 재배치가 필요하지 않다. 이번 검사에서 만든 `Temp/cleanup-validator`의 임시 파일4개만 `Backups/20261017-cleanup/validator-fixture.zip`에 바이트 검증 후 보관하고 제거했다.
- `Assets/Settings/DefaultVolumeProfile.asset`에서 효과 목록에 연결되지 않고 외부 직렬화 참조도 없는 깨진 하위 자산 9개만 제거했다. Missing Script 5개와 미해결 스크립트 4개다. 정상 효과 19개·루트 목록·나머지 블록·meta는 그대로다. `evidence/cleanup-volume-profile.json`에 이름/fileID와 백업을 기록했다.
- 존재하지 않는 `SampleScene`의 비활성 Build Settings 항목 제거. 활성 씬은 Title/MVP 두 개, 원본 Map/6Puzzles는 비활성 보존. 저장된 별도 Build Profile 자산은 없으며 Editor의 현재 활성 프로필은 미확인이다.
- 최종 구역 검사에서 gate를 null 검사 전에 접근하던 오류를 고쳤다. 체크포인트·경로 Renderer·grounding 월드 검사도 추가하고, 일반 릴리스 사전 검사에서 MVP 전용 검사를 호출한다. finale 참조를 잃어 원본의 자동 엔딩 경로로 빠지는 구성을 사전에 거부한다.
- `.gitignore`에 로컬 `Backups/`와 Python 캐시만 추가했다. Unity 캐시/빌드/로그 제외를 유지했고 추적 해제·이력 변경·push는 하지 않았다.

| 위치 | 편집 용도 |
|---|---|
| `Assets/EverflowMap/Runtime`, `Editor`, `Prefabs`, `Materials` | 현재 맵 동작, Unity 메뉴/검사, 플레이어 원형, 맵 재질. 프리팹 Apply는 별도 의도 없이 하지 않음 |
| `Assets/map_ver1/fbx` | 실제 사용하는 분리 모델/복원 애니메이션. `map_ver1.fbx` 통합 원본도 모델 교체용으로 보존 |
| `Assets/EverflowMVP/Runtime/EFTitleScreen.cs`, `Scenes/Everflow_Title.unity`, `Assets/logo`, `Assets/icon` | 현재 타이틀과 원본 UI 이미지. 같은 폴더의 6Puzzles/기타 런타임은 이전 퍼즐 검증용 |
| `Assets/EverflowGraybox`, `Assets/EverflowPrototype`, 이전 실험 씬 | 기존 검증과 공유 Ability/Input 의존성. 미사용으로 단정하거나 삭제하지 않음 |
| `tools`, `docs/everflow_mvp` | 오프라인 검사와 현재 안내/증거. `tools/mvp_staging`은 미커밋 역사 사본이므로 유지하며 현재 소스로 복사하지 않음 |

`Assets/Resources`는 빈 폴더다. 프로젝트 런타임의 Resources 호출은 내장 폰트 조회뿐이고 StreamingAssets/Addressables/AssetBundle 로딩은 없다. 문자열 씬 경로는 Title↔MVP와 현재 씬 재시작에 사용한다. Layout JSON/모델 경로는 기존 Editor 생성기에 사용되므로 보존했다. 프로젝트 asmdef는 없어 기존 Assembly-CSharp/Editor 경계를 유지했다. 별도 사운드 파일 대신 finale의 짧은 합성 chime을 사용하며 완성 음향은 미완료다.

정적 씬/스크립트 검사53개 PASS, C# Editor/런타임/일반 플레이어/개발 플레이어 컴파일 PASS, 관리 코드 대체 객체를 사용한 실제 validator 본문 검사11개 PASS. `PC_Renderer.asset`의 기존 probe 디버그 리소스 GUID 7개는 설치 패키지에서 미해결 상태다. 기능 영향이 불확실해 보존했으며 전체 에셋 검사는 이 때문에 FAIL을 유지한다(`cleanup-audit.json`의 `scene_and_script_pass=true`, 전체 `pass=false`). 이를 플레이 결함이나 해결 완료로 단정하지 않는다. 수정 후 Unity 임포트·씬 재로드·퍼즐/추락/최종 PULSE·스킵/재시작·새 빌드는 미검증이다.

백업은 **`Backups/20261017-cleanup/`**(Assets 밖, Git 제외)에 있다. `baseline.json`, 작업 전 Git status/diff, 수정 전 파일의 원래 상대경로 사본이 있다. 원복하려는 파일 하나만 같은 상대경로로 복사한다(예: `Backups/20261017-cleanup/Assets/Settings/DefaultVolumeProfile.asset` → `Assets/Settings/DefaultVolumeProfile.asset`). 이후 사용자 변경이 생겼으면 먼저 diff를 비교한다. scene/meta는 수정하지 않았으므로 재생성하지 않는다. 이 로컬 백업은 다른 기기로 인계할 때 별도 보존한다. reset/clean이나 이전 통합 전체 덮어쓰기는 사용하지 않는다.
