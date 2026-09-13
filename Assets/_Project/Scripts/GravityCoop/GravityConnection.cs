using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    public sealed class GravityConnection : MonoBehaviour
    {
        public NetworkManager manager;
        public ushort port = 7777;
        string address = "127.0.0.1";
        string code = "";
        string status = "Host = Gravity operator / Join = Runner";
        bool busy;
        ISession session;
        Font font;

        void Start()
        {
            Application.runInBackground = true;
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientDisconnectCallback += Disconnected;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-gravityHost") >= 0) Direct(true);
            if (Array.IndexOf(args, "-gravityClient") >= 0) Direct(false);
        }

        void OnDestroy()
        {
            if (manager != null) manager.OnClientDisconnectCallback -= Disconnected;
            if (font != null) Destroy(font);
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = manager.ConnectedClientsIds.Count < 2;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = response.Approved ? "" : "This prototype requires exactly two players.";
        }

        void Disconnected(ulong id)
        {
            status = "연결 종료 / Disconnected. " + manager.DisconnectReason;
            // Let the development test finish writing its result before exiting.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gravitySmoke") >= 0) return;
            if (!manager.IsServer && !busy) StartCoroutine(ReloadAfterDisconnect());
        }

        System.Collections.IEnumerator ReloadAfterDisconnect()
        {
            busy = true;
            yield return new WaitForSecondsRealtime(1);
            manager.Shutdown();
            while (manager != null && manager.ShutdownInProgress) yield return null;
            if (manager != null) Destroy(manager.gameObject);
            yield return null;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void Direct(bool host)
        {
            manager.GetComponent<UnityTransport>().SetConnectionData(address.Trim(), port, "0.0.0.0");
            bool started = host ? manager.StartHost() : manager.StartClient();
            status = started ? "연결 대기 중 / Waiting for the other player" : "연결 시작 실패 / Connection failed";
        }

        async void Online(bool host)
        {
            busy = true;
            status = "Relay 연결 중...";
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetProfile("gravity" + System.Diagnostics.Process.GetCurrentProcess().Id));
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                session = host
                    ? await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { Name = "Gravity prototype", MaxPlayers = 2, IsPrivate = true }.WithRelayNetwork())
                    : await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                code = session.Code;
                status = "Relay code: " + code;
            }
            catch (Exception exception)
            {
                status = "Relay 실패: " + exception.Message;
                Debug.LogWarning(status);
                manager.Shutdown();
            }
            finally { busy = false; }
        }

        async void Leave()
        {
            busy = true;
            try { if (session != null) await session.LeaveAsync(); }
            catch (Exception exception) { Debug.LogWarning(exception.Message); }
            finally
            {
                session = null;
                // Reload restores in-scene network objects after NGO shutdown.
                StartCoroutine(ReloadAfterDisconnect());
            }
        }

        void OnGUI()
        {
            GUI.skin.font = font;
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 16;
            GUI.skin.textField.fontSize = 16;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(0.7f, Screen.height / 900f));
            GUILayout.BeginArea(new Rect(16, 8, Screen.width / Mathf.Max(0.7f, Screen.height / 900f) - 32, 240), GUI.skin.box);
            var game = GravityGame.Instance;
            if (game == null || !game.IsSpawned)
            {
                GUILayout.Label("중력 협동 — 1차 프로토타입");
                GUILayout.Label("호스트: 중력 조작자 / 참가자: 내부 플레이어 (역할 고정)");
                GUI.enabled = !busy && !manager.IsListening;
                GUILayout.BeginHorizontal();
                GUILayout.Label("IP", GUILayout.Width(35));
                address = GUILayout.TextField(address, GUILayout.Width(240));
                if (GUILayout.Button("직접 호스트")) Direct(true);
                if (GUILayout.Button("IP 참가")) Direct(false);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                code = GUILayout.TextField(code, GUILayout.Width(280));
                if (GUILayout.Button("온라인 생성")) Online(true);
                if (GUILayout.Button("코드 참가")) Online(false);
                GUILayout.EndHorizontal();
                GUI.enabled = !busy;
                if (manager.IsListening && GUILayout.Button("연결 취소")) Leave();
                GUILayout.Label(status);
            }
            else
            {
                GUILayout.Label($"{game.Puzzle.Value + 1}/5  {game.Current.title}  |  {(game.IsOperator ? "중력 조작자" : "내부 플레이어")}");
                GUILayout.Label($"중력: {game.Direction.Value}  |  대기: {Math.Max(0, game.NextGravityTime.Value - manager.ServerTime.Time):0.0}s  |  리셋: {game.ResetCount.Value}");
                GUILayout.Label(game.Current.objective);
                GUILayout.Label(game.IsOperator ? "방향키: 공간 중력 변경 / R: 구간 리셋" : "A/D: 바닥 좌우 / W/S: 깊이 이동 / Space: 점프 / E: 집기·내려놓기·레버 / R: 리셋");
                if (game.IsOperator)
                {
                    GUILayout.Label($"압력판: {Convert.ToString(game.Current.Pressed.Value, 2)} / 문: {(game.Current.DoorOpen.Value ? "열림" : "닫힘")} / 통로: {(game.Current.routeA == null ? "없음" : game.Current.LeverOn.Value ? "B 배송" : "A 입구")}");
                    GUILayout.BeginHorizontal();
                    GUI.enabled = game.Playing;
                    foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
                        if (GUILayout.Button(direction.ToString())) game.ChangeGravityRpc(direction);
                    GUILayout.EndHorizontal();
                }
                else if (game.Current.lever != null && Vector3.Distance(game.runner.transform.position, game.Current.lever.position) < game.interactionDistance)
                    GUILayout.Label(game.Current.RouteBlocked.Value ? "문에 물체가 걸려 있습니다. 치운 뒤 E로 다시 전환하세요." : $"E: {(game.Current.LeverOn.Value ? "A 입구 열기" : "B 배송 열기")} / 반대쪽 문은 닫힙니다.");
                GUI.enabled = !busy;
                if (!game.Ready.Value) GUILayout.Label("두 번째 플레이어를 기다립니다. 두 명이 연결되면 시작합니다.");
                if (game.Dead.Value) GUILayout.Label("실패 — 현재 퍼즐을 복원합니다.");
                if (game.Finished.Value) GUILayout.Label("5개 퍼즐 완료! 역할을 바꿔 새 연결로 다시 평가해 보세요.");
                if (!string.IsNullOrEmpty(code)) GUILayout.Label("Relay code: " + code);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("현재 퍼즐 리셋")) game.ResetRpc();
                if (GUILayout.Button("연결 종료")) Leave();
                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
