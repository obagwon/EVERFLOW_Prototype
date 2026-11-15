# character_1 플레이어 적용

대상: `Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity`.

## 현재 모델: character_fixed (2026-11-09 교체)

플레이어 모델을 `Assets/character_1/model/character_fixed.fbx`로 교체했다. 새 FBX의 유효한 Humanoid Avatar를 연결하고 기존 높이/발 위치에 맞췄다. 기존 애니메이션 FBX 4개, 해당 임포터 설정, `Character1.controller`, `Character1.mat`는 유지했다. 게임 플레이/카메라/충돌 코드는 수정하지 않았다. 자동 Humanoid 변환에서 누락된 LeftHand는 기존 검증된 애니메이션의 본 매핑으로 연결했다.

fixed 모델로 실제 Unity 입력 검사 **13/13 통과**. 이번 교체에서는 이동·점프·낙하·착지, 능력 모드/일반 일시정지의 애니메이션 정지, 취소/재개, 리스폰/재시작을 다시 검사했다. 아래의 전체 퍼즐 두 경로 검사는 최초 통합 때의 기록이며, fixed 교체 후 전체 경로를 다시 실행한 결과는 아니다.

- [fixed 시연 영상](fixed/character-integration.mp4)
- [fixed 검사 결과](fixed/character-checks.txt)
- [fixed 정면 화면](fixed/02-idle-front.png)
- `fixed/before/`: 교체 직전 씬/설정 백업. 기존 noTex FBX도 에셋 폴더에 보존했다.
- 에디터 메뉴 `Tools > EVERFLOW > V2 Puzzles > Replace Player Model With Character Fixed`는 모델/Avatar만 교체하고 기존 컨트롤러/머티리얼을 재사용한다.

## 적용 내용

- 기존 플레이어의 캡슐 **렌더러만** 숨기고 `Character Visual` 자식에 `character_fixed.fbx`를 배치했다. CharacterController(높이 2, 반지름 0.4), 이동 속도, 점프, 충돌, Cinemachine 타깃은 유지했다.
- 동봉된 JPG 아틀라스를 URP Lit 머티리얼에 연결했다. 원본 FBX/이미지 바이트는 변경하지 않았다.
- 모델과 애니메이션을 Humanoid로 임포트했다. 애니메이션 자동 매핑에서 빠지는 왼손을 포함해 모델의 본 매핑을 각 애니메이션 Avatar에 명시했다.
- `Idle / Walk / Run`, `Jump`, `Fall`, `Land`를 연결했다. 별도 Idle 파일이 없어 착지 FBX의 마지막 안정 구간(45–63 프레임)을 대기로 재사용한다. 전용 Idle이 전달되면 교체할 수 있다.
- 루트 모션을 끄고 기존 물리 이동 결과를 읽어 애니메이션만 재생한다. 이동 발판이 플레이어를 운반한 거리는 자신의 이동 속도에서 제외한다.
- 능력 모드와 일반 일시정지에서는 Normal Animator가 세계 시간과 함께 정지한다. 재개 시 Rebind하지 않아 같은 자세에서 이어진다. 리스폰/재시작에는 기존 Teleport 알림을 받아 공중/착지 상태를 초기화한다.
- 착지는 이동 입력을 막지 않는다. 착지 중 이동하면 즉시 이동 블렌드로 돌아간다.

## 조작

기존과 같다. WASD 이동, Space 점프, 마우스 시점, 1/2/3 능력 모드, 좌클릭 적용, Esc 취소/일시정지, R 복귀. 전용 걷기 키는 추가하지 않았다. 기존 속도 6에서는 Run을 사용하며 실제 낮은 속도에서는 Walk로 블렌딩한다.

## 주요 파일

- `Assets/EverflowMap/Character/Character1.controller`: 이동 BlendTree와 점프/낙하/착지 상태.
- `Assets/EverflowMap/Character/Character1.mat`: 제공된 색상 텍스처를 사용하는 머티리얼.
- `Assets/EverflowMap/Runtime/EFV2CharacterAnimator.cs`: 기존 플레이어 상태를 읽는 애니메이션 브리지.
- `Assets/EverflowMap/Runtime/EFMapPlayer.cs`: 읽기 전용 이동 속도/접지/수직 속도/Teleport 버전 신호 추가. 물리 이동식은 유지.
- `Assets/EverflowMap/Editor/EFV2CharacterSetup.cs`: Unity API로 임포터/컨트롤러/대상 씬을 재설정하는 도구. 다시 실행하면 이 캐릭터의 컨트롤러와 `Character Visual`을 재생성하므로 수동 수정 후에는 주의.
- `Assets/EverflowMap/Editor/EFV2CharacterPlayChecks.cs`: 실제 Input System 이벤트를 사용하는 캐릭터 검사와 연속 루트 관찰.
- `Assets/EverflowMap/Editor/EFV2PuzzlePlayChecks.cs`: 자동 검사 중 OS 커서 이벤트가 `.current` 장치나 카메라 Look 액션을 가로채지 않도록 검사 장치를 격리하고 종료 시 액션 장치 필터를 복원한다. 실제 게임 조작/능력 선택·정지·확정 경로는 그대로 사용한다.
- `Assets/character_1/**/*.fbx.meta`: Humanoid, 명시적 매핑, 클립 분할/루프/루트 추출 설정.

## 최초 통합 검증 자료 (2026-11-08)

캐릭터 입력 검사 **13/13**, 연속 BLOOM 경로 **46/46**, 연속 대체 경로 **63/63** 통과. 두 경로 모두 시작부터 물레방아 복원, 정원 도착, 엔딩 Enter 재시작까지 진행했다. Unity에서 실제 프레임 진행과 게임 화면을 확인했다. OS 입력 장치와 합성 검사 장치의 경합으로 생긴 초기 검사 실패는 보존했으며, 카메라 Look 액션까지 검사 장치에 한정한 후 두 경로를 새 게임부터 다시 통과했다.

대체 경로에서 정지 상태로 발판에 운반된 2,639 프레임 동안 잘못된 달리기 신호가 없었고, 능력/일시정지 569 프레임 동안 애니메이션 시간이 진행되지 않았다. 기존 씬 문서 삭제는 0건이며 변경된 기존 문서는 플레이어 프리팹/플레이어 컴포넌트 기록 두 건뿐이다. 원본 5개 FBX와 이미지 파일 해시도 동일하다. 테스트 종료 후 씬 저장 상태, 입력 장치 필터 복원, timeScale=1, 오디오 일시정지 해제, 기존 백그라운드 실행 설정 복원을 확인했다.

- `character-checks.txt`: 실제 Unity Play에서 입력으로 진행한 캐릭터 통합 검사.
- `character-integration.mp4`: 후면/전면, 이동, 점프/착지, 능력 모드 정지/취소, 일시정지, 리스폰/재시작 녹화.
- `01-idle-back.png` ~ `08-restored-idle.png`: 주요 자세의 실제 GameView 캡처.
- `route-animation-audit.txt`: 연속 퍼즐 진행 중 이동 발판 운반과 능력 정지 애니메이션 관찰.
- `../v2-puzzle-validation/runtime-route-bloom.txt`, `runtime-route-alternate.txt`: 기존 연속 입력 검사 결과. 각 파일의 최신 시간과 PASS/FAIL을 확인한다. 퍼즐 상태를 강제 변경하지 않는다. 중간 실패 기록도 `../v2-puzzle-validation/runs/`에 보존한다.

`before/`에는 작업 직전 씬/플레이어 코드/FBX 임포터 설정과 원본 파일 해시를 보관했다. 기존 물길, 능력 타깃 발광, HUD, 퍼즐/공급 조건은 이 작업에서 수정하지 않았다.

## 원본 에셋의 한계

전용 대기/능력 시전/사망 애니메이션은 제공되지 않아 새로 만들지 않았다. 시전 중 전용 포즈로 변경하지 않고 선택 직전의 자세를 정지한다. 모델의 본/웨이트를 편집하지 않고 제공된 fixed FBX를 사용한다.
