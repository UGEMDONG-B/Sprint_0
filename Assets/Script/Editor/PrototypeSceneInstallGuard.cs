using UnityEditor;
using UnityEngine;

namespace Sprint0.Prototype.Editor
{
    internal static class PrototypeSceneInstallGuard
    {
        const string Step8SeatPrefabPath = "Assets/_Project/Prefabs/TentaclePlayerSeat.prefab";

        public static bool IsStep8Installed =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Step8SeatPrefabPath) != null;
    }
}
