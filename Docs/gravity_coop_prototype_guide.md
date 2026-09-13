# 중력 협동 1차 프로토타입 실행 가이드

## 기준과 구성

`gravity_coop_implementation_spec.md`를 구현 기준으로, 아이디어 문서를 역할과 재미의 기준으로 사용했다. 기존 `SampleScene`, 4인 샘플 코드 및 패키지는 유지한다. 새 `GravityPrototype` 씬에서 기존 Input System, Netcode for GameObjects, Unity Transport, Multiplayer Services/Relay를 사용한다.

호스트가 중력 조작자, 접속자가 내부 플레이어다. 두 명이 연결되면 자동 시작하며 플레이 중 역할은 바뀌지 않는다. 역할을 바꾸려면 연결을 종료하고 상대방이 호스트가 된다.

## 실행

1. Unity 6000.3.10f1에서 `Assets/Scenes/GravityPrototype.unity`를 연다.
2. Editor와 `Builds/GravityPrototype/GravityPrototype.exe`, 또는 실행 파일 두 개를 실행한다.
3. 같은 PC: 한쪽에서 **직접 호스트**, 다른 쪽은 IP `127.0.0.1`로 **IP 참가**.
4. 같은 네트워크: 참가자가 호스트의 LAN IPv4 주소를 입력한다. 직접 접속 포트는 UDP 7777이다.
5. 인터넷: **온라인 생성** 후 표시되는 Relay 코드를 상대방에게 전달하고 **코드 참가**한다. 기존 Unity 프로젝트에 연결된 Multiplayer Services/Relay와 익명 인증이 활성화되어 있어야 한다. 계정 입력·방 목록·매치메이킹 UI는 없다.

씬과 빌드 순서는 생성되어 있다. 수동 오브젝트 배치나 Inspector 참조 연결은 필요 없다. 씬을 기본 배치로 다시 만들려면 **Tools > Sprint 0 > Build Gravity Prototype**을 실행한다. 이 명령은 해당 프로토타입 씬의 수동 변경을 기본값으로 다시 생성하므로, 직접 조정한 씬을 보존하려면 먼저 별도로 저장한다.

## 조작

| 역할 | 조작 |
|---|---|
| 중력 조작자 | 방향키 또는 화면의 Down/Up/Left/Right 버튼 |
| 내부 플레이어 | A/D: 현재 바닥 기준 좌우, W/S: 방의 깊이 이동, Space: 점프 |
| 내부 플레이어 | E: 가까운 상자 집기/내려놓기 또는 레버 토글. 들고 있으면 내려놓기가 우선 |
| 공통 | R 또는 화면 버튼: 현재 퍼즐 초기화 |

청록색 작은 출구는 **방 뒤쪽**에 있다. 중력만으로는 깊이 방향으로 이동하지 않으므로 내부 플레이어가 W로 출구에 들어가야 한다. 주황색은 중력을 받는 상자, 노란색은 압력판, 보라색은 레버, 파란색은 문, 붉은색은 위험 구역이다. 활성 압력판과 레버는 녹색이 된다.

## 5개 퍼즐과 참고 풀이

정답 방향을 HUD에 표시하지 않는다. 다음 내용은 진행이 막혔을 때 테스트용으로 참고한다.

1. **중력 이동:** 위쪽 중력으로 천장에 착지하고 출구 쪽으로 이동한 뒤 W로 진입한다.
2. **상자와 버튼:** 바닥에서 상자를 천장 압력판 아래에 운반해 내려놓는다. 위쪽 중력으로 상자를 압력판에 붙이고 내부 플레이어가 출구로 이동한다. 누르는 물체가 떠나면 문이 다시 닫힌다.
3. **중력 순서:** 처음부터 위로 바꾸면 상자가 선반 아래에 막힌다. 오른쪽으로 선반 끝을 돌아 위로 보낸 후 왼쪽, 위쪽 중력으로 천장 왼쪽 압력판에 붙인다. 내부 플레이어는 출구까지 이동한다.
4. **타이밍:** 레이저가 꺼지는 구간에 위쪽 중력을 적용해 레이저 면을 통과한다. 잘못된 타이밍에 접촉하면 이 퍼즐만 리셋된다.
5. **협동 종합:** 두 상자를 각 천장 압력판 아래에 배치하고, 조작자는 상자와 플레이어가 함께 레이저를 안전하게 통과할 순간을 판단한다. 천장에서 내부 플레이어가 레버를 E로 켠 뒤 출구에 들어간다. 이 구간은 상자가 레이저에 닿아도 초기화된다.

## 조절 지점

- `Gravity Game`: 중력 세기, 변경 대기시간, 리스폰 지연, 상호작용 거리, 카메라 회전 속도/거리.
- `Runner`: 이동 속도, 점프 속도, 공중 조작 비율, 가속도, 시각 모델 회전 속도.
- 각 상자의 `Rigidbody`: 질량. `GravityBody`: 중력 영향 여부, 운반 가능 여부.
- `Assets/_Project/Materials/Gravity/*Friction.physicMaterial`: 마찰.
- 각 `Puzzle`의 `GravityPuzzle`: 레이저 주기/켜짐 시간, 상자 위험 판정. 자식 Collider/Transform으로 판정 크기와 퍼즐 배치를 조정한다.

## 동기화와 임시 결정

- 물리와 상호작용 판정은 호스트만 계산한다. 클라이언트 Rigidbody는 kinematic이며 위치/회전은 서버 권한 NetworkTransform으로 받는다.
- 중력, 구간, 압력판, 레버, 문, 레이저, 운반 상태, 사망, 리셋, 완료는 서버 쓰기 NetworkVariable이다. 클라이언트는 입력 RPC만 보낸다. RPC 발신 역할·거리·가림·쿨타임을 서버에서 검사한다.
- 4방향은 월드 XY축, 방 깊이는 Z축이다. 공간은 3D이고 내부 플레이어는 표면의 두 축으로 움직인다.
- 작은 구간 5개를 한 씬에 배치하고 완료하면 다음 체크포인트로 이동한다. 현재 구간만 물리를 진행한다.
- 기본값은 중력 18, 쿨타임 0.5초, 이동 5, 점프 7, 공중 조작 0.45, 리스폰 0.8초다.
- 캐릭터는 캡슐 모양 Primitive와 구형 충돌체를 사용한다. 중력 회전 순간 충돌체가 벽에 끼는 일을 줄이기 위한 프로토타입 선택이다.
- 관찰자는 회전하지 않는 전체 시점, 내부 플레이어는 중력에 따라 부드럽게 회전하는 제한된 3인칭 추적 시점이다. 마우스 자유 회전은 없다.
- 음성 채팅은 게임에 추가하지 않았다. 서로 대화할 수 있는 환경에서 평가한다.

## 자동 검증 실행

개발 빌드 전용 `-gravitySmoke` 옵션이 실제 두 프로세스의 입력 RPC와 PhysX를 사용한다. 일반 실행에는 테스트 입력이 개입하지 않는다.

```powershell
Start-Process ./Builds/GravityPrototype/GravityPrototype.exe -ArgumentList '-batchmode -nographics -gravityHost -gravitySmoke -logFile gravity-host-test.log' -WindowStyle Hidden
Start-Process ./Builds/GravityPrototype/GravityPrototype.exe -ArgumentList '-batchmode -nographics -gravityClient -gravitySmoke -logFile gravity-client-test.log' -WindowStyle Hidden
```

초기 시스템 검증에서는 테스트 배치를 위해 서버에서 위치/구간을 설정한다. 이후 5개 풀이에서는 클라이언트의 이동·집기·레버 RPC와 호스트의 중력 변경으로 진행한다. 따라서 자동 통과는 완성도나 두 사람의 재미를 증명하지 않는다. 서로 역할을 바꿔 플레이하고 조작자가 먼저 경로·상자·위험을 판단했는지, 내부 플레이어가 위치와 준비 상태를 전달했는지 함께 평가한다.
