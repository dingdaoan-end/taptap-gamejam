using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.PianoDefense
{
    // The children of this component are the board seen in both Scene and Game views.
    public sealed class PianoBoardScene : MonoBehaviour
    {
        public static readonly Color[] PitchColors = {
            new Color(.93f,.55f,.57f), new Color(.93f,.67f,.45f),
            new Color(.91f,.81f,.49f), new Color(.55f,.81f,.61f),
            new Color(.44f,.79f,.87f), new Color(.60f,.67f,.93f),
            new Color(.77f,.61f,.90f), new Color(.95f,.66f,.80f)
        };
        [SerializeField] GameObject towerPrefab;
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Camera boardCamera;

        readonly Dictionary<int, GameObject> runtimeTowers = new Dictionary<int, GameObject>();
        readonly Dictionary<int, GameObject> runtimeEnemies = new Dictionary<int, GameObject>();
        readonly List<int> stale = new List<int>();
        PianoDefenseGame game;
        LineRenderer selection;
        LineRenderer resonance;
        readonly List<LineRenderer> shotLines = new List<LineRenderer>();
        Material lineMaterial;

        public static Vector3 CellPosition(int x, int y, float z = 0)
        {
            return new Vector3(x - (DefenseSimulation.Width - 1) * 0.5f,
                (DefenseSimulation.Height - 1) * 0.5f - y, z);
        }

        public IEnumerable<StartingTower> ReadStartingTowers(int lesson = 0)
        {
            foreach (var tower in GetComponentsInChildren<PianoTowerAuthoring>(true))
            {
                if (tower.Lesson != lesson) continue;
                int x = Mathf.RoundToInt(tower.transform.localPosition.x + (DefenseSimulation.Width - 1) * 0.5f);
                int y = Mathf.RoundToInt((DefenseSimulation.Height - 1) * 0.5f - tower.transform.localPosition.y);
                yield return new StartingTower(x, y, tower.Mode, tower.Pitch, tower.Rhythm);
            }
        }

        public void SetScreenViewport(RectTransform viewport)
        {
            if (boardCamera == null) return;
            var corners = new Vector3[4]; viewport.GetWorldCorners(corners);
            boardCamera.rect = new Rect(corners[0].x / Screen.width, corners[0].y / Screen.height,
                (corners[2].x - corners[0].x) / Screen.width, (corners[2].y - corners[0].y) / Screen.height);
        }

        public void SetViewport(Rect boardRect, Vector2 offset, float scale)
        {
            if (boardCamera == null || Screen.width <= 0 || Screen.height <= 0) return;
            boardCamera.rect = new Rect(
                (offset.x + boardRect.x * scale) / Screen.width,
                (Screen.height - offset.y - (boardRect.y + boardRect.height) * scale) / Screen.height,
                boardRect.width * scale / Screen.width,
                boardRect.height * scale / Screen.height);
        }

        void Awake() { game = GetComponentInParent<PianoDefenseGame>(); }

        void LateUpdate()
        {
            if (game == null || game.Display == null) return;
            SyncTowers();
            SyncEnemies();
            SyncEffects();
        }

        void SyncTowers()
        {
            var authored = new HashSet<int>();
            foreach (var anchor in GetComponentsInChildren<PianoTowerAuthoring>(true))
            {
                if (anchor.Lesson != (game.CompositionMode ? game.LessonIndex : 0)) { anchor.gameObject.SetActive(false); continue; }
                int x = Mathf.RoundToInt(anchor.transform.localPosition.x + (DefenseSimulation.Width - 1) * 0.5f);
                int y = Mathf.RoundToInt((DefenseSimulation.Height - 1) * 0.5f - anchor.transform.localPosition.y);
                var state = game.Display.TowerAt(x, y);
                anchor.gameObject.SetActive(state != null);
                if (state != null) { authored.Add(state.Id); UpdateTower(anchor.gameObject, state); }
            }
            var live = new HashSet<int>();
            foreach (var tower in game.Display.Towers)
            {
                if (authored.Contains(tower.Id)) continue;
                live.Add(tower.Id);
                if (!runtimeTowers.TryGetValue(tower.Id, out var visual))
                {
                    if (towerPrefab == null) continue;
                    visual = Instantiate(towerPrefab, transform);
                    visual.name = "Runtime Tower " + tower.Id;
                    runtimeTowers.Add(tower.Id, visual);
                }
                visual.transform.localPosition = CellPosition(tower.X, tower.Y, -0.18f);
                UpdateTower(visual, tower);
            }
            stale.Clear();
            foreach (var entry in runtimeTowers) if (!live.Contains(entry.Key)) stale.Add(entry.Key);
            foreach (int id in stale) { Destroy(runtimeTowers[id]); runtimeTowers.Remove(id); }
        }

        void SyncEnemies()
        {
            var live = new HashSet<int>();
            foreach (var enemy in game.Display.Enemies)
            {
                if (enemy.X >= DefenseSimulation.Width) continue;
                live.Add(enemy.Id);
                if (!runtimeEnemies.TryGetValue(enemy.Id, out var visual))
                {
                    if (enemyPrefab == null) continue;
                    visual = Instantiate(enemyPrefab, transform);
                    visual.name = "Enemy " + enemy.Id;
                    runtimeEnemies.Add(enemy.Id, visual);
                }
                float progress = game.Display.Beat > game.AudibleBeat ? 0 :
                    Mathf.Clamp01((float)((AudioSettings.dspTime - game.BeatTime) / 0.14));
                float x = Mathf.Lerp(enemy.PreviousX, enemy.X, progress);
                visual.transform.localPosition = new Vector3(x - (DefenseSimulation.Width - 1) * 0.5f,
                    (DefenseSimulation.Height - 1) * 0.5f - enemy.Lane, -0.3f);
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.GetComponent<TextMesh>() != null) continue;
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    properties.SetColor("_Color", PitchColors[enemy.Pitch]);
                    renderer.SetPropertyBlock(properties);
                }
                var text = visual.GetComponentInChildren<TextMesh>();
                if (text != null) text.text = DefenseSimulation.NoteNames[enemy.Pitch] + (enemy.Speed == 2 ? " >>" : "");
            }
            stale.Clear();
            foreach (var entry in runtimeEnemies) if (!live.Contains(entry.Key)) stale.Add(entry.Key);
            foreach (int id in stale) { Destroy(runtimeEnemies[id]); runtimeEnemies.Remove(id); }
        }

        void UpdateTower(GameObject visual, TowerState tower)
        {
            var label = visual.GetComponentInChildren<TextMesh>();
            if (label != null)
            {
                string[] rhythms = { "1 2 3 4", "1 - 3 -", "- 2 - 4", "1 - - -" };
                label.text = (tower.Mode == TargetMode.All ? "PIANO" : tower.Mode == TargetMode.Solo ? "ONLY " + DefenseSimulation.NoteNames[tower.Pitch] : "SKIP " + DefenseSimulation.NoteNames[tower.Pitch]) + "\n" + rhythms[(int)tower.Rhythm];
            }
            var halo = visual.transform.Find("Beat Lamp");
            if (halo != null)
            {
                bool active = DefenseSimulation.AttacksOnBeat(tower.Rhythm, Mathf.Max(0, game.AudibleBeat));
                var renderer = halo.GetComponent<Renderer>();
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_Color", active ? new Color(.42f, .90f, .77f) : new Color(.17f, .23f, .29f));
                renderer.SetPropertyBlock(properties);
            }
        }

        LineRenderer CreateLine(string name, float width)
        {
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false); line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial; line.widthMultiplier = width;
            line.numCapVertices = 4; line.numCornerVertices = 3;
            return line;
        }

        void SyncEffects()
        {
            if (selection == null) selection = CreateLine("Selected Tower Range", .035f);
            var selected = game.Display.TowerById(game.SelectedTowerId);
            selection.gameObject.SetActive(selected != null);
            if (selected != null)
            {
                selection.startColor = selection.endColor = new Color(.40f, .85f, .75f, .6f);
                selection.loop = true; selection.positionCount = 4;
                var center = CellPosition(selected.X, selected.Y, -.65f);
                selection.SetPositions(new[] { center + Vector3.up * 3, center + Vector3.right * 3, center + Vector3.down * 3, center + Vector3.left * 3 });
            }
            for (int i = 0; i < game.Shots.Count; i++)
            {
                if (i >= shotLines.Count) shotLines.Add(CreateLine("Piano Strike " + i, .055f));
                var visual = game.Shots[i]; var line = shotLines[i]; line.gameObject.SetActive(true);
                Color color = PitchColors[visual.Shot.Pitch]; color.a = Mathf.Clamp01(1 - (Time.unscaledTime - visual.Time) / .4f);
                line.startColor = line.endColor = color; line.positionCount = 2;
                line.SetPositions(new[] { CellPosition(visual.Shot.FromX, visual.Shot.FromY, -.6f), CellPosition(visual.Shot.ToX, visual.Shot.ToY, -.6f) });
            }
            for (int i = game.Shots.Count; i < shotLines.Count; i++) shotLines[i].gameObject.SetActive(false);
            if (resonance == null) resonance = CreateLine("Resonance Wave", .055f);
            float age = Time.unscaledTime - game.ResonanceTime;
            resonance.gameObject.SetActive(age >= 0 && age < 1.25f);
            if (resonance.gameObject.activeSelf)
            {
                Color color = new Color(.43f, .94f, .79f, 1 - age / 1.25f);
                resonance.startColor = resonance.endColor = color; resonance.loop = true; resonance.positionCount = 64;
                for (int i = 0; i < 64; i++)
                {
                    float angle = i * Mathf.PI * 2 / 64;
                    resonance.SetPosition(i, new Vector3(Mathf.Cos(angle) * (1 + age * 6), Mathf.Sin(angle) * (1 + age * 6), -.7f));
                }
            }
        }

        void OnDestroy() { if (lineMaterial != null) Destroy(lineMaterial); }
    }

}
