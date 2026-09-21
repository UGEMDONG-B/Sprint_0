#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    public sealed partial class GravitySmokeTest
    {
        IEnumerator RunExpansion()
        {
            // Isolated negative tests use explicit setup; the solutions below use real input.
            game.Puzzle.Value = 9;
            game.ResetSection(true);
            var rotor = game.Current.rotors[0];
            game.Current.boxes[0].Body.position = rotor.interior.transform.position;
            Physics.SyncTransforms();
            Check(!rotor.TryTurn() && !rotor.Turned.Value && rotor.Blocked.Value, "Rotor refuses occupied chamber");
            game.ResetSection(true);
            game.runner.Restore(rotor.interior.transform.position);
            Physics.SyncTransforms();
            Check(!rotor.TryTurn(), "Rotor refuses runner in chamber");
            game.ResetSection(true);
            game.Current.boxes[0].Body.position = rotor.interior.transform.position + new Vector3(1, 2.45f, 0);
            Physics.SyncTransforms();
            Check(!rotor.TryTurn() && !rotor.Turned.Value, "Rotor refuses occupied destination wall");
            game.ResetSection(true);
            Check(rotor.TryTurn() && rotor.Turned.Value, "Empty rotor changes physical orientation");
            game.ResetSection(true);
            Check(!rotor.Turned.Value && !rotor.Blocked.Value, "Reset restores rotor state");

            game.Puzzle.Value = 11;
            game.ResetSection(true);
            var clamp = game.Current.clamps[0];
            Check(!clamp.TryToggle(), "Empty clamp rejects lock");
            var anchor = game.Current.boxes[0];
            anchor.Body.position = game.Current.transform.position + new Vector3(-8.2f, 15.5f, -1.6f);
            anchor.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            game.HeldBox.Value = 0;
            Check(!clamp.TryToggle(), "Clamp rejects held crate");
            game.HeldBox.Value = -1;
            anchor.Body.linearVelocity = Vector3.right * 2;
            Check(!clamp.TryToggle(), "Clamp rejects moving crate");
            anchor.Body.linearVelocity = Vector3.zero;
            Check(clamp.TryToggle() && anchor.Anchored.Value, "Clamp locks stationary crate");
            game.runner.Restore(anchor.Body.position + Vector3.right * 1.3f);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.1f);
            Check(game.HeldBox.Value == -1 && anchor.Anchored.Value, "Runner cannot pick up locked crate");
            game.ResetSection();
            yield return Wait(0.3f);
            Check(anchor.Anchored.Value && clamp.LockedBox.Value == 0 && game.Current.CheckpointSaved.Value, "Checkpoint restores P lock");
            Check(game.Current.TransferOpen.Value && !game.Current.DoorOpen.Value, "P opens handoff but not final exit");
            var anchoredAt = anchor.Body.position;
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(0.8f);
            Check(Vector3.Distance(anchor.Body.position, anchoredAt) < 0.02f, "Anchor holds against changed gravity");
            Check(clamp.TryToggle() && !anchor.Anchored.Value, "Clamp releases crate");
            yield return Wait(0.7f);
            Check(Vector3.Distance(anchor.Body.position, anchoredAt) > 0.5f, "Released crate resumes gravity");
            game.ResetSection(true);
            Check(!game.Current.CheckpointSaved.Value && !anchor.Anchored.Value && clamp.LockedBox.Value == -1,
                "Full restart clears checkpoint and lock");
            if (failed) yield break;

            // Verify both physical windows from the initial state, including first-person carry direction.
            game.Puzzle.Value = 8;
            game.ResetSection(true);
            yield return SolveHandoff(true);
            Check(game.Puzzle.Value == 9, "Puzzle 9 upper window completed");
            if (failed) yield break;
            game.Puzzle.Value = 8;
            game.ResetSection(true);
            yield return SolveHandoff(false);
            Check(game.Puzzle.Value == 9, "Puzzle 9 lower window completed");
            if (failed) yield break;

            // P10: operator loads R1, runner sets R2, operator transfers then lifts to Q.
            yield return Wait(0.5f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            Check(game.Current.boxes[0].Body.position.y < 4, "R1 starts with inlet closed");
            Input(Vector2.zero, interact: true);
            yield return Wait(1.5f);
            Check(game.Current.rotors[0].Turned.Value && game.Current.boxes[0].Body.position.y > 6, "Runner turns R1 to load");
            yield return MoveX(1);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.3f);
            Check(game.Current.rotors[1].Turned.Value, "Runner prepares R2 exit");
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(2);
            float localX = game.Current.boxes[0].Body.position.x - game.Current.transform.position.x;
            Check(localX > 4 && localX < 6, "R2 receives physical handoff");
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(1.5f);
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(1.5f);
            Check(game.Current.Pressed.Value == 1, "Rotary delivery reaches Q");
            yield return MoveY(6);
            yield return EnterExit(9);
            Check(game.Puzzle.Value == 10, "Puzzle 10 completed");
            if (failed) yield break;

            // P11: prepare both boxes while down, secure P at the ceiling, then deliver Q right.
            yield return PrepareP();
            yield return MoveZ(1.7f);
            yield return MoveX(-3.25f);
            yield return MoveZ(-1.6f);
            Input(Vector2.zero, interact: true, lookYaw: 90);
            yield return Wait(0.3f);
            Check(game.HeldBox.Value == 1, "P11 second crate picked up");
            yield return MoveX(0.75f);
            yield return MoveZ(1.7f);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.4f);
            yield return MoveX(-10.3f);
            yield return MoveZ(-1.6f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.3f);
            Check(game.Current.clamps[0].LockedBox.Value == 0, "P11 runner locks P");
            yield return MoveZ(1.7f);
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(2);
            Check(game.Current.Pressed.Value == 3 && game.Current.boxes[0].Anchored.Value, "P11 P holds while Q arrives");
            yield return MoveY(6);
            yield return EnterExit(10);
            Check(game.Puzzle.Value == 11, "Puzzle 11 completed");
            if (failed) yield break;

            // P12: save P, return down, carry Q through A, then turn rear R1 from the front handle.
            yield return PrepareP();
            yield return MoveX(-10.3f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.3f);
            Check(game.Current.CheckpointSaved.Value && game.Current.TransferOpen.Value, "P12 first delivery saves and opens A");
            game.ChangeGravityRpc(GravityDirection.Down);
            yield return Wait(2);
            yield return MoveX(-3.25f);
            Input(Vector2.zero, interact: true, lookYaw: 90);
            yield return Wait(0.3f);
            Check(game.HeldBox.Value == 1, "P12 second crate ready for handoff");
            yield return MoveX(-7.25f);
            Input(Vector2.zero, lookYaw: 0);
            yield return Wait(0.3f);
            yield return MoveZ(1.6f);
            Input(Vector2.zero, lookYaw: 90);
            yield return Wait(0.3f);
            yield return MoveX(2.75f);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.4f);
            Check(game.Current.boxes[1].Body.position.z > 0.7f, "P12 crate transferred behind partition");
            yield return MoveX(-6);
            yield return MoveZ(-1.6f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            yield return MoveX(-1);
            Input(Vector2.zero, interact: true);
            yield return Wait(1.5f);
            Check(game.Current.rotors[0].Turned.Value && game.Current.boxes[1].Body.position.y > 10, "P12 rotary inlet loads Q");
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(2);
            Check(game.Current.Pressed.Value == 3, "P12 both switches held");
            yield return MoveY(6);
            Check(game.Finished.Value, "Puzzle 12 completed");
        }

        IEnumerator PrepareP()
        {
            yield return Wait(0.5f);
            Input(Vector2.zero, interact: true, lookYaw: 90);
            yield return Wait(0.3f);
            Check(game.HeldBox.Value == 0, "P crate picked up");
            yield return MoveX(-9.45f);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.4f);
        }

        IEnumerator SolveHandoff(bool upper)
        {
            yield return Wait(0.5f);
            if (upper)
            {
                game.ChangeGravityRpc(GravityDirection.Up);
                yield return Wait(2);
            }
            Input(Vector2.zero, interact: true, lookYaw: upper ? -90 : 90);
            yield return Wait(0.3f);
            Check(game.HeldBox.Value == 0, "P9 crate picked up for " + (upper ? "B" : "A"));
            yield return MoveX(upper ? 0.75f : -7.25f);
            Input(Vector2.zero, lookYaw: 0);
            yield return Wait(0.3f);
            yield return MoveZ(1.7f);
            Input(Vector2.zero, interact: true);
            yield return Wait(0.4f);
            Check(game.Current.boxes[0].Body.position.z > 0.7f, "P9 handoff reaches rear lane");
            if (!upper)
            {
                game.ChangeGravityRpc(GravityDirection.Up);
                yield return Wait(2);
            }
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(2);
            Check(game.Current.Pressed.Value == 1, "P9 rear delivery reaches Q");
            yield return MoveY(6);
            yield return EnterExit(8);
        }
    }
}
#endif
