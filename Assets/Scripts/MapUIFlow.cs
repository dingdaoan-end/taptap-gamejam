using System.Collections;
using System.Linq;
using TapTapGameJam.Combat;
using TapTapGameJam.Music;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace TapTapGameJam.UI
{
    // Dialogue, battle results and level preparation share the existing music clock.
    [DefaultExecutionOrder(-100)]
    public sealed class MapUIFlow : MonoBehaviour
    {
        public enum Phase { Dialogue, Preparation, Battle, Result, Paused, Transition }
        public DialoguePlayer dialogue;
        public LevelDialogueSet[] levelDialogues;
        public BuildSelection selection;
        public MusicClock clock;
        public EnemyAudioTestSpawner[] spawners;
        public Button startBattleButton;
        [FormerlySerializedAs("battleControlsRoot")] public GameObject pauseMenuRoot;
        public Button restartButton;
        public Button skipLevelButton;
        public Button quitButton;
        [SerializeField] AudioSource menuClickSound;
        public GameObject resultRoot;
        [SerializeField] Button retryButton;
        [SerializeField] Button nextLevelButton;
        [SerializeField] Button replayLevelButton;
        public GameObject transitionRoot;
        [Header("Level camera transition")]
        [SerializeField, Min(0.1f)] float cameraMoveDuration = 1.2f;
        public TMP_Text levelText;
        public TMP_Text healthText;
        public TMP_Text waveText;
        public GameObject bellPrefab;
        public GameObject chordPrefab;
        public GameObject dudukPrefab;
        [Header("Level 3 and 4 prefab variants")]
        public GameObject level3BellPrefab;
        public GameObject level3ChordPrefab;
        [SerializeField, Min(0)] int firstLevelTowerLimit = 3;
        const int MaximumTowerLimit = 6;
        int highestClearedLevel;
        public Phase CurrentPhase { get; private set; } = Phase.Dialogue;
        public int CurrentLevel { get; private set; } = 1;
        readonly BaseHealthState baseHealth = new BaseHealthState();
        [SerializeField] Image[] healthCells = new Image[BaseHealthState.Maximum];
        [Header("Authored HUD tower capacity")]
        [SerializeField] Image[] towerCapacityIcons = new Image[MaximumTowerLimit];
        [SerializeField, Range(0, 1)] float occupiedTowerIconAlpha = 0.2f;
        public int CurrentBaseHealth => baseHealth.Current;
        public int MaxBaseHealth => BaseHealthState.Maximum;
        public Button RetryButton => retryButton;
        public Button NextLevelButton => nextLevelButton;
        public Button ReplayLevelButton => replayLevelButton;
        public bool LevelWon { get; private set; }
        EnemyAudioTestSpawner[] allSpawners;
        EnemyWaveGroupGate waveGroupGate;
        float timeScaleBeforePause = 1;
        bool ownsPause;
        Phase phaseBeforePause = Phase.Preparation;
        bool dialogueVisibleBeforePause, resultVisibleBeforePause, clockPausedBeforePause, quitting;
        string waveTextBeforePause;
        GameObject selectionBeforePause;
        int lastSkipFrame = -1;
        Camera movingCamera;
        Vector3 cameraMoveStart, cameraMoveTarget;
        float cameraSizeStart, cameraSizeTarget;
        float cameraMoveElapsed, activeCameraMoveDuration;

        bool IsPlayablePhase => CurrentPhase == Phase.Preparation || CurrentPhase == Phase.Battle;

        void Awake()
        {
            selection.MaxTowerCount = Mathf.Clamp(firstLevelTowerLimit, 0, MaximumTowerLimit);
            allSpawners = FindObjectsOfType<EnemyAudioTestSpawner>(true)
                .Where(s => s.gameObject.scene == gameObject.scene).ToArray();
            foreach (var spawner in allSpawners) spawner.enabled = false;
            if (clock != null) clock.ResetBellLoopAssignments();
            selection.SetInteractionAllowed(false);
            SetSpawners(false);
            if (resultRoot != null) resultRoot.SetActive(false);
            if (transitionRoot != null) transitionRoot.SetActive(false);
            startBattleButton.gameObject.SetActive(false);
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
            selection.SetUnlockedPrefabs(new[] { bellPrefab });
            UpdateTowerVariants();
            RefreshBaseHealth();
            RefreshTowerCapacity();
        }
        void OnEnable()
        {
            if (dialogue != null) dialogue.PlayingChanged += OnDialoguePlayingChanged;
            OnDialoguePlayingChanged(dialogue != null && dialogue.IsPlaying);
            if (startBattleButton != null) startBattleButton.onClick.AddListener(StartBattle);
            if (restartButton != null) restartButton.onClick.AddListener(RestartCurrentLevel);
            if (skipLevelButton != null) skipLevelButton.onClick.AddListener(SkipCurrentLevel);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
            if (RetryButton != null) RetryButton.onClick.AddListener(RestartCurrentLevel);
            if (NextLevelButton != null) NextLevelButton.onClick.AddListener(NextLevel);
            if (replayLevelButton != null) replayLevelButton.onClick.AddListener(RestartCurrentLevel);
            if (allSpawners != null)
                foreach (var spawner in allSpawners)
                    if (spawner != null) spawner.EnemyArrived += OnEnemyArrived;
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (CurrentPhase == Phase.Transition) UpdateCameraTransition();
            // If audio finished loading while paused, pause its transport as well.
            if (CurrentPhase == Phase.Paused && clock != null && clock.IsRunning && !clock.IsPaused)
                clock.PausePlayback();
            RefreshBattleControls();
            if (CurrentPhase == Phase.Battle && waveGroupGate != null && clock != null && clock.IsRunning && !clock.IsPaused)
                waveGroupGate.TryAdvance(clock.CurrentBeat,
                    spawners.All(s => s != null && s.CurrentWaveGroupComplete),
                    spawners.Sum(s => s != null ? s.LivingCount : 0));
            CheckVictory();
        }

        void RefreshBattleControls()
        {
            bool paused = CurrentPhase == Phase.Paused;
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(paused);
            if (restartButton != null) restartButton.interactable = paused && phaseBeforePause != Phase.Transition && !quitting;
            if (skipLevelButton != null) skipLevelButton.interactable = paused && CanSkipCurrentLevel() && !quitting;
            if (quitButton != null) quitButton.interactable = paused && !quitting;
            if (startBattleButton != null) startBattleButton.gameObject.SetActive(CurrentPhase == Phase.Preparation);
        }

        void CheckVictory()
        {
            if (CurrentPhase != Phase.Battle || spawners == null || spawners.Length == 0) return;
            int planned = 0, spawned = 0, living = 0;
            foreach (var spawner in spawners)
            {
                if (spawner == null || spawner.PlannedCount == 0) return;
                planned += spawner.PlannedCount;
                spawned += spawner.SpawnedCount;
                living += spawner.LivingCount;
            }
            waveText.text = "已出场：" + spawned + " / " + planned + "　剩余：" + living;
            if (WaveCompletionRules.HasWon(baseHealth.Current, planned, spawned, living)) ShowResult(true);
        }
        void Start()
        {
            if (clock != null) { clock.SetLevel(CurrentLevel); clock.EnsurePlayback(); }
            levelText.text = "第一关";
            RefreshBaseHealth();
            waveText.text = "准备阶段";
            var intro = levelDialogues != null && levelDialogues.Length > 0 && levelDialogues[0] != null
                ? levelDialogues[0].intro : null;
            if (!dialogue.Play(intro, OnIntroCompleted))
                Debug.LogError("Map UI flow remains locked until its opening dialogue is configured.", this);
        }
        void OnIntroCompleted(DialogueSequence sequence)
        {
            if (CurrentPhase != Phase.Dialogue || levelDialogues == null || levelDialogues.Length == 0 ||
                sequence != levelDialogues[0].intro) return;
            EnterPreparation();
        }
        void OnDialoguePlayingChanged(bool playing)
        {
            if (clock != null) clock.SetDialogueActive(playing);
        }
        public void StartBattle()
        {
            if (!isActiveAndEnabled || CurrentPhase != Phase.Preparation || clock == null) return;
            CurrentPhase = Phase.Battle;
            LevelWon = false;
            RefreshBattleControls();
            startBattleButton.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            selection.CancelBuild();
            selection.SetInteractionAllowed(true);
            waveText.text = "战斗中";
            SetSpawners(true);
            clock.EnsurePlayback();
        }
        public void TogglePause()
        {
            if (!isActiveAndEnabled || quitting) return;
            if (CurrentPhase != Phase.Paused)
            {
                phaseBeforePause = CurrentPhase;
                waveTextBeforePause = waveText.text;
                selectionBeforePause = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                dialogueVisibleBeforePause = dialogue != null && dialogue.panel != null && dialogue.panel.activeSelf;
                resultVisibleBeforePause = resultRoot != null && resultRoot.activeSelf;
                if (dialogueVisibleBeforePause) dialogue.panel.SetActive(false);
                if (resultVisibleBeforePause) resultRoot.SetActive(false);
                clockPausedBeforePause = clock != null && clock.IsPaused;
                CurrentPhase = Phase.Paused;
                selection.SetInteractionAllowed(false);
                if (clock != null) clock.PausePlayback();
                timeScaleBeforePause = Time.timeScale;
                ownsPause = true;
                Time.timeScale = 0;
                waveText.text = "已暂停";
                if (pauseMenuRoot != null) pauseMenuRoot.transform.SetAsLastSibling();
            }
            else if (CurrentPhase == Phase.Paused)
            {
                RestoreTimeScale();
                if (clock != null && !clockPausedBeforePause) clock.EnsurePlayback();
                CurrentPhase = phaseBeforePause;
                selection.SetInteractionAllowed(IsPlayablePhase);
                if (dialogueVisibleBeforePause && dialogue != null && dialogue.IsPlaying) dialogue.panel.SetActive(true);
                if (resultVisibleBeforePause && resultRoot != null) resultRoot.SetActive(true);
                waveText.text = waveTextBeforePause;
            }
            RefreshBattleControls();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(CurrentPhase == Phase.Paused
                    ? (restartButton != null && restartButton.interactable ? restartButton.gameObject : quitButton?.gameObject)
                    : (selectionBeforePause != null && selectionBeforePause.activeInHierarchy ? selectionBeforePause : null));
        }
        public void RestartCurrentLevel()
        {
            if (!isActiveAndEnabled || quitting || (CurrentPhase == Phase.Paused && phaseBeforePause == Phase.Transition) ||
                (!IsPlayablePhase && CurrentPhase != Phase.Paused && CurrentPhase != Phase.Result)) return;
            PlayMenuClick();
            if (dialogue != null) dialogue.Cancel();
            CurrentPhase = Phase.Preparation;
            ResetBattleState();
            EnterPreparation();
        }

        // Cleanup does not enable preparation controls; camera transitions share it with retries.
        void ResetBattleState()
        {
            selection.SetInteractionAllowed(false);
            RestoreTimeScale();
            SetSpawners(false);
            // Runtime slots own separately rooted tower instances. Clear both, retaining authored map objects.
            foreach (var slot in FindObjectsOfType<BuildSlot>(true))
            {
                if (slot.gameObject.scene != gameObject.scene || !slot.GeneratedPlacement) continue;
                slot.TryDestroyTower();
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
            if (clock != null)
            {
                clock.ResetBellLoopAssignments();
                clock.EnsurePlayback();
            }
            LevelWon = false;
            baseHealth.Reset();
            RefreshBaseHealth();
            if (resultRoot != null) resultRoot.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void EnterPreparation()
        {
            CurrentPhase = Phase.Preparation;
            waveText.text = "建造准备";
            selection.SetInteractionAllowed(true);
            if (clock != null) clock.EnsurePlayback();
            RefreshBattleControls();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        void RestoreTimeScale()
        {
            if (!ownsPause) return;
            Time.timeScale = timeScaleBeforePause;
            ownsPause = false;
        }

        void OnEnemyArrived(EnemyPathMover enemy)
        {
            if (enemy == null || !enemy.DamagesBaseOnArrival) return;
            if (CurrentPhase != Phase.Battle || !baseHealth.LoseLife()) return;
            RefreshBaseHealth();
            if (!baseHealth.IsDefeated) return;
            ShowResult(false);
        }

        void ShowResult(bool won)
        {
            // Change phase first so other arrivals in this frame cannot re-enter defeat.
            CurrentPhase = Phase.Result;
            LevelWon = won;
            if (won) RecordLevelCompletion();
            selection.SetInteractionAllowed(false);
            SetSpawners(false);
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
            startBattleButton.gameObject.SetActive(false);
            waveText.text = won ? "本关已通关" : "基地已被攻破";
            if (RetryButton != null) RetryButton.gameObject.SetActive(!won);
            if (NextLevelButton != null) NextLevelButton.gameObject.SetActive(won && CurrentLevel < 4);
            if (replayLevelButton != null) replayLevelButton.gameObject.SetActive(won);
            if (resultRoot != null)
            {
                var title = resultRoot.transform.Find("ResultTitleText")?.GetComponent<TMP_Text>();
                var body = resultRoot.transform.Find("ResultBodyText")?.GetComponent<TMP_Text>();
                if (title != null) title.text = won ? "游戏胜利" : "游戏失败";
                if (body != null) body.text = won
                    ? "本关敌人已全部结算。\n基地剩余生命：" + baseHealth.Current + " / " + MaxBaseHealth
                    : "基地生命已耗尽。\n调整防御，再试一次！";
                resultRoot.transform.SetAsLastSibling();
                resultRoot.SetActive(true);
            }
            var button = won ? (CurrentLevel < 4 ? NextLevelButton : replayLevelButton) : RetryButton;
            if (EventSystem.current != null && button != null && button.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        void RecordLevelCompletion()
        {
            // Normal victories and skips grant the same one-time progress reward.
            highestClearedLevel = Mathf.Max(highestClearedLevel, CurrentLevel);
            selection.MaxTowerCount = Mathf.Clamp(firstLevelTowerLimit + highestClearedLevel, 0, MaximumTowerLimit);
            RefreshTowerCapacity();
        }

        bool CanSkipCurrentLevel()
        {
            return isActiveAndEnabled && !quitting && (IsPlayablePhase ||
                (CurrentPhase == Phase.Paused && phaseBeforePause != Phase.Transition)) &&
                CurrentLevel < 4 && allSpawners != null &&
                allSpawners.Any(s => s != null && s.Level == CurrentLevel + 1);
        }

        public void SkipCurrentLevel()
        {
            if (!CanSkipCurrentLevel() || lastSkipFrame == Time.frameCount) return;
            PlayMenuClick();
            if (dialogue != null) dialogue.Cancel();
            lastSkipFrame = Time.frameCount;
            int next = CurrentLevel + 1;
            var nextSpawners = allSpawners.Where(s => s != null && s.Level == next).ToArray();
            // Enter the existing transition gate without showing results or starting dialogue.
            // Leaving Battle first prevents arrivals from triggering defeat during cleanup.
            CurrentPhase = Phase.Dialogue;
            RecordLevelCompletion();
            EnterNextLevel(next, nextSpawners);
        }

        public void NextLevel()
        {
            if (!isActiveAndEnabled || CurrentPhase != Phase.Result || !LevelWon || CurrentLevel >= 4) return;
            int next = CurrentLevel + 1;
            var nextSpawners = allSpawners.Where(s => s != null && s.Level == next).ToArray();
            if (nextSpawners.Length == 0) return;
            var victory = levelDialogues != null && CurrentLevel <= levelDialogues.Length && levelDialogues[CurrentLevel - 1] != null
                ? levelDialogues[CurrentLevel - 1].victory : null;
            CurrentPhase = Phase.Dialogue;
            resultRoot.SetActive(false);
            if (dialogue != null && victory != null && victory.IsValid)
                dialogue.Play(victory, _ => EnterNextLevel(next, nextSpawners));
            else EnterNextLevel(next, nextSpawners);
        }

        void EnterNextLevel(int level, EnemyAudioTestSpawner[] nextSpawners)
        {
            if (!isActiveAndEnabled || CurrentPhase != Phase.Dialogue || level != CurrentLevel + 1) return;
            // Both normal victory and skip enter the same locked camera transition.
            CurrentPhase = Phase.Transition;
            RefreshBattleControls();
            ResetBattleState();
            CurrentLevel = level;
            UpdateTowerVariants();
            RefreshTowerCapacity();
            if (clock != null) clock.SetLevel(CurrentLevel);
            spawners = nextSpawners;
            SetSpawners(false);
            levelText.text = new[] { "第一关", "第二关", "第三关", "第四关" }[level - 1];
            selection.SetUnlockedPrefabs(level == 2 ? new[] { bellPrefab, chordPrefab } : new[] { bellPrefab, chordPrefab, dudukPrefab });
            waveText.text = "前往下一关";
            BeginCameraTransition();
        }

        void UpdateTowerVariants()
        {
            selection.SetPrefabReplacement(bellPrefab, CurrentLevel >= 3 ? level3BellPrefab : null);
            selection.SetPrefabReplacement(chordPrefab, CurrentLevel >= 3 ? level3ChordPrefab : null);
        }

        void PlayMenuClick()
        {
            if (menuClickSound != null && menuClickSound.clip != null) menuClickSound.Play();
        }

        public void QuitGame()
        {
            if (!isActiveAndEnabled || quitting || CurrentPhase != Phase.Paused) return;
            StartCoroutine(QuitAfterClick());
        }

        IEnumerator QuitAfterClick()
        {
            quitting = true;
            RefreshBattleControls();
            PlayMenuClick();
            if (menuClickSound != null && menuClickSound.clip != null)
                yield return new WaitForSecondsRealtime(menuClickSound.clip.length / Mathf.Max(.01f, Mathf.Abs(menuClickSound.pitch)) + .03f);
            RestoreTimeScale();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        void BeginCameraTransition()
        {
            var camera = selection.WorldCamera;
            movingCamera = null;
            // A scene without a usable camera has no movement to wait for.
            if (camera == null || !camera.orthographic || spawners.Length == 0 || spawners[0].spawnPoint == null)
            {
                EnterPreparation();
                return;
            }
            var bounds = new Bounds(spawners[0].spawnPoint.position, Vector3.zero);
            foreach (var spawner in spawners)
            {
                if (spawner.spawnPoint != null) bounds.Encapsulate(spawner.spawnPoint.position);
                if (spawner.route == null) continue;
                for (int i = 0; i < spawner.route.Count; i++) bounds.Encapsulate((Vector3)spawner.route.Point(i));
            }
            movingCamera = camera;
            cameraMoveStart = camera.transform.position;
            cameraMoveTarget = new Vector3(bounds.center.x, bounds.center.y, cameraMoveStart.z);
            cameraSizeStart = camera.orthographicSize;
            cameraSizeTarget = Mathf.Max(bounds.extents.y + 3, (bounds.extents.x + 3) / Mathf.Max(.1f, camera.aspect));
            cameraMoveElapsed = 0;
            activeCameraMoveDuration = Mathf.Max(0.1f, cameraMoveDuration);
        }

        void UpdateCameraTransition()
        {
            if (movingCamera != null)
            {
                // Unscaled time also supports skipping a level from the paused state.
                cameraMoveElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, cameraMoveElapsed / activeCameraMoveDuration);
                movingCamera.transform.position = Vector3.Lerp(cameraMoveStart, cameraMoveTarget, t);
                movingCamera.orthographicSize = Mathf.Lerp(cameraSizeStart, cameraSizeTarget, t);
                if (cameraMoveElapsed < activeCameraMoveDuration) return;
                movingCamera.transform.position = cameraMoveTarget;
                movingCamera.orthographicSize = cameraSizeTarget;
            }
            movingCamera = null;
            EnterPreparation();
        }

        void RefreshBaseHealth()
        {
            if (healthCells == null) return;
            for (int i = 0; i < healthCells.Length; i++)
                if (healthCells[i] != null)
                    healthCells[i].enabled = i < baseHealth.Current;
        }
        // LateUpdate also observes builds/demolition triggered by UI clicks this frame.
        void LateUpdate() { RefreshTowerCapacity(); }
        void RefreshTowerCapacity()
        {
            if (selection == null || towerCapacityIcons == null) return;
            int built = selection.BuiltTowerCount;
            int limit = selection.MaxTowerCount;
            int available = Mathf.Max(0, limit - built);
            for (int i = 0; i < towerCapacityIcons.Length; i++)
            {
                var icon = towerCapacityIcons[i];
                if (icon == null) continue;
                bool visible = i < limit;
                if (icon.gameObject.activeSelf != visible) icon.gameObject.SetActive(visible);
                var color = icon.color;
                color.a = i < available ? 1f : occupiedTowerIconAlpha;
                if (icon.color != color) icon.color = color;
            }
        }
        void SetSpawners(bool active)
        {
            waveGroupGate = null;
            if (spawners == null) return;
            if (active && clock != null)
            {
                double startBeat = EnemyBeatTiming.FirstBirth(clock.CurrentBeat, 1);
                if (CurrentLevel == 2 && spawners.Length > 0 && spawners.All(s => s != null && s.HasAuthoredWave))
                    waveGroupGate = new EnemyWaveGroupGate(startBeat, 16, 16,
                        (spawners.Max(s => s.LastAuthoredBeat) - 1) / 32 + 1);
                foreach (var spawner in spawners)
                    if (spawner != null && spawner.HasAuthoredWave) spawner.BeginWaveAt(startBeat, waveGroupGate);
            }
            foreach (var spawner in spawners) if (spawner != null) spawner.enabled = active;
        }
        void OnDisable()
        {
            if (startBattleButton != null) startBattleButton.onClick.RemoveListener(StartBattle);
            if (restartButton != null) restartButton.onClick.RemoveListener(RestartCurrentLevel);
            if (skipLevelButton != null) skipLevelButton.onClick.RemoveListener(SkipCurrentLevel);
            if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
            if (RetryButton != null) RetryButton.onClick.RemoveListener(RestartCurrentLevel);
            if (NextLevelButton != null) NextLevelButton.onClick.RemoveListener(NextLevel);
            if (replayLevelButton != null) replayLevelButton.onClick.RemoveListener(RestartCurrentLevel);
            if (allSpawners != null)
                foreach (var spawner in allSpawners)
                    if (spawner != null) spawner.EnemyArrived -= OnEnemyArrived;
            RestoreTimeScale();
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
            if (selection != null) selection.SetInteractionAllowed(false);
            if (dialogue != null)
            {
                dialogue.Cancel();
                dialogue.PlayingChanged -= OnDialoguePlayingChanged;
            }
            OnDialoguePlayingChanged(false);
            SetSpawners(false);
            if (clock != null) clock.StopPlayback();
        }
    }
}
