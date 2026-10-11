using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
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

        [MenuItem("Tools/Game Jam/Run Piano Defense Checks")]
        public static void Run()
        {
            var root = new GameObject("Controller verification");
            try
            {
                var game = root.AddComponent<PianoDefenseGame>();
                if (game.Simulation == null) game.SendMessage("Awake");
                int a = game.Simulation.Towers[0].Id, b = game.Simulation.Towers[1].Id;
                game.SelectOrBuild(8, 2); Pending(game); game.SelectOrBuild(5, 4);
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

                var layoutField = typeof(PianoDefenseGame).GetField("startingTowers", BindingFlags.Instance | BindingFlags.NonPublic);
                var coinsField = typeof(PianoDefenseGame).GetField("startingCoins", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(layoutField != null && coinsField != null, "Scene layout settings are missing from the game component.");
                layoutField.SetValue(game, new List<StartingTower> {
                    new StartingTower(7, 0, TargetMode.Solo, 4),
                    new StartingTower(3, 6, TargetMode.Skip, 2)
                });
                coinsField.SetValue(game, 16);
                game.NewGame(true);
                Require(game.Simulation.Towers.Count == 2 && game.Simulation.Coins == 0,
                    "The scene layout or starting budget was ignored.");
                Require(game.Simulation.TowerAt(7, 0).Pitch == 4 && game.Simulation.TowerAt(3, 6).Mode == TargetMode.Skip,
                    "Scene tower filters were ignored.");
                Debug.Log("PIANO_CHECK_PASS scene layout applies to play mode");

                var boardObject = new GameObject("Board source verification");
                boardObject.transform.SetParent(root.transform, false);
                var board = boardObject.AddComponent<PianoBoardScene>();
                var sceneTower = new GameObject("Moved scene tower");
                sceneTower.transform.SetParent(boardObject.transform, false);
                sceneTower.transform.localPosition = PianoBoardScene.CellPosition(4, 0, -.18f);
                sceneTower.AddComponent<PianoTowerAuthoring>();
                typeof(PianoDefenseGame).GetField("board", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, board);
                game.NewGame(true);
                Require(game.Simulation.TowerAt(4, 0) != null && game.Simulation.Towers.Count == 1,
                    "Play mode did not use the tower GameObject from the scene.");
                Debug.Log("PIANO_CHECK_PASS scene GameObject controls starting tower");
                typeof(PianoDefenseGame).GetField("board", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, null);
                typeof(PianoDefenseGame).GetField("useCompositionLessons", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, true);
                game.SelectLesson(2);
                Require(game.Simulation.Towers.Count == 3 && game.Bpm == CompositionLessons.Tempo(2), "Lesson selection did not reset layout and tempo.");
                int selected = game.Simulation.Towers[0].Id;
                Pending(game); game.SetRhythm(selected, TowerRhythm.Offbeat); Flush(game);
                Require(game.Simulation.TowerById(selected).Rhythm == TowerRhythm.Offbeat, "Queued rhythm change did not reach the next beat.");
                Debug.Log("PIANO_CHECK_PASS lesson selection and queued rhythm");
                game.SelectLesson(1);
                Require(game.Simulation.Beat == -1 && game.Simulation.Notes.Count == 0 && game.Simulation.Resonances == 0 && !game.Started,
                    "A fresh lesson kept the previous transport or music state.");
                game.NewGame(false);
                Require(game.Simulation.Towers.Count == 0 && game.Simulation.Coins == CompositionLessons.Budget(1), "Clear did not restore the lesson budget.");
                Debug.Log("PIANO_CHECK_PASS lesson reset clears music and restores budget");
                Debug.Log("PIANO_CONTROLLER_CHECKS_PASS count=7");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
