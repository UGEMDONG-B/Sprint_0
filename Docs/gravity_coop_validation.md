# 중력 협동 퍼즐 재설계 검증 기록

기준: `gravity_coop_puzzle_redesign.md`에 따른 새 5개 퍼즐. Unity 6000.3.10f1 / Windows / NGO 2.13.2 / Input System 1.18.0.

## 최종 결과

- 실제 Unity C# 컴파일과 Windows 개발 빌드 성공. 최종 로그에 C# 오류/경고 없음.
- 생성 씬의 392개 컴포넌트와 필수 참조 검사 통과. TextMesh 폰트도 명시적으로 검사.
- 별도 호스트/클라이언트 프로세스에서 5개 새 퍼즐 연속 완료.
- 호스트 `HOST PASS 445 checks`, 클라이언트 최종 구간·문 상태 `CLIENT PASS`.
- 최종 실행 로그에 MissingReferenceException, NullReferenceException, 테스트 실패 없음. 두 테스트 프로세스 종료 확인.

445개 검사는 컴포넌트 참조 검사와 게임플레이 검사 합계다. 초기 배치의 299개 검사 결과와 구분한다.

| 검사 | 실제 확인한 내용 |
|---|---|
| 4방향 중력·플레이어 | 네 방향 낙하·착지·이동·점프. 실제 클라이언트 입력 RPC 사용 |
| 상자 | 네 방향 낙하, 집기·운반·내려놓기 |
| 권한 | 참가자의 중력 변경 요청과 조작자의 상자 집기 요청 거절 |
| 쿨타임·리셋 | 연속 중력 변경 차단, 사망·리스폰, 물체와 상태 초기화 |
| 분기 안전 | 닫힐 문 안에 상자가 있으면 전환 거절 |
| 분기 순서 | 처음부터 B를 열면 A에서 막힘. A를 유지하면 B 배송이 막힘. 리셋 시 A 열림/B 닫힘 복원 |
| 퍼즐 1 | 앞쪽에서 위로 가면 선반에 막힘 확인. 뒤쪽 출발 → 천장 → 깊이 전환 → 출구 완료 |
| 퍼즐 2 | P 홈에 상자 배치 → 깊이 우회 → 오른쪽 중력 → 출구 완료 |
| 보관 홈 | 상자 마찰을 0으로 바꾼 테스트에서도 중력 전환 후 압력판 유지. Rigidbody는 dynamic 상태 |
| 퍼즐 3 | 상자를 A로 올린 후 원격 레버 입력으로 B를 열고 Q에 배송하여 완료 |
| 퍼즐 4 | 점프 후 오른쪽 중력으로 첫 벽에 착지, 벽에서 점프 후 위쪽 중력으로 높은 받침에 착지하여 완료 |
| 퍼즐 5 | 첫 상자 준비 → 우회 → 두 번째 준비 → P 보관/A 입구 → B 전환 → 환승 → P/Q 동시 유지 및 출구 완료 |
| 역할별 시야 | 다섯 방을 오프스크린 렌더링하여 앞뒤 구조, 문자 표식, 문 초기 상태 확인 |

시스템/부정 경로 검사의 초기 배치는 서버 위치·상태 설정을 사용한다. 이후 5개 연속 풀이에서는 위치나 완료 상태를 강제로 바꾸지 않고 클라이언트 이동·점프·상호작용 RPC와 호스트 중력 변경으로 진행한다. 단, 퍼즐 2의 보관 검사에서 상자 마찰을 잠시 0으로 바꿨다가 복원한다.

## 구현 중 발견하고 수정한 사항

- 레버를 입구 바로 옆의 안전한 위치로 옮겨, 열린 A 입구에 플레이어가 빨려 들어가기 전에 B로 전환할 수 있게 했다.
- 상자를 놓은 뒤 같은 통로로 걸으면 다시 밀려나는 정상 물리 반응을 확인했다. 자동 풀이도 깊이 통로로 우회하도록 수정했다.
- 깊이 방향으로 이동한 뒤 집으면 마지막 이동 방향을 따라 상자를 벽으로 당기던 문제를 수정했다. 집는 순간 선택한 상자 쪽으로 운반 방향을 맞춘다.
- 운반 위치에 조절 가능한 바닥 여유 높이 0.3을 추가했다.
- 표식 폰트 참조를 연결하고 검증 항목에 포함했다. 고정 관찰 시점에 높이 차를 주고 A/B·P/Q 표식을 확대했다.
- 닫힌 출구 문과 출구 표시가 같은 면에서 겹치지 않도록 문 크기를 조정했다.

## 설계와 실제 배치의 차이

- 종합 퍼즐은 두 상자를 먼저 준비한 뒤 함께 위로 보내는 풀이도 허용한다. 숨은 순서·점프 횟수 조건을 두지 않았다.
- 공중 환승은 넓은 받침과 회수 바닥을 사용한다. 다른 협동 우회 풀이를 전부 막았다는 의미는 아니다.
- 새 고정 시점은 작은 기울기로 깊이를 읽게 하며, 중력에 따라 관찰 화면이 회전하지 않는다.

## 남은 사람 검증

- 두 사람이 역할을 교대했을 때의 재미, 대화 빈도, 난이도와 카메라 체감은 직접 확인해야 한다. 자동 성공은 재미를 증명하지 않는다.
- 각 역할이 계획을 제안했는지, 준비 위치를 골랐는지, 한 사람이 장시간 기다렸는지 관찰한다.
- 자동 네트워크 검사는 localhost 직접 접속이다. 서로 다른 인터넷 회선에서의 Relay 접속, 높은 지연·패킷 손실 조건은 미검증이다.
- 내부 플레이어는 호스트 권한 이동이며 클라이언트 예측이 없어 높은 지연에서 조작 지연을 느낄 수 있다. 설계의 넓은 타이밍 허용 구간은 실제 2인 테스트로 추가 조정한다.
- 시각 검증은 저장 씬의 카메라 렌더를 사용했다. 일반 실행에서 HUD 클릭과 가독성을 직접 확인할 수 있다.

## 환경 / 재실행

이 Windows 환경의 Burst 캐시 DLL 로드 제한 때문에 배치 실행에는 `--burst-disable-compilation`을 사용했다. Inspector의 게임 규칙이나 새 능력을 바꾸는 설정은 아니다. Editor에서 같은 DLL 오류가 발생하면 Jobs/Burst의 Enable Compilation을 끄고 테스트할 수 있다.

### 2026-09-14: 일반 에디터 Relay 시작 실패 우회

Editor.log의 최초 실패는 `UnityTLSCallbacks.GetSendCallbackPtr()`에서 발생한
`Burst failed to compile the function pointer ... SendCallback`이었다.
이후 `StartHost()`의 실패 정리 과정에서 `Trying to destroy object 0` 경고와
세션 시작 실패 예외가 연쇄 발생했다.

`Assets/_Project/Editor/WindowsEditorBurstWorkaround.cs`에서 Windows 에디터의
`SubsystemRegistration` 시점에 Burst 컴파일을 비활성화한다. 씬의 Awake/Start와
Relay 드라이버 생성 전에 적용되며 도메인 재로드를 끈 Play 진입에도 실행된다.
이 환경에서는 에디터 Play의 Burst 성능 최적화를 포기하고 관리 코드로 실행한다.
플레이어 빌드 및 다른 OS의 에디터에는 포함되지 않는다. 네이티브 DLL 로드 문제가
해결되면 이 우회 파일을 제거하고 Burst를 다시 활성화할 수 있다.

설정 API: [Unity Burst EnableBurstCompilation](https://docs.unity3d.com/Packages/com.unity.burst@1.8/api/Unity.Burst.BurstCompilerOptions.EnableBurstCompilation.html).

검증: Unity 6000.3.10f1에서 컴파일 성공. Burst를 켠 뒤 빈 씬의 실제 Play 진입으로
우회 설정의 자동 실행을 확인했고, 설치된 Transport 패키지의 TLS 송신·수신·로그
콜백 포인터 생성 3건이 모두 통과했다. 기록은
`Logs/burst-relay-playmode-validation.log`, 임시 검증 소스는
`Logs/BurstRelayValidation.cs`에 보관했다. 실제 온라인 방 생성·2인 Relay 접속은
이번 검증에서 수행하지 않았다.

- `Logs/gravity-redesign-build.log`: 최종 컴파일·빌드 기록
- `Logs/gravity-redesign-host.log`, `Logs/gravity-redesign-client.log`: 최종 5개 연속 플레이 기록
- `Logs/gravity-redesign-preview.log`: 최종 씬 렌더 및 참조 검사
- `Logs/gravity-redesign-puzzle-1.png` ~ `gravity-redesign-puzzle-5.png`: 각 방 관찰 시점
- `Builds/GravityPrototype/GravityPrototype.exe`: 갱신된 Windows 실행 파일

재실행 명령과 퍼즐 참고 풀이는 `gravity_coop_prototype_guide.md`에 있다. 호스트에 `-gravityFinal`을 함께 지정하면 개발 테스트에서 종합 퍼즐만 검사한다. 게임의 일반 UI에는 구간 건너뛰기를 추가하지 않았다.

기존 SampleScene, 패키지 및 네트워크 연결 흐름은 유지했다. Unity가 빌드 중 자동으로 바꾼 무관한 프로젝트·렌더 설정은 원래대로 복원했다. Logs/Builds는 기존 .gitignore에 따라 버전 관리되지 않는다.

## 장치 가독성 보강 검증 (2026-09-13)

- `Logs/gravity-hints-final-build.log`: Windows 개발 빌드 성공, 종료 코드 0. 472개 컴포넌트에서 Missing Script/Reference 없음.
- 이번 변경은 설명용 컴포넌트, HUD, 충돌체 없는 문자 표식이다. 기존 이동/중력/퍼즐 판정 코드는 수정하지 않았다.
- 기존 5개 퍼즐의 두 프로세스 자동 통과 결과는 앞 절을 참조한다. 이번 표시 변경 후 전체 풀이 테스트는 다시 실행하지 않았다.
- 조작자의 마우스 검사와 내부 플레이어의 근접 안내가 실제 플레이에서 충분히 잘 읽히는지는 두 역할로 확인이 필요하다. 저장 씬 렌더에는 런타임 HUD가 포함되지 않는다.

## 자유 3인칭 카메라 (2026-09-13)

- 마우스 회전, 상하 각도 제한, 휠 거리 조절, Esc 커서 해제/화면 클릭 재진입을 구현했다. 커서가 풀린 동안 이동과 상호작용은 멈춘다.
- 카메라 기준 이동을 기존 서버 입력 좌표로 변환한다. 물리/역할 권한 검증은 유지한다.
- `Logs/gravity-camera-host.log`: HOST PASS 533 checks. 네 중력 방향마다 시점 90도·180도 기준 전진, 착지·점프와 5개 퍼즐 전체 풀이를 검증했다. `Logs/gravity-camera-client.log`: CLIENT PASS.
- 카메라는 벽 앞에서 거리를 줄이며, 중력 전환에도 검사 시작점이 벽을 넘지 않도록 플레이어 중심에서 충돌을 검사한다. 투명한 전면 경계는 카메라만 통과하도록 임시 결정했다.
- 자동 입력 테스트는 실제 마우스 잠금과 시각적인 카메라 사용감을 검증하지 않는다. 좁은 공간의 거리 변화와 감도는 직접 플레이로 확인해야 한다.
- 최종 카메라 충돌 시작점 보정 후 `Logs/gravity-camera-final-build.log`에서 빌드 성공 및 472개 컴포넌트 참조 검사를 재확인했다.

## SampleScene 공통 UI / 접속 흐름 재사용 (2026-09-13)

- `GravityPrototypeBuilder`는 SampleScene을 저장하지 않고 기존 Interface, MultiplayerGame, EventSystem 루트를 가져온다. 메인·서버 검색·로비·설정·서버 종료 화면과 버튼 참조를 유지한다.
- 접속은 기존 `MultiplayerGameController`의 공개 방 생성/조회/ID 참가와 Relay를 사용한다. 별도 IP/코드 UI와 중복 서비스 접속 코드를 삭제했으며 `GravityConnection`은 게임 표시만 담당하는 `GravityHud`로 이름을 바꿨다(스크립트 GUID 유지).
- 게임별 설정은 2인 정원/2인 시작 조건, 게임 종류 구분, 커서 정책, 세션 종료 후 씬 복원이다. SampleScene의 기존 기본 설정은 유지한다.
- `Logs/gravity-shared-ui-validation.log`: 빌드 성공, 생성 씬 614개 컴포넌트 Missing Script/Reference 없음. 메인/검색/로비/설정 오프스크린 렌더 확인. 프리뷰는 저장하지 않았다.
- `Logs/gravity-shared-ui-host.log`: HOST PASS 677 checks. 두 명 접속 후에도 로비에서는 Ready=false이며 물리가 정지하는 검사, 네 방향 이동/카메라 입력, 5개 퍼즐 완료 통과. 클라이언트도 CLIENT PASS.
- 물리 자동 테스트는 공통 컨트롤러의 시작 상태 알림을 모의하고 로컬 전송을 사용한다. 실제 UGS 방 생성·검색·참가·호스트 시작·나가기·재접속의 온라인 왕복 검증을 대체하지 않는다. 이 온라인 경로는 이번 작업에서 실행 검증하지 않았다.
- 수동 확인: 기존 서비스 설정으로 방 생성 → 다른 플레이어가 방 참가/공개 목록 선택 → 2인 로비 → 호스트 시작 → Esc 설정 → 방 나가기 → 다시 생성/참가. 호스트 종료 시 참가자의 서버 종료 화면도 확인한다.

## 상하단 HUD 제거 (2026-09-13)

양쪽 역할의 GravityHud를 제거하고 카메라 viewport를 전체 화면으로 변경했다. gravity-clean-screen-build.log: 빌드 성공, 종료 코드 0, 613개 컴포넌트 Missing Script/Reference 없음. 표시 변경이므로 전체 퍼즐 자동 테스트는 재실행하지 않았다.

## 협동 실험 스테이지 6~8 추가 (2026-09-16)

- 기존 1~5번 뒤에 받고 다시 보내기, 둘 다 준비됐어?, 보관하고 길 열기를 연결했다. 기존 상자·압력판·A/B 스위치와 문·고정 벽만 조합했으며 신규 런타임 퍼즐 규칙은 추가하지 않았다.
- 6번은 천장 스위치까지 이동한 후 배송을 요청한다. 7번은 두 상자의 발사 위치와 플레이어의 뒤쪽 경로를 준비한다. 8번은 P 보관을 유지하면서 천장 스위치로 B를 열고 Q를 배송한다. 6/8번 선반 뒤에는 귀환 통로가 있어 A가 닫혀도 출구로 내려갈 수 있다.
- `Logs/gravity-coop-labs-build.log`: Unity 6000.3.10f1 Windows 개발 빌드 성공, 종료 코드 0. 8개 방의 컴포넌트 971개에서 누락된 스크립트/참조 없음.
- `Logs/gravity-eight-host.log`: HOST PASS 1047 checks. 네 방향 이동·착지·점프, 역할 권한, 리셋, 통로 끼임 방지, 기존 1~5번과 신규 6~8번 전체 연속 클리어 통과. 신규 방 풀이 중 위치 강제 변경 없이 클라이언트 이동/상호작용 RPC와 호스트 중력 입력을 사용했다.
- `Logs/gravity-eight-client.log`: CLIENT PASS. 마지막 8번의 출구 개방과 전체 완료 상태 동기화 확인.
- `Logs/gravity-coop-labs-preview.log`: 최종 저장 씬 렌더 성공. `Logs/gravity-redesign-puzzle-6.png`~`gravity-redesign-puzzle-8.png`에서 세 방의 스위치·문·표식 배치를 시각 확인했다.
- 자동 테스트에서 온라인 컨트롤러를 비활성화하면 승인 콜백도 등록되지 않아 접속이 멈추던 문제를 수정했다. 개발 테스트의 직접 localhost 접속에만 ConnectionApproval을 끄며, 일반 로비 승인 동작은 유지한다. 클라이언트의 마지막 구간 검사를 퍼즐 배열 길이 기준으로 바꾸고 `-gravityCoopLabs`로 신규 세 방만 검사할 수 있게 했다.
- 실제 두 사람의 재미, 우회 풀이, 시점 조작감과 인터넷 Relay 왕복은 이번 자동 검사에서 검증하지 않았다. 역할 교대와 기존 3번 대비 평가 항목은 `gravity_coop_prototype_guide.md`에 정리했다.
- 씬과 `Builds/GravityPrototype/GravityPrototype.exe`를 갱신했다. 빌드가 자동으로 변경한 무관한 렌더/프로젝트 설정은 작업 전 내용으로 복원했다.

## 3번 위치 전달 부담 완화 (2026-09-19)

- 3번만 A 투입구 폭을 2.4 → 4.8로 확장하고 충돌체 없는 하늘색 배치 구역(3.2 × 4), A/B 색상 연결, 고정 FRONT/REAR 명칭을 추가했다. 스위치는 넓어진 입구 왼쪽의 막힌 선반 아래로 옮겼다.
- `GravityLandmark`는 게임 카메라 갱신 후 각 로컬 카메라를 향해 TextMesh만 회전시킨다. 네트워크 물리나 퍼즐 정답 조건은 바꾸지 않는다. 3번의 기존 상자/출구 표식에도 적용했다.
- `Logs/gravity-communication-build.log`: Unity 6000.3.10f1 Windows 개발 빌드 성공, 종료 코드 0. 8개 방, 993개 컴포넌트 참조 검사 통과.
- `Logs/gravity-communication-host.log`: HOST PASS 1071 checks. A 중심에서 x ±1.55, z ±1.95인 두 위치에 상자를 배치한 물리 검사에서 투입구 통과 확인. 이어 초기 상태에서 실제 클라이언트 운반/스위치 RPC로 3번을 클리어하고 전체 8개 연속 진행 통과.
- `Logs/gravity-communication-client.log`: CLIENT PASS, 8번 최종 완료/출구 상태 동기화 통과.
- `Logs/gravity-communication-preview.log`: 저장 씬 렌더 성공. `gravity-communication-operator.png`, `gravity-communication-runner.png`, `gravity-communication-runner-rear.png`에서 전체 시점과 내부 앞뒤 시점의 배치 구역 및 표식 가독성을 확인했다. 이는 실제 마우스 조작감이나 모든 카메라 각도의 시인성을 보장하는 검사는 아니다.
- 씬과 개발 실행 파일 갱신. Unity가 생성한 무관한 프로젝트/렌더 설정 변경은 복원했다. 위치 미세 지시 감소와 판단 주도권 변화는 동일한 두 사람의 재플레이로 검증한다.

## 내부 플레이어 1인칭 시점 (2026-09-19)

- GravityPrototype 내부 플레이어 카메라를 눈높이 1인칭으로 변경했다. 눈 위치는 플레이어 중심에서 중력 기준 위쪽 0.25이며 충돌체 안쪽으로 제한한다. 마우스 상하 회전은 -85~85도, 초기 시선은 수평이다. 휠 거리 조절을 제거했다.
- 자기 캐릭터의 렌더러는 내부 플레이어에서만 숨기며 조작자 전체 시점에서는 계속 표시한다. 중력 기준축의 부드러운 회전, 시선 기준 이동, 기존 커서/설정 조작을 유지했다.
- `Logs/gravity-first-person-build.log`: Unity 6000.3.10f1 개발 빌드 성공, 8개 퍼즐과 993개 컴포넌트 참조 검사 통과. 실행 파일은 `Builds/GravityPrototype/GravityPrototype.exe`에 갱신했다.
- `Logs/gravity-first-person-host.log`: HOST PASS 1074 checks. 네 중력 방향 이동/점프, 조작자 카메라/캐릭터 표시, 8개 퍼즐 연속 완료 통과.
- `Logs/gravity-first-person-client.log`: CLIENT PASS. 1인칭 눈 위치, 원근 카메라, 로컬 몸 숨김과 최종 완료 동기화 검사 통과.
- 로컬 두 프로세스 자동 검사이며 실제 마우스 조작감, 시각적 편안함, 인터넷 Relay 접속은 이번 검사 범위에 포함되지 않는다.
