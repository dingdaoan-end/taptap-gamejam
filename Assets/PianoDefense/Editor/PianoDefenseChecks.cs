using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TapTapGameJam.PianoDefense.Editor
{
    // Exercises the actual controller while a beat is pending, without audio playback or UI automation.
    public static class PianoDefenseChecks
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Pending(PianoDefenseGame game)
        {
            typeof(PianoDefenseGame).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, game.Simulation.Step());
        }
        static void Flush(PianoDefenseGame game)
        {
            var queue = (Queue<Action>)typeof(PianoDefenseGame).GetField("queuedInput", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            while (queue.Count > 0) queue.Dequeue()();
        }

        public static void Run()
        {
            var root = new GameObject("Controller verification");
            try
            {
                var game = root.AddComponent<PianoDefenseGame>();
                if (game.Simulation == null) game.SendMessage("Awake");
                int a = game.Simulation.Towers[0].Id, b = game.Simulation.Towers[1].Id;
                game.SelectOrBuild(9, 0); Pending(game); game.SelectOrBuild(9, 2);
                Require(game.SelectedTowerId == b, "Selecting a visible tower must not wait for the pending beat.");
                game.SellSelected(); Flush(game);
                Require(game.Simulation.TowerById(a) != null && game.Simulation.TowerById(b) == null, "Selling removed the previously selected tower.");
                Debug.Log("PIANO_CHECK_PASS select then sell during pending beat");

                game.NewGame(false); game.SelectOrBuild(8, 0); Pending(game);
                game.BuildMode = true; game.SelectOrBuild(5, 4); game.BuildMode = false; Flush(game);
                Require(game.Simulation.TowerAt(5, 4) != null, "Changing build mode canceled a previously issued build order.");
                Debug.Log("PIANO_CHECK_PASS build order preserves click intent");

                game.NewGame(true); a = game.Simulation.Towers[0].Id; Pending(game);
                game.SetMode(a, TargetMode.Solo); game.SetPitch(a, 2); Flush(game);
                Require(game.Simulation.TowerById(a).Mode == TargetMode.Solo && game.Simulation.TowerById(a).Pitch == 2,
                    "Queued pitch change overwrote the preceding mode change.");
                Debug.Log("PIANO_CHECK_PASS mode and pitch commands compose in order");
                game.NewGame(true); Pending(game); game.BuildMode = true; game.SelectOrBuild(5, 4);
                game.SelectLevel("progression-4536251");
                Require(game.Level.id == "progression-4536251" && game.Simulation.TotalEnemies == 37 &&
                    game.Simulation.TotalBeats == 16 && game.Simulation.Beat == -1 && game.Simulation.Notes.Count == 0,
                    "Switching failed to load the saved 4536251 level.");
                Flush(game);
                Require(game.Simulation.Towers.Count == 4 && game.Simulation.TowerAt(5, 4) == null,
                    "Old queued input changed the new level.");
                game.StartBattle(); game.TogglePause(); game.SelectLevel("canon");
                Require(!game.Started && !game.Paused && !game.Finished && !game.Replaying &&
                    game.Simulation.TotalEnemies == 87 && game.Simulation.TotalBeats == 36,
                    "Paused state leaked across level switch.");
                game.NewGame(false);
                Require(game.Level.id == "canon" && game.Simulation.Towers.Count == 0, "Clear layout changed level.");
                game.NewGame(true);
                Require(game.Level.id == "canon" && game.Simulation.Towers.Count == 4, "Restore changed level.");
                Debug.Log("PIANO_CHECK_PASS saved examples switch and clear pending state");
                Debug.Log("PIANO_CONTROLLER_CHECKS_PASS count=4");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
