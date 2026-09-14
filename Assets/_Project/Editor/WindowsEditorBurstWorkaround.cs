#if UNITY_EDITOR_WIN
using Unity.Burst;
using UnityEngine;

namespace Sprint0.Editor
{
    internal static class WindowsEditorBurstWorkaround
    {
        // This development environment cannot load Burst's generated native DLLs.
        // Relay's DTLS callbacks compile synchronously during StartHost/StartClient;
        // failure there leaves NGO cleaning up a partially initialized session.
        // Run before scene Awake/Start, including when domain reload is disabled.
        // Editor folder + platform guard keep player builds and other editors unaffected.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void UseManagedCallbacks()
        {
            BurstCompiler.Options.EnableBurstCompilation = false;
        }
    }
}
#endif
