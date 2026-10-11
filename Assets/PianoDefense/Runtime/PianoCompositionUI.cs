using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.PianoDefense
{
    public sealed class PianoCompositionUI : MonoBehaviour
    {
        public PianoDefenseGame Game;
        public RectTransform BoardViewport;
        public Text Status, LessonName, Hint, Progress, TowerInfo, Notice, Goal, ResultsTitle, ResultsInfo;
        public Text StartLabel, BuildLabel, AudioLabel, ReplayLabel;
        public Button StartButton, BuildButton, ClearButton, RestoreButton, SellButton, AudioButton, ReplayButton, RetryButton, NextButton, ContinueButton;
        public Button[] LessonButtons, RhythmButtons, ModeButtons, PitchButtons, PreviewButtons;
        public Image[] BeatLights, NoteDots, KeyLights;
        public GameObject PausePanel, ResultsPanel;
        static readonly Color Mint = new Color(.40f, .87f, .76f), Dim = new Color(.18f, .24f, .31f), Ink = new Color(.92f, .94f, .95f);

        void Awake()
        {
            StartButton.onClick.AddListener(() => { if (Game.Replaying) Game.ToggleReplay(); else if (!Game.Started) Game.StartBattle(); else Game.TogglePause(); });
            BuildButton.onClick.AddListener(() => Game.BuildMode = !Game.BuildMode);
            ClearButton.onClick.AddListener(() => Game.NewGame(false)); RestoreButton.onClick.AddListener(() => Game.NewGame(true));
            SellButton.onClick.AddListener(Game.SellSelected); AudioButton.onClick.AddListener(() => Game.Sound.SetMuted(!Game.Sound.Muted));
            ReplayButton.onClick.AddListener(Game.ToggleReplay); RetryButton.onClick.AddListener(() => Game.NewGame(true));
            NextButton.onClick.AddListener(() => Game.SelectLesson((Game.LessonIndex + 1) % 4));
            ContinueButton.onClick.AddListener(Game.TogglePause);
            for (int i = 0; i < LessonButtons.Length; i++) { int index = i; LessonButtons[i].onClick.AddListener(() => Game.SelectLesson(index)); }
            for (int i = 0; i < RhythmButtons.Length; i++) { int index = i; RhythmButtons[i].onClick.AddListener(() => Game.SetRhythm(Game.SelectedTowerId, (TowerRhythm)index)); }
            for (int i = 0; i < ModeButtons.Length; i++) { int index = i; ModeButtons[i].onClick.AddListener(() => Game.SetMode(Game.SelectedTowerId, (TargetMode)index)); }
            for (int i = 0; i < PitchButtons.Length; i++) { int index = i; PitchButtons[i].onClick.AddListener(() => Game.SetPitch(Game.SelectedTowerId, index)); }
            for (int i = 0; i < PreviewButtons.Length; i++) { int index = i; PreviewButtons[i].onClick.AddListener(() => Game.PreviewKey(index)); }
        }

        void Update()
        {
            if (Game == null || Game.Display == null) return;
            if (Input.GetKeyDown(KeyCode.Space)) { if (!Game.Started) Game.StartBattle(); else Game.TogglePause(); }
            if (Input.GetKeyDown(KeyCode.Escape)) Game.TogglePause();
            if (Input.GetKeyDown(KeyCode.B) && !Game.Paused && !Game.Finished) Game.BuildMode = !Game.BuildMode;
            if (Input.GetKeyDown(KeyCode.Delete)) Game.SellSelected();
            var s = Game.Display;
            Set(Status, "生命  " + s.Hp + "/10     乐章币  " + s.Coins.ToString("00") + "     守护  " + s.Killed + "/" + s.TotalEnemies);
            Set(LessonName, Game.LessonTitle); Set(Hint, Game.LessonHint);
            Set(Progress, Game.Bpm + " BPM   /   " + (Game.Started ? "拍 " + (Game.AudibleBeat + 1) + " / " + Game.TotalBeats : "准备演奏") + "   /   来敌 ←");
            Set(StartLabel, Game.Replaying ? "停止回放" : !Game.Started ? "开始演奏  [空格]" : Game.Paused ? "继续演奏  [空格]" : "暂停演奏  [空格]");
            Set(BuildLabel, Game.BuildMode ? "＋ 钢琴塔  ·  建造中" : "＋ 建造钢琴塔  ·  8 币");
            Set(AudioLabel, Game.Sound.Muted ? "声音  关闭" : "声音  开启");
            bool active = !Game.Finished && !Game.Paused && !Game.Replaying;
            StartButton.interactable = !Game.Finished || Game.Replaying; BuildButton.interactable = active;
            ClearButton.interactable = RestoreButton.interactable = !Game.Started;
            for (int i = 0; i < LessonButtons.Length; i++)
            {
                LessonButtons[i].interactable = !Game.Started || Game.Finished;
                Tint(LessonButtons[i], i == Game.LessonIndex);
            }
            var tower = s.TowerById(Game.SelectedTowerId);
            Set(TowerInfo, tower == null ? "点击棋盘上的琴塔\n选择它的节拍与音调" : "钢琴塔 #" + tower.Id + "    (" + tower.X + ", " + tower.Y + ")\n射程 3 格 · 优先攻击最近敌人");
            SellButton.interactable = active && tower != null;
            for (int i = 0; i < RhythmButtons.Length; i++) { RhythmButtons[i].interactable = active && tower != null; Tint(RhythmButtons[i], tower != null && (int)tower.Rhythm == i); }
            for (int i = 0; i < ModeButtons.Length; i++) { ModeButtons[i].interactable = active && tower != null; Tint(ModeButtons[i], tower != null && (int)tower.Mode == i); }
            for (int i = 0; i < PitchButtons.Length; i++) { PitchButtons[i].interactable = active && tower != null && tower.Mode != TargetMode.All; Tint(PitchButtons[i], tower != null && tower.Pitch == i && tower.Mode != TargetMode.All); }
            for (int i = 0; i < BeatLights.Length; i++) BeatLights[i].color = Game.AudibleBeat >= 0 && Game.AudibleBeat % 4 == i ? Mint : Dim;
            Set(Goal, "同拍 Do + Mi + Sol  →  共鸣\n全场休止 1 拍 · +3 币\n本局共鸣 " + s.Resonances + " 次  /  完整旋律 " + s.CompleteMelodies + " 次");
            Set(Notice, Time.unscaledTime < Game.NoticeUntil ? Game.Notice : "点击空地建塔 · 点击琴塔调整 · 下方琴键可试听");
            for (int i = 0; i < NoteDots.Length; i++) NoteDots[i].enabled = false;
            int phraseStart = Mathf.Max(0, Game.AudibleBeat) / 16 * 16;
            foreach (var note in s.Notes)
                if (note.Beat >= phraseStart && note.Beat < phraseStart + 16 && note.Beat <= Game.AudibleBeat)
                    NoteDots[(note.Beat - phraseStart) * 8 + note.Pitch].enabled = true;
            for (int i = 0; i < KeyLights.Length; i++)
            {
                bool flash = Game.KeyFlash[i] > 0 && Time.unscaledTime - Game.KeyFlash[i] < .3f;
                KeyLights[i].color = flash ? PianoBoardScene.PitchColors[i] : Ink;
                PreviewButtons[i].interactable = !Game.Replaying && (!Game.Started || Game.Finished);
            }
            PausePanel.SetActive(Game.Paused);
            ResultsPanel.SetActive(Game.Finished && !Game.Replaying);
            if (Game.Finished)
            {
                Set(ResultsTitle, s.Won ? "你守住了一段乐章。" : "换一种布阵，再演奏一次。");
                Set(ResultsInfo, "得分 " + s.Score + "   ·   生命 " + s.Hp + "/10\n记录 " + s.Notes.Count + " 个音符   ·   共鸣 " + s.Resonances + " 次\n回放保留这次演奏的节拍、和声与休止。");
                ReplayButton.interactable = s.Notes.Count > 0;
                NextButton.interactable = s.Won;
            }
            Set(ReplayLabel, Game.Replaying ? "停止回放" : "聆听我的乐章");
        }
        void LateUpdate() { if (Game != null && Game.Board != null) Game.Board.SetScreenViewport(BoardViewport); }
        static void Set(Text label, string value) { if (label != null && label.text != value) label.text = value; }
        static void Tint(Button button, bool selected) { button.image.color = selected ? new Color(.24f, .48f, .43f) : Dim; }
    }
}
