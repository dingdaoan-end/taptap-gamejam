using System;
using UnityEngine;

namespace TapTapGameJam.PianoDefense
{
    [RequireComponent(typeof(PianoDefenseGame))]
    public sealed class PianoDefenseView : MonoBehaviour
    {
        static readonly Color Background = Hex(0x101722), Panel = Hex(0x192331), Muted = Hex(0x8f9dab);
        static readonly Color Ink = Hex(0xedf1f1), Mint = Hex(0x68ddc7), Gold = Hex(0xf0c47b);
        static readonly Color[] PitchColors = { Hex(0xed8b91), Hex(0xeeac73), Hex(0xe9cf7c), Hex(0x8dce9b), Hex(0x71c9dd), Hex(0x98a6ee), Hex(0xc59ce6), Hex(0xf1a9cc) };
        static readonly Rect Board = new Rect(296, 188, 840, 490);
        const float Cell = 70;
        PianoDefenseGame game;
        Texture2D pixel, circle;
        Font font;
        GUIStyle label, button;
        Vector2 mouse;
        DefenseSimulation Sim { get { return game.Display; } }

        void Awake() { game = GetComponent<PianoDefenseGame>(); }
        void EnsureStyles()
        {
            if (label != null) return;
            pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
            circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                circle.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(32 - d)));
            }
            circle.Apply();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
            label = new GUIStyle { font = font, richText = false, clipping = TextClipping.Clip };
            button = new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            button.normal.textColor = Ink; button.hover.textColor = Color.white; button.active.textColor = Mint;
        }

        void OnGUI()
        {
            if (game == null || game.Simulation == null) return;
            EnsureStyles();
            GUI.color = Color.white;
            Fill(new Rect(0, 0, Screen.width, Screen.height), Background);
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            Vector2 offset = new Vector2((Screen.width - 1440 * scale) * 0.5f, (Screen.height - 900 * scale) * 0.5f);
            mouse = (Event.current.mousePosition - offset) / scale;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
            HandleKeys();
            Header(); LeftPanel(); BoardView(); MelodyPanel(); TowerPanel(); ScoreStrip(); Legend();
            if (!game.Started) Text("准备阶段可点击下方琴键试听。", new Rect(298, 680, 838, 22), 13, Muted);
            else if (Time.unscaledTime < game.NoticeUntil) Text(game.Notice, new Rect(298, 680, 838, 22), 13, Mint);
            if (game.Paused) PauseOverlay();
            if (game.Finished && !game.Replaying) ResultsOverlay();
            GUI.matrix = old;
        }

        void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Space)
            {
                if (!game.Started) game.StartBattle(); else game.TogglePause(); e.Use();
            }
            else if (e.keyCode == KeyCode.B && !game.Finished) { game.BuildMode = !game.BuildMode; e.Use(); }
            else if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && !game.Paused) { game.SellSelected(); e.Use(); }
            else if (e.keyCode == KeyCode.Escape && game.Started && (!game.Finished || game.Replaying)) { game.TogglePause(); e.Use(); }
        }

        void Header()
        {
            Text("夜之乐章", new Rect(24, 22, 330, 44), 32, Ink, true);
            Text("PIANO STUDY  /  涌现于每一次守护", new Rect(26, 70, 460, 22), 13, Muted);
            Text("据点", new Rect(570, 28, 88, 20), 14, Muted);
            Text(Sim.Hp + " / 10", new Rect(570, 52, 135, 35), 26, Sim.Hp > 3 ? Mint : PitchColors[0], true);
            for (int i = 0; i < 10; i++) Fill(new Rect(703 + i * 7, 63, 4, 18), i < Sim.Hp ? Mint : Panel);
            Text("乐章币", new Rect(812, 28, 100, 20), 14, Muted);
            Text(Sim.Coins.ToString("00"), new Rect(812, 52, 100, 36), 26, Gold, true);
            Text("守护记录", new Rect(954, 28, 130, 20), 14, Muted);
            Text(Sim.Killed + " / " + Sim.TotalEnemies, new Rect(954, 52, 145, 36), 26, Ink, true);
            if (Btn(new Rect(1160, 27, 122, 36), game.Sound.Muted ? "声音：关" : "声音：开", false)) game.Sound.SetMuted(!game.Sound.Muted);
            if (Btn(new Rect(1292, 27, 124, 36), game.Sound.Metronome ? "节拍器：开" : "节拍器：关", false)) game.Sound.Metronome = !game.Sound.Metronome;
            Text("110 BPM  ·  4/4  ·  钢琴原型", new Rect(1160, 74, 256, 22), 13, Muted);
            Fill(new Rect(24, 108, 1392, 1), Hex(0x2b3745));
        }

        void LeftPanel()
        {
            Card(new Rect(24, 128, 248, 548));
            Text("01  /  布阵与守夜", new Rect(44, 148, 215, 22), 13, Mint, true);
            string title = game.Replaying ? "聆听你的乐章" : game.Finished ? "乐章落幕" : !game.Started ? "让防御成为旋律" : game.Paused ? "暂歇片刻" : "守夜进行中";
            Text(title, new Rect(44, 186, 214, 36), 23, Ink, true);
            Paragraph(!game.Started ? "示例琴塔已就位。你可以直接开演，也可以清空后自由布阵。" : "敌人按拍前进，» 装饰音每拍两格、会抢拍。琴塔击杀时，奏响敌人身上的音符。", new Rect(44, 236, 207, 74), 15, Muted);
            if (game.Replaying)
            {
                if (Btn(new Rect(44, 326, 208, 46), "停止回放", true)) game.ToggleReplay();
            }
            else if (!game.Finished)
            {
                if (Btn(new Rect(44, 326, 208, 46), !game.Started ? "开始守夜  [空格]" : game.Paused ? "继续  [空格]" : "暂停  [空格]", true))
                { if (!game.Started) game.StartBattle(); else game.TogglePause(); }
            }
            if (Btn(new Rect(44, 386, 208, 42), game.BuildMode ? "＋ 建造中 · 8币  [B]" : "建造钢琴塔  [B]", game.BuildMode, !game.Finished && !game.Paused)) game.BuildMode = !game.BuildMode;
            Text("琴塔 " + Sim.Towers.Count + "/8     每拍一击 · 射程3格", new Rect(44, 442, 220, 23), 13, Muted);
            if (!game.Started)
            {
                if (Btn(new Rect(44, 488, 208, 36), "清空，自己布阵", false)) game.NewGame(false);
                if (Btn(new Rect(44, 536, 208, 36), "恢复示例布置", false)) game.NewGame(true);
            }
            else
            {
                Text("下一个音符", new Rect(44, 488, 200, 22), 14, Muted);
                int found = 0;
                foreach (var spawn in Sim.Spawns)
                {
                    if (spawn.Beat <= Sim.Beat) continue;
                    if (found == 4) break;
                    Dot(new Vector2(67 + found * 51, 541), 18, PitchColors[spawn.Pitch]);
                    Text(DefenseSimulation.NoteNames[spawn.Pitch] + (spawn.Speed == 2 ? "»" : ""), new Rect(46 + found * 51, 529, 42, 24), 13, Background, true, TextAnchor.MiddleCenter);
                    found++;
                }
                if (found == 0) Text("本夜敌人已全部登场", new Rect(44, 526, 208, 30), 14, Ink);
            }
            Text("音色：合成钢琴 · 占位版", new Rect(44, 627, 215, 24), 12, Muted);
            Card(new Rect(24, 700, 248, 176));
            Text("怎么玩", new Rect(44, 718, 205, 25), 17, Ink, true);
            Paragraph("① 点击深色空地放塔\n② 点击琴塔，筛选音高\n③ 击杀成曲，守住据点\n④ » 装饰音每拍两格，可用筛选放行", new Rect(44, 752, 210, 105), 14, Muted);
        }

        void BoardView()
        {
            int beat = Math.Max(0, game.AudibleBeat);
            string wave = !game.Started ? "准备 / 可以自由调整布置" : game.Replaying ? "回放 / 原始击杀节拍" : beat < 32 ? "第 1 波 / 听见顺序" : beat < 40 ? "间奏 / 重新布阵" : beat < 72 ? "第 2 波 / 填充音·装饰音抢拍" : beat < 80 ? "间奏 / 为终段做准备" : "第 3 波 / 多路合奏·装饰音突进";
            Text(wave, new Rect(296, 125, 605, 25), 17, Ink, true);
            Text((game.Started ? "拍 " + (game.AudibleBeat + 1) + " / 112" : "一夜约 60 秒") + "   ← 来敌", new Rect(908, 125, 228, 25), 14, Muted, false, TextAnchor.MiddleRight);
            for (int i = 0; i < 16; i++)
            {
                Color c = i % 4 == 0 ? Hex(0x425267) : Hex(0x293747);
                if (game.AudibleBeat >= 0 && beat % 16 == i) c = Mint;
                Fill(new Rect(Board.x + i * 52.5f, 162, 46.5f, 8), c);
            }
            var selected = Sim.TowerById(game.SelectedTowerId);
            for (int y = 0; y < 7; y++) for (int x = 0; x < 12; x++)
            {
                Rect cell = CellRect(x, y);
                Color c = DefenseSimulation.IsLane(y) ? Hex(0x293646) : Hex(0x18222f);
                if (x == 0) c = Hex(0x21483f);
                if (x == 11) c = Hex(0x413139);
                Fill(new Rect(cell.x + 1, cell.y + 1, Cell - 2, Cell - 2), c);
                if (selected != null && Math.Abs(selected.X - x) + Math.Abs(selected.Y - y) <= 3)
                    Fill(new Rect(cell.x + 2, cell.y + 2, Cell - 4, Cell - 4), Alpha(Mint, 0.10f));
                if (DefenseSimulation.IsLane(y) && x > 0 && x < 11 && x % 2 == 1)
                    Text("‹", cell, 28, Hex(0x4c5e71), false, TextAnchor.MiddleCenter);
                if (x == 0 && DefenseSimulation.IsLane(y)) Text("守", cell, 20, Mint, true, TextAnchor.MiddleCenter);
                if (x == 11 && DefenseSimulation.IsLane(y)) Text("入", cell, 18, Hex(0xb8818e), false, TextAnchor.MiddleCenter);
            }
            if (Board.Contains(mouse) && !game.Finished && !game.Paused)
            {
                int x = (int)((mouse.x - Board.x) / Cell), y = (int)((mouse.y - Board.y) / Cell);
                if (game.BuildMode && DefenseSimulation.IsBuildCell(x, y) && Sim.TowerAt(x, y) == null)
                {
                    Rect r = CellRect(x, y); Fill(r, Alpha(Sim.Coins >= 8 ? Mint : PitchColors[0], 0.18f));
                    Text("＋", r, 28, Sim.Coins >= 8 ? Mint : PitchColors[0], false, TextAnchor.MiddleCenter);
                }
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { game.SelectOrBuild(x, y); Event.current.Use(); }
            }
            foreach (var tower in Sim.Towers)
            {
                Vector2 center = Center(tower.X, tower.Y);
                if (tower.Id == game.SelectedTowerId) Dot(center, 31, Mint);
                Dot(center, 28, Hex(0x354756));
                Fill(new Rect(center.x - 21, center.y - 19, 42, 33), Ink);
                for (int k = 0; k < 4; k++) Fill(new Rect(center.x - 14 + k * 9, center.y - 19, 4, 18), Background);
                Text(tower.Mode == TargetMode.All ? "全部" : (tower.Mode == TargetMode.Solo ? "独 " : "跳 ") + DefenseSimulation.NoteNames[tower.Pitch], new Rect(center.x - 31, center.y + 15, 62, 19), 11, tower.Mode == TargetMode.All ? Ink : PitchColors[tower.Pitch], true, TextAnchor.MiddleCenter);
            }
            if (!game.Replaying)
            {
                foreach (var enemy in Sim.Enemies)
                {
                    float progress = Sim.Beat > game.AudibleBeat ? 0 : Mathf.Clamp01((float)((AudioSettings.dspTime - game.BeatTime) / 0.14));
                    float x = Mathf.Lerp(enemy.PreviousX, enemy.X, progress);
                    if (x >= 12) continue;
                    Vector2 center = Center(x, enemy.Lane);
                    Dot(center + new Vector2(0, 3), 23, Alpha(Color.black, 0.2f));
                    Dot(center, 22, PitchColors[enemy.Pitch]);
                    Text(DefenseSimulation.NoteNames[enemy.Pitch], new Rect(center.x - 24, center.y - 14, 48, 28), 16, Background, true, TextAnchor.MiddleCenter);
                    if (enemy.Speed == 2) Text("»", new Rect(center.x + 20, center.y - 13, 22, 26), 16, PitchColors[enemy.Pitch], true, TextAnchor.MiddleLeft);
                }
                foreach (var visual in game.Shots)
                {
                    float t = (Time.unscaledTime - visual.Time) / 0.4f;
                    var shot = visual.Shot; Color c = Alpha(PitchColors[shot.Pitch], 1 - t);
                    Vector2 end = Center(shot.ToX, shot.ToY);
                    if (t < 0.45f) Line(Center(shot.FromX, shot.FromY), end, c, 3);
                    Dot(end, 13 + 20 * t, Alpha(c, (1 - t) * 0.25f));
                    Text(DefenseSimulation.NoteNames[shot.Pitch], new Rect(end.x - 32, end.y - 22 - t * 30, 64, 26), 20, c, true, TextAnchor.MiddleCenter);
                }
            }
        }

        void MelodyPanel()
        {
            Card(new Rect(1160, 128, 256, 254));
            Text("02  /  目标乐句", new Rect(1178, 146, 218, 24), 13, Mint, true);
            int progress = Sim.MelodyProgress;
            for (int i = 0; i < 4; i++)
            {
                int p = DefenseSimulation.TargetMelody[i]; float x = 1178 + i * 56;
                Fill(new Rect(x, 190, 50, 65), progress > i ? PitchColors[p] : Hex(0x283647));
                Text(DefenseSimulation.NoteNames[p], new Rect(x, 198, 50, 28), 17, progress > i ? Background : PitchColors[p], true, TextAnchor.MiddleCenter);
                Text(DefenseSimulation.PitchNames[p], new Rect(x, 229, 50, 18), 11, progress > i ? Background : Muted, false, TextAnchor.MiddleCenter);
            }
            Text("每 16 拍结算  ·  已完成 " + Sim.CompleteMelodies + " 次", new Rect(1178, 270, 227, 22), 13, Ink);
            Text("本乐句祝福", new Rect(1178, 297, 110, 22), 13, Ink);
            for (int i = 0; i < 4; i++)
                Fill(new Rect(1318 + i * 24, 297, 20, 20), Sim.MelodyProgress > i ? PitchColors[DefenseSimulation.TargetMelody[i]] : Hex(0x283647));
            Text("每层 +1 币  ·  2 层以上据点 +1", new Rect(1178, 328, 227, 22), 13, Muted);
            Text("4 层终止式：清除 1/4 敌人", new Rect(1178, 353, 227, 22), 13, Gold);
        }

        void TowerPanel()
        {
            Card(new Rect(1160, 398, 256, 278));
            var tower = Sim.TowerById(game.SelectedTowerId);
            Text(tower == null ? "03  /  选择一座琴塔" : "03  /  钢琴塔 #" + tower.Id, new Rect(1178, 415, 220, 25), 14, Ink, true);
            if (tower == null)
            {
                Paragraph("点击场上的琴塔，决定它演奏哪些音。\n\n独奏：只打指定音高\n跳过：放行指定音高", new Rect(1178, 463, 218, 149), 15, Muted);
                return;
            }
            string[] modes = { "全部", "独奏", "跳过" };
            for (int m = 0; m < 3; m++)
                if (Btn(new Rect(1178 + m * 76, 459, 68, 36), modes[m], (int)tower.Mode == m, !game.Finished && !game.Paused)) game.SetMode(tower.Id, (TargetMode)m);
            for (int p = 0; p < 8; p++)
            {
                Rect r = new Rect(1178 + p % 4 * 56, 514 + p / 4 * 43, 48, 34);
                if (Btn(r, DefenseSimulation.NoteNames[p], tower.Pitch == p, !game.Finished && !game.Paused)) game.SetPitch(tower.Id, p);
            }
            if (Btn(new Rect(1178, 620, 220, 35), "回收 ＋" + (game.Started ? 6 : 8) + "币  [Delete]", false, !game.Finished && !game.Paused)) game.SellSelected();
        }

        void ScoreStrip()
        {
            Card(new Rect(296, 708, 840, 168));
            Text(game.Replaying ? "击杀回放 / 保留你当时的每一拍" : "从战斗里，写下一段乐章", new Rect(312, 718, 585, 22), 14, Ink, true);
            Text("同拍多音 = 和声", new Rect(936, 718, 182, 22), 12, Muted, false, TextAnchor.MiddleRight);
            for (int y = 0; y < 5; y++) Fill(new Rect(318, 751 + y * 11, 796, 1), Hex(0x3b4959));
            int audible = Math.Max(0, game.AudibleBeat), phraseStart = audible / 16 * 16;
            for (int i = 0; i < 16; i++)
            {
                float x = 338 + i * 49;
                if (game.AudibleBeat >= 0 && audible % 16 == i) Fill(new Rect(x - 17, 748, 35, 55), Alpha(Mint, 0.10f));
            }
            foreach (var note in Sim.Notes)
            {
                if (note.Beat < phraseStart || note.Beat >= phraseStart + 16 || note.Beat > game.AudibleBeat) continue;
                float x = 338 + (note.Beat - phraseStart) * 49, y = 794 - note.Pitch * 6;
                Dot(new Vector2(x, y), 5, PitchColors[note.Pitch]);
                Fill(new Rect(x + 3, y - 19, 2, 20), PitchColors[note.Pitch]);
            }
            for (int p = 0; p < 8; p++)
            {
                Rect key = new Rect(316 + p * 100, 817, 96, 46);
                bool flash = game.KeyFlash[p] > 0 && Time.unscaledTime - game.KeyFlash[p] < 0.3f;
                Fill(key, flash ? PitchColors[p] : Ink);
                Text(DefenseSimulation.NoteNames[p], new Rect(key.x + 6, key.y + 7, 57, 29), 17, Background, true);
                Text(DefenseSimulation.PitchNames[p], new Rect(key.x + 56, key.y + 10, 36, 25), 11, Hex(0x566579));
                if (GUI.Button(key, GUIContent.none, GUIStyle.none)) game.PreviewKey(p);
            }
        }

        void Legend()
        {
            Card(new Rect(1160, 700, 256, 176));
            Text("音高即敌人的身份", new Rect(1178, 718, 220, 24), 15, Ink, true);
            for (int i = 0; i < 8; i++)
            {
                float x = 1190 + i % 4 * 57, y = 772 + i / 4 * 42;
                Dot(new Vector2(x, y), 5, PitchColors[i]);
                Text(DefenseSimulation.NoteNames[i], new Rect(x - 13, y + 8, 42, 20), 11, Muted);
            }
        }

        void PauseOverlay()
        {
            Fill(new Rect(0, 112, 1440, 788), Alpha(Background, 0.72f));
            Card(new Rect(488, 326, 464, 238));
            Text("乐章暂停", new Rect(518, 350, 404, 50), 28, Ink, true, TextAnchor.MiddleCenter);
            Text("切回窗口后，按空格继续。", new Rect(518, 409, 404, 35), 16, Muted, false, TextAnchor.MiddleCenter);
            if (Btn(new Rect(560, 476, 320, 48), "继续守夜  [空格]", true)) game.TogglePause();
        }

        void ResultsOverlay()
        {
            Fill(new Rect(0, 112, 1440, 788), Alpha(Background, 0.86f));
            Card(new Rect(390, 228, 660, 450));
            Text(Sim.Won ? "今夜，被你谱成了音乐。" : "乐章暂歇，再试一种布阵。", new Rect(420, 255, 600, 45), 27, Sim.Won ? Mint : PitchColors[0], true, TextAnchor.MiddleCenter);
            Text("乐章得分  " + Sim.Score, new Rect(420, 322, 600, 56), 38, Ink, true, TextAnchor.MiddleCenter);
            Text(new string('★', Sim.Stars) + new string('☆', 3 - Sim.Stars), new Rect(420, 384, 600, 34), 27, Gold, false, TextAnchor.MiddleCenter);
            Text("守住 " + Sim.Hp + "/10  ·  击杀 " + Sim.Killed + "/" + Sim.TotalEnemies + "  ·  完整旋律 " + Sim.CompleteMelodies + " 次", new Rect(420, 440, 600, 27), 17, Muted, false, TextAnchor.MiddleCenter);
            Text("记录了 " + Sim.Notes.Count + " 个钢琴音符，回放保留原始节拍和停顿。", new Rect(420, 482, 600, 26), 15, Ink, false, TextAnchor.MiddleCenter);
            if (Btn(new Rect(435, 542, 570, 48), "聆听这一次战斗的乐章", true, Sim.Notes.Count > 0)) game.ToggleReplay();
            if (Btn(new Rect(435, 606, 278, 40), "重新布阵", false)) game.NewGame(false);
            if (Btn(new Rect(727, 606, 278, 40), "再演一次示例", false)) game.NewGame(true);
        }

        static Color Hex(int rgb) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f); }
        static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
        Rect CellRect(int x, int y) { return new Rect(Board.x + x * Cell, Board.y + y * Cell, Cell, Cell); }
        Vector2 Center(float x, float y) { return new Vector2(Board.x + (x + 0.5f) * Cell, Board.y + (y + 0.5f) * Cell); }
        void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, pixel != null ? pixel : Texture2D.whiteTexture); GUI.color = Color.white; }
        void Dot(Vector2 center, float radius, Color color) { GUI.color = color; GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), circle); GUI.color = Color.white; }
        void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y - width / 2, Vector2.Distance(a, b), width), color); GUI.matrix = previous;
        }
        void Card(Rect r) { Fill(r, Panel); Fill(new Rect(r.x, r.y, r.width, 2), Hex(0x334152)); }
        void Text(string text, Rect rect, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            label.fontSize = size; label.normal.textColor = color; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = align; label.wordWrap = false; GUI.Label(rect, text, label);
        }
        void Paragraph(string text, Rect rect, int size, Color color)
        {
            label.fontSize = size; label.normal.textColor = color; label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.UpperLeft; label.wordWrap = true; GUI.Label(rect, text, label);
        }
        bool Btn(Rect rect, string text, bool primary, bool enabled = true)
        {
            bool previous = GUI.enabled; GUI.enabled = enabled;
            Color bg = primary ? Hex(0x345e58) : Hex(0x293747);
            if (rect.Contains(mouse) && enabled) bg = primary ? Hex(0x437e70) : Hex(0x3b4b5e);
            Fill(rect, enabled ? bg : Hex(0x202a37));
            bool clicked = GUI.Button(rect, text, button); GUI.enabled = previous; return clicked;
        }
        void OnDestroy()
        {
            if (pixel != null) Destroy(pixel); if (circle != null) Destroy(circle); if (font != null) Destroy(font);
        }
    }
}
