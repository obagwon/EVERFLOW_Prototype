# EVERFLOW · 멈춘 풍차와 계곡의 숨결

계곡의 풍차는 바람을 받아 물을 높은 수로로 올렸다. 수로는 계단처럼 층층이 이어졌고, 물이 닿는 곳마다 정원이 자랐다. 어느 날 풍차의 순환 장치가 멈추고, 장치에 꽂혀 있던 여섯 **숨결 조각**이 바람을 타고 계곡 곳곳으로 흩어졌다. 물길이 마른 곳부터 잎이 시들기 시작한다.

플레이어 곁의 세 구체는 계곡의 작은 정령이다. 식물을 키우는 **BLOOM**, 고정하고 막는 **FREEZE**, 바람을 보내는 **FLOW**가 길을 만드는 힘을 빌려준다. 숨결 조각은 풍차 장치를 깨우는 열쇠다. 수집물은 안쪽에 나선이 보이는 금빛 물방울 형태로 설계해 세 정령 구체와 색·실루엣을 구분한다.

![전망대에서 출발해 풍차와 계곡을 회복하는 네 장면](storyboard.png)

## 시작에서 엔딩까지

| 시점 | 플레이어가 보고 하는 일 | 플레이어가 알아차리는 것 |
| --- | --- | --- |
| **전망대** | 낮아진 수위, 멈춘 풍차 날개, 갈라진 수로를 한 시야에 담는다. 아래 첫 코스 방향에서 금빛 조각이 한 번 반짝인다. BLOOM으로 뿌리를 키워 전망대를 내려간다. | 풍차가 멈춘 뒤 물길이 끊겼다. 반짝이는 조각을 찾아야 풍차에 들어갈 수 있다. |
| **외부 코스 여섯 곳** | B 수조, C 바깥섬, D 씨앗 골짜기, E 상부 착지턱, W 정비 발코니, 서쪽 채광실에서 퍼즐을 풀어 조각을 하나씩 얻는다. 첫 조각을 얻었을 때 풍차 문 위 여섯 홈 중 하나가 켜지고, 주변 수로나 식물에 짧은 반응이 일어난다. | 퍼즐을 풀 때마다 작은 변화가 먼저 생긴다. 풍차 문이 세계의 큰 변화로 이어진다. |
| **풍차 입구** | 여섯 번째 조각을 얻으면 문 위 홈이 모두 켜지고 문이 열린다. 안에서 물이 새는 소리와 멈춘 톱니가 보인다. | 조각을 모으는 일은 풍차 안에서 장치를 고치기 위한 준비였다. |
| **풍차 내부 퍼즐** | 조각을 끼우면 내부 장치의 여섯 홈에 빛이 들어온다. **BLOOM**으로 뿌리를 키워 끊긴 구동축을 연결한다. **FREEZE**로 수로의 균열을 잠시 막아 수위를 맞춘다. 마지막에 **FLOW**로 바람을 풍차 날개 쪽에 보내 장치를 돌린다. | 외부에서 익힌 세 능력을 순환 장치 하나에 사용한다. 풍차가 돌아가야 물이 계곡 위쪽까지 다시 올라간다. |
| **회복** | 시작 전망대와 같은 구도로 돌아온다. 풍차 날개가 돌고 위쪽 수조에서 낮은 계곡 순으로 물이 이어지며, 수로 주변 새싹이 핀다. | 플레이어의 조작이 멈춰 있던 계곡을 되살렸다는 것을 한눈에 확인한다. |

## 게임에 넣을 안내 문구

- **첫 목표** — “풍차가 멈추자 계곡의 물길도 끊겼다. 숨결 조각 여섯 개를 모아 풍차를 열자.”
- **첫 조각** — “숨결 조각 1/6 · 풍차에 첫 번째 불빛이 켜졌다.”
- **여섯 번째 조각** — “풍차 문이 열렸다. 안에서 순환 장치를 수리하자.”
- **내부 첫 단서** — “축은 끊어졌고 물은 샌다. 바람을 보내기 전에 물길을 연결하자.”
- **회복 후** — “풍차가 돌아간다. 다시 물이 흐른다.”

LKH 씬에는 수집물 이름, 첫 목표, 모은 뒤 풍차 안의 장치를 고치는 목표를 표시했다. 현재 플레이 완료 시점은 풍차 입구이며, 위의 내부 장치·문 홈 연출·계곡 회복은 후속 구현용 설계다.

## 주인공 주변 능력 구체

초록 새싹, 파란 눈꽃, 주황 바람 띠가 작은 크기에서도 읽히는 구성을 기준으로 했다. 모두 투명 배경 PNG다.

| BLOOM | FREEZE | FLOW |
| --- | --- | --- |
| ![BLOOM 새싹 구체](bloom-orb.png) | ![FREEZE 얼음 구체](freeze-orb.png) | ![FLOW 바람 구체](flow-orb.png) |
| 옥빛 구체 속에 새싹이 빛나고 잎맥이 겉면을 감싼다. | 푸른 얼음 속에 결정이 있고 작은 얼음 조각이 가장자리를 이룬다. | 호박빛 구체를 두 줄의 바람 띠가 감는다. |

## 이미지 생성 프롬프트

스토리보드와 구체는 내장 ImageGen으로 생성했다.

### Storyboard

Use case illustration-story. A landscape 2x2 game concept storyboard, four sequential cinematic panels for the original Korean fantasy puzzle game EVERFLOW, each separated by a thin charcoal green gutter. Gorgeous crisp stylized 3D storybook game environment, hand-painted textures, a coherent geography with stepped mossy limestone cliffs, an ancient warm wood four-sail windmill perched on the left ridge, winding pool terraces, stone paths. Same tiny chibi character across all four panels, in a soft turquoise blue rounded hood and small cream scarf, a shadowed face slit with two cyan eyes, and exactly three small green blue amber floating magical companion spheres. Top left: opening from behind the hero on a lookout, warm windmill motionless, calm dry central channels, pale dormant plants, faint snaking paths away from the lookout. Top right: the hero solves a short mossy traversal puzzle, giant leaf platforms over water, reaching one small artifact called a Breath Shard, a luminous warm gold pebble shaped like a teardrop of amber with a translucent spiral inside; distant routes and windmill visible. Bottom left: interior of the same windmill, six amber teardrop shards seated in a ring of six sockets in a big brass wooden mechanism with a small water channel and a root-driven gear, the hero redirects a curl of amber wind to a vane, readable quiet moment before activation. Bottom right: wide view from the same lookout as the first panel, turning windmill sails, turquoise running water spilling down terraces, fresh plant growth and sunlit mist, the turquoise hero watches. Deep pine green, muted warm stone, turquoise water, small amber guiding lights. Clear directional storytelling, inviting high-end contemporary game concept art. No text, letters, watermark, flags, speech balloons, borders beyond the thin panel gutters, or star-shaped collectibles.

### BLOOM

Use case stylized-concept. Original cozy stylized 3D adventure game concept, hand-painted semi-matte materials, small readability, luminous magical core, warm woodland fantasy. Match the exact framing for a coordinated sprite set: square PNG, precisely one centered spherical object, sphere takes 68–72 percent of square width, front view with slight overhead, consistent quiet rounded silhouette, rim lighting top left, centered fully visible, generous clean alpha margins. Absolutely no text, typography, shadow underneath, character, ground, pedestal, solid backdrop or HUD frame. True transparent background while retaining opaque readable colorful object and clean isolated fine effects. BLOOM companion orb, rich emerald and yellow-green. The orb is a jade and translucent sap seed with a warm gold-green sprout suspended at its center, two broad petal leaves curving closely along the upper shell and delicate botanical radial vein details. A few small detached green sparks very close to the surface. Distinct leaf silhouette while still an orb, avoid huge tendrils, no other shapes outside the central object.

### FREEZE

Use case stylized-concept. Original cozy stylized 3D adventure game concept, hand-painted semi-matte materials, small readability, luminous magical core, warm woodland fantasy. Match the exact framing for a coordinated sprite set: square PNG, precisely one centered spherical object, sphere takes 68–72 percent of square width, front view with slight overhead, consistent quiet rounded silhouette, rim lighting top left, centered fully visible, generous clean alpha margins. Absolutely no text, typography, shadow underneath, character, ground, pedestal, solid backdrop or HUD frame. True transparent background while retaining opaque readable colorful object and clean isolated fine effects. FREEZE companion orb, rounded condensed glacial turquoise sphere, an icy six-branched snow-crystal frozen inside the spherical core, pale sky blue inner light, very slightly faceted shell like frosted glass, two or three short ice flakes hugging the edge. Distinct ice silhouette while still an orb, no large jagged spikes, no other shapes outside the central object.

### FLOW

Use case stylized-concept. Original cozy stylized 3D adventure game concept, hand-painted semi-matte materials, small readability, luminous magical core, warm woodland fantasy. Match the exact framing for a coordinated sprite set: square PNG, precisely one centered spherical object, sphere takes 68–72 percent of square width, front view with slight overhead, consistent quiet rounded silhouette, rim lighting top left, centered fully visible, generous clean alpha margins. Absolutely no text, typography, shadow underneath, character, ground, pedestal, solid backdrop or HUD frame. True transparent background while retaining opaque readable colorful object and clean isolated fine effects. FLOW companion orb, warm golden amber and tangerine. A dark orange translucent amber center with two broad bright cream and gold bands swirling around the sphere, visible curving airflow lanes that convey a gentle gust, a few exquisitely simple golden specks close to the rim. Distinct wind curl silhouette while still an orb, no gear teeth, no lightning bolts, no other shapes outside the central object.
