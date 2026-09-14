using UnityEngine;

namespace Sprint0.GravityCoop
{
    // Presentation only: inspecting a device never activates it.
    public sealed class GravityHint : MonoBehaviour
    {
        public string heading;
        [TextArea] public string explanation;
        public Collider target;
        public int plateIndex = -1;

        public string Describe(GravityPuzzle puzzle)
        {
            string state = "";
            if (plateIndex >= 0)
                state = (puzzle.Pressed.Value & (1 << plateIndex)) != 0 ? "현재 눌림 · 초록" : "현재 비어 있음 · 노랑";
            else if (target == puzzle.routeA || target == puzzle.routeB || transform == puzzle.lever)
                state = puzzle.RouteBlocked.Value ? "닫힐 문에 물체가 있어 전환할 수 없습니다."
                    : puzzle.LeverOn.Value ? "A 닫힘 / B 열림" : "A 열림 / B 닫힘";
            else if (target == puzzle.exit)
                state = puzzle.ConditionsMet ? "통과 가능" : "표시된 압력판을 모두 누르고 있어야 열립니다.";
            return heading + (state.Length > 0 ? "  |  " + state : "") + "\n" + explanation;
        }
    }
}
