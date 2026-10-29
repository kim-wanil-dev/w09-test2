using System.Collections.Generic;
using System.Text;

using UnityEngine;

using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BackpackDefense
{
    /// <summary>
    /// Displays and edits the prototype board with shape and path previews.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float CELL_SIZE = 64f;

        [Header("Economy")]
        [SerializeField] private EconomySettings _economySettings = new EconomySettings();
        [SerializeField] private int _shopSeed = 42;

        [Header("Synergy")]
        [SerializeField] private SynergySettings _synergySettings = new SynergySettings();

        [Header("Combat")]
        [SerializeField] private CombatSettings _combatSettings = new CombatSettings();

        [Header("Towers")]
        [SerializeField] private List<TowerShape> _towerDefinitions = new List<TowerShape>(TowerCatalog.CreateShapes());

        private BoardModel _board;
        private readonly UnityEngine.UI.Image[,] _cells = new UnityEngine.UI.Image[BoardModel.WIDTH, BoardModel.HEIGHT];
        private readonly TextMeshProUGUI[,] _anchorLabels = new TextMeshProUGUI[BoardModel.WIDTH, BoardModel.HEIGHT];
        private readonly TextMeshProUGUI[] _shopLabels = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] _benchLabels = new TextMeshProUGUI[6];
        private readonly int[] _shopRotations = new int[4];
        private readonly List<UnityEngine.UI.Button> _economyButtons = new List<UnityEngine.UI.Button>();
        private readonly Dictionary<int, EnemyMarker> _enemyMarkers = new Dictionary<int, EnemyMarker>();
        private readonly Dictionary<int, RectTransform> _impactMarkers = new Dictionary<int, RectTransform>();
        private IReadOnlyList<TowerShape> _shapes;
        private EconomyModel _economy;
        private CombatModel _combat;
        private RectTransform _boardRect;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _economyStatus;
        private TextMeshProUGUI _selectedStatus;
        private TextMeshProUGUI _synergyStatus;
        private TMP_FontAsset _font;
        private TowerUnit _selectedUnit;
        private TowerUnit _material;
        private int _rotation;
        private bool _isDragging;
        private bool _isFeeding;
        private bool _isSaleArmed;
        private PlacedTower _moving;
        private Vector2Int _grabOffset;
        private int _activeSeed;
        private CombatPhase _displayedPhase;
        private bool _hasDisplayedPhase;
        private UnityEngine.UI.Button _advanceButton;
        private UnityEngine.UI.Button _pauseButton;
        private UnityEngine.UI.Button _speedButton;
        private UnityEngine.UI.Button _retryButton;
        private UnityEngine.UI.Button _newRunButton;
        private TextMeshProUGUI _advanceLabel;
        private TextMeshProUGUI _pauseLabel;
        private TextMeshProUGUI _speedLabel;

        private sealed class EnemyMarker
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Image;
            public TextMeshProUGUI Label;
            public RectTransform HealthFill;
        }

        void Start()
        {
            _shapes = _towerDefinitions;
            _activeSeed = _shopSeed;
            _board = new BoardModel();
            _economy = new EconomyModel(_board, _shapes, _activeSeed, _economySettings);
            _combat = new CombatModel(_activeSeed, _combatSettings);
            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            CreateInterface();
            HandlePhaseChange();
        }

        void Update()
        {
            if (_combat == null) return;
            bool advanced = _combat.Advance(Time.unscaledDeltaTime);
            if (advanced) RenderCombatEntities();
            if (!_hasDisplayedPhase || _displayedPhase != _combat.Phase) HandlePhaseChange();
            UpdateCombatHud();
            if (Mouse.current == null || Keyboard.current == null) return;
            if (_combat.Phase == CombatPhase.Combat && Keyboard.current.spaceKey.wasPressedThisFrame)
                ToggleCombatPause();
            if (_combat.Phase == CombatPhase.Preparation && Keyboard.current.enterKey.wasPressedThisFrame)
                AdvancePhase();
            if (_combat.Phase != CombatPhase.Preparation)
            {
                Vector2 combatPointer = Mouse.current.position.ReadValue();
                if (Mouse.current.rightButton.wasPressedThisFrame && TryGetCell(combatPointer, out Vector2Int inspectCell))
                {
                    PlacedTower inspectTower = _board.FindTower(inspectCell);
                    SelectUnit(inspectTower == null ? null : _economy.FindUnit(inspectTower));
                    Refresh(null, false, null);
                }
                return;
            }
            if (!_isDragging && _isFeeding && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelFeed();
                Refresh(null, false, null);
                return;
            }
            Vector2 pointer = Mouse.current.position.ReadValue();
            bool onBoard = TryGetCell(pointer, out Vector2Int cell);
            if (!_isDragging && Mouse.current.leftButton.wasPressedThisFrame && onBoard)
            {
                _moving = _board.FindTower(cell);
                TowerUnit clicked = _moving == null ? null : _economy.FindUnit(_moving);
                if (_isFeeding)
                {
                    SelectMaterial(clicked);
                    _moving = null;
                    return;
                }
                if (_moving != null)
                {
                    SelectUnit(clicked);
                    _rotation = _moving.Rotation;
                    _grabOffset = cell - _moving.Position;
                }
                else _grabOffset = Vector2Int.zero;
                _isDragging = _selectedUnit != null && (_moving != null || _selectedUnit.Placement == null);
            }
            if (_isDragging && Keyboard.current.rKey.wasPressedThisFrame)
            {
                _rotation = (_rotation + 1) % 4;
                _grabOffset = new Vector2Int(-_grabOffset.y, _grabOffset.x);
            }
            if (_isDragging && (Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
            {
                _isDragging = false;
                _moving = null;
                Refresh(null, false, null);
                return;
            }
            if (_isDragging && onBoard)
            {
                Vector2Int anchor = cell - _grabOffset;
                TowerShape shape = _shapes[_selectedUnit.TypeIndex];
                bool valid = _board.TryPreview(shape, anchor, _rotation, _moving, out List<Vector2Int> path, out string reason);
                if (_moving == null && _board.Towers.Count >= _economy.Level)
                {
                    valid = false;
                    reason = "Deployment limit reached";
                }
                PlacedTower previewPlacement = valid ? new PlacedTower(shape, anchor, _rotation) : null;
                Refresh(path, valid, shape.GetCells(anchor, _rotation), previewPlacement);
                if (anchor.x >= 0 && anchor.x < BoardModel.WIDTH && anchor.y >= 0 && anchor.y < BoardModel.HEIGHT)
                    _anchorLabels[anchor.x, anchor.y].text = "A";
                _status.text = valid ? "Drag to place / R: rotate / Esc: cancel  |  Path: " + (path.Count - 1) : reason;
            }
            if (_isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (onBoard) _economy.Place(_selectedUnit, cell - _grabOffset, _rotation);
                _isDragging = false;
                _moving = null;
                Refresh(null, false, null);
            }
            if (!_isDragging && onBoard && Mouse.current.rightButton.wasPressedThisFrame)
            {
                PlacedTower tower = _board.FindTower(cell);
                TowerUnit unit = tower == null ? null : _economy.FindUnit(tower);
                if (_isFeeding) SelectMaterial(unit);
                else { SelectUnit(unit); Refresh(null, false, null); }
            }
        }

        /// <summary>
        /// Creates the canvas, tower selectors, grid cells, and input system for the board.
        /// </summary>
        private void CreateInterface()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
            }
            var canvasObject = new GameObject("Board Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _boardRect = CreateRect(canvas.transform, "Board", new Vector2(640, 512), new Vector2(100, 0));
            for (int y = 0; y < BoardModel.HEIGHT; y++)
            for (int x = 0; x < BoardModel.WIDTH; x++)
            {
                RectTransform rect = CreateRect(_boardRect, "Cell", new Vector2(CELL_SIZE - 2, CELL_SIZE - 2),
                    new Vector2((x - 4.5f) * CELL_SIZE, (y - 3.5f) * CELL_SIZE));
                var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.raycastTarget = false;
                _cells[x, y] = image;
                _anchorLabels[x, y] = CreateText(rect, "", 20);
            }

            for (int i = 0; i < _shopLabels.Length; i++)
            {
                int index = i;
                _shopLabels[i] = CreateButton(canvas.transform, new Vector2(-650, 250 - i * 65), new Vector2(300, 58),
                    () => { CancelFeed(); bool success = _economy.Buy(index); Refresh(null, false, null); if (!success) _status.text = "Purchase unavailable: check gold, offer, or bench space"; });
                CreateButton(canvas.transform, new Vector2(-470, 250 - i * 65), new Vector2(40, 58),
                    () => { CancelFeed(); if (_economy.Offers[index] >= 0) _shopRotations[index] = (_shopRotations[index] + 1) % 4; Refresh(null, false, null); }).text = "R";
            }
            for (int i = 0; i < _benchLabels.Length; i++)
            {
                int index = i;
                _benchLabels[i] = CreateButton(canvas.transform, new Vector2(-210 + i * 125, -335), new Vector2(120, 65),
                    () => { TowerUnit unit = GetBenchUnit(index); if (_isFeeding) SelectMaterial(unit); else { SelectUnit(unit); Refresh(null, false, null); } });
                _benchLabels[i].fontSize = 15;
                var dragHandler = _benchLabels[i].transform.parent.gameObject.AddComponent<BenchDragHandler>();
                dragHandler.Initialize(this, index);
            }
            CreateButton(canvas.transform, new Vector2(-650, -60), new Vector2(300, 48),
                () => { CancelFeed(); bool success = _economy.Reroll(); if (success) System.Array.Clear(_shopRotations, 0, _shopRotations.Length); Refresh(null, false, null); if (!success) _status.text = "Reroll unavailable: unlock shop or obtain gold"; }).text = "REROLL (" + _economy.RerollCost + ")";
            CreateButton(canvas.transform, new Vector2(-650, -115), new Vector2(300, 48),
                () => { CancelFeed(); _economy.ToggleLock(); Refresh(null, false, null); }).text = "TOGGLE LOCK";
            CreateButton(canvas.transform, new Vector2(650, 250), new Vector2(260, 48),
                () => { CancelFeed(); bool success = _economy.LevelUp(); Refresh(null, false, null); if (!success) _status.text = "Level up unavailable: maximum level or insufficient gold"; }).text = "LEVEL UP";
            CreateButton(canvas.transform, new Vector2(650, 190), new Vector2(260, 48),
                () => { CancelFeed(); bool success = _selectedUnit != null && _economy.ReturnToBench(_selectedUnit); Refresh(null, false, null); if (!success) _status.text = "Select a deployed tower and make room on the bench"; }).text = "RETURN TO BENCH";
            CreateButton(canvas.transform, new Vector2(650, 130), new Vector2(260, 48), SellSelected).text = "SELL";
            CreateButton(canvas.transform, new Vector2(650, 70), new Vector2(260, 48),
                () => { _isFeeding = _selectedUnit != null && _selectedUnit.FeedCount < 5; _material = null; Refresh(null, false, null); if (!_isFeeding) _status.text = "Select a tower below 3 stars first"; }).text = "SELECT MATERIAL";
            CreateButton(canvas.transform, new Vector2(650, 10), new Vector2(260, 48), ConfirmFeed).text = "CONFIRM FEED";
            _advanceLabel = CreateButton(canvas.transform, new Vector2(650, 420), new Vector2(260, 48), AdvancePhase, false);
            _advanceButton = _advanceLabel.GetComponentInParent<UnityEngine.UI.Button>();
            _pauseLabel = CreateButton(canvas.transform, new Vector2(650, 360), new Vector2(125, 48), ToggleCombatPause, false);
            _pauseButton = _pauseLabel.GetComponentInParent<UnityEngine.UI.Button>();
            _speedLabel = CreateButton(canvas.transform, new Vector2(785, 360), new Vector2(125, 48), ToggleCombatSpeed, false);
            _speedButton = _speedLabel.GetComponentInParent<UnityEngine.UI.Button>();
            TextMeshProUGUI retryLabel = CreateButton(canvas.transform, new Vector2(650, 420), new Vector2(260, 48), RetrySameSeed, false);
            _retryButton = retryLabel.GetComponentInParent<UnityEngine.UI.Button>();
            TextMeshProUGUI newRunLabel = CreateButton(canvas.transform, new Vector2(650, 360), new Vector2(260, 48), StartNewSeed, false);
            _newRunButton = newRunLabel.GetComponentInParent<UnityEngine.UI.Button>();
            retryLabel.text = "RETRY SAME SEED";
            newRunLabel.text = "NEW RUN / SEED";
            _economyStatus = CreateText(CreateRect(canvas.transform, "Economy Status", new Vector2(1000, 50), new Vector2(0, 410)), "", 24);
            RectTransform synergyRect = CreateRect(canvas.transform, "Synergy Status", new Vector2(500, 280), new Vector2(-650, -275));
            UnityEngine.UI.Image synergyBackground = synergyRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            synergyBackground.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
            synergyBackground.raycastTarget = false;
            _synergyStatus = CreateText(synergyRect, "", 18);
            _synergyStatus.alignment = TextAlignmentOptions.TopLeft;
            _synergyStatus.margin = new Vector4(12f, 10f, 10f, 8f);
            RectTransform selectedRect = CreateRect(canvas.transform, "Selected Status", new Vector2(460, 360), new Vector2(710, -240));
            UnityEngine.UI.Image selectedBackground = selectedRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            selectedBackground.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
            selectedBackground.raycastTarget = false;
            _selectedStatus = CreateText(selectedRect, "", 18);
            _selectedStatus.alignment = TextAlignmentOptions.TopLeft;
            _selectedStatus.margin = new Vector4(12f, 10f, 10f, 8f);
            RectTransform statusRect = CreateRect(canvas.transform, "Status", new Vector2(1400, 70), new Vector2(0, -430));
            _status = CreateText(statusRect, "", 24);
        }

        /// <summary>
        /// Creates an interactive button with a text label at the requested position.
        /// </summary>
        private TextMeshProUGUI CreateButton(Transform parent, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action, bool isEconomyAction = true)
        {
            RectTransform rect = CreateRect(parent, "Button", size, position);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.18f, 0.24f, 0.31f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            if (isEconomyAction) _economyButtons.Add(button);
            return CreateText(rect, "", 20);
        }

        /// <summary>
        /// Recreates board, economy, and combat state for a deterministic run seed.
        /// </summary>
        private void InitializeRun(int seed)
        {
            _activeSeed = seed;
            _board = new BoardModel();
            _economy = new EconomyModel(_board, _shapes, seed, _economySettings);
            _combat = new CombatModel(seed, _combatSettings);
            _selectedUnit = null;
            _material = null;
            _moving = null;
            _rotation = 0;
            _isDragging = false;
            _isFeeding = false;
            _isSaleArmed = false;
            System.Array.Clear(_shopRotations, 0, _shopRotations.Length);
            _hasDisplayedPhase = false;
            HandlePhaseChange();
        }

        /// <summary>
        /// Starts a frozen-stat wave or advances a survived result into the next preparation phase.
        /// </summary>
        private void AdvancePhase()
        {
            if (_combat.Phase == CombatPhase.Preparation)
            {
                BoardSynergyEvaluation evaluation = SynergyEvaluator.Evaluate(
                    _economy.Units, _shapes, _board.GetPath(), _synergySettings);
                if (!_combat.StartWave(evaluation, _board.GetPath()))
                {
                    _status.text = "Wave could not start: check the board path and combat schedule";
                    return;
                }
                _isDragging = false;
                _moving = null;
                CancelFeed();
                _economy.BeginCombat();
            }
            else if (_combat.Phase == CombatPhase.WaveResult)
            {
                int reward = _combat.ContinueAfterWave();
                _economy.EnterPreparation(reward);
            }
            HandlePhaseChange();
        }

        /// <summary>
        /// Toggles the fixed-step combat clock and refreshes its visible state.
        /// </summary>
        private void ToggleCombatPause()
        {
            _combat.TogglePause();
            UpdateControls();
            UpdateCombatHud();
        }

        /// <summary>
        /// Toggles the combat clock between one-times and two-times speed.
        /// </summary>
        private void ToggleCombatSpeed()
        {
            _combat.ToggleSpeed();
            UpdateControls();
            UpdateCombatHud();
        }

        /// <summary>
        /// Starts a fresh run with the same shop seed and reset board, economy, and base health.
        /// </summary>
        private void RetrySameSeed()
        {
            InitializeRun(_activeSeed);
        }

        /// <summary>
        /// Starts a fresh run with a newly generated shop seed.
        /// </summary>
        private void StartNewSeed()
        {
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            if (seed == _activeSeed) seed = seed == int.MaxValue - 1 ? 1 : seed + 1;
            InitializeRun(seed);
        }

        /// <summary>
        /// Updates button availability and labels for the current run phase.
        /// </summary>
        private void UpdateControls()
        {
            if (_combat == null || _advanceButton == null) return;
            bool preparing = _combat.Phase == CombatPhase.Preparation;
            bool showingResult = _combat.Phase == CombatPhase.WaveResult;
            bool inCombat = _combat.Phase == CombatPhase.Combat;
            bool ended = _combat.Phase == CombatPhase.Victory || _combat.Phase == CombatPhase.Defeat;
            foreach (UnityEngine.UI.Button button in _economyButtons)
                button.interactable = preparing;
            _advanceButton.gameObject.SetActive(preparing || showingResult);
            _advanceButton.interactable = preparing || showingResult;
            _advanceLabel.text = preparing ? "START WAVE" : "NEXT WAVE";
            _pauseButton.gameObject.SetActive(inCombat);
            _speedButton.gameObject.SetActive(inCombat);
            _retryButton.gameObject.SetActive(ended);
            _newRunButton.gameObject.SetActive(ended);
            _pauseButton.interactable = inCombat;
            _speedButton.interactable = inCombat;
            _pauseLabel.text = _combat.IsPaused ? "RESUME" : "PAUSE";
            _speedLabel.text = _combat.SimulationSpeed > 1.5f ? "2X" : "1X";
        }

        /// <summary>
        /// Refreshes visible state after the combat phase changes.
        /// </summary>
        private void HandlePhaseChange()
        {
            _displayedPhase = _combat.Phase;
            _hasDisplayedPhase = true;
            UpdateControls();
            Refresh(null, false, null);
            RenderCombatEntities();
            SetPhaseStatus();
        }

        /// <summary>
        /// Displays a concise instruction or completed-wave report for the current phase.
        /// </summary>
        private void SetPhaseStatus()
        {
            switch (_combat.Phase)
            {
                case CombatPhase.Preparation:
                    _status.text = "Preparation | Arrange towers, then START WAVE. Enter also starts the wave.";
                    break;
                case CombatPhase.Combat:
                    _status.text = "Wave " + _combat.CurrentWave + " in progress | Space toggles pause. Board and economy are locked.";
                    break;
                case CombatPhase.WaveResult:
                    WaveReport report = _combat.LastReport;
                    _status.text = "Wave " + report.WaveNumber + " clear | Path " + report.PathLength + " | Kills " + report.Kills + " | Leaks " +
                        report.Leaks + " | Base -" + report.BaseDamage + " (" + report.RemainingBaseHealth + " HP) | +" +
                        report.GoldReward + " gold after NEXT WAVE.";
                    break;
                case CombatPhase.Victory:
                    _status.text = "VICTORY | All " + _combat.WaveCount + " waves cleared with " +
                        _combat.BaseHealth + " base HP. Retry this seed or start a new run.";
                    break;
                case CombatPhase.Defeat:
                    string cause = _combat.DefeatCause == DefeatReason.BossReachedGoal
                        ? "The boss reached the goal." : "Base health reached zero.";
                    _status.text = "DEFEAT | " + cause + " Retry this seed or start a new run.";
                    break;
            }
        }

        /// <summary>
        /// Displays current wave, base health, economy, seed, time, and combat speed.
        /// </summary>
        private void UpdateCombatHud()
        {
            if (_combat == null || _economyStatus == null) return;
            int minutes = Mathf.FloorToInt(_combat.SimulationTime / 60f);
            int seconds = Mathf.FloorToInt(_combat.SimulationTime % 60f);
            string time = minutes.ToString("00") + ":" + seconds.ToString("00");
            string phase = _combat.Phase == CombatPhase.Combat ? " | " + time + " | " +
                (_combat.IsPaused ? "PAUSED" : (_combat.SimulationSpeed > 1.5f ? "2X" : "1X")) : "";
            _economyStatus.text = "Wave " + _combat.CurrentWave + "/" + _combat.WaveCount + "   Base " +
                _combat.BaseHealth + "/" + _combat.StartingBaseHealth + "   Gold " + _economy.Gold +
                "   Level " + _economy.Level + "   Towers " + _board.Towers.Count + "/" + _economy.Level +
                "   Seed " + _activeSeed + phase;
        }

        /// <summary>
        /// Creates or updates enemy and mortar-landing tokens on the two-dimensional board.
        /// </summary>
        private void RenderCombatEntities()
        {
            if (_combat == null) return;
            var activeEnemies = new HashSet<int>();
            foreach (EnemyInstance enemy in _combat.Enemies)
            {
                activeEnemies.Add(enemy.Sequence);
                if (!_enemyMarkers.TryGetValue(enemy.Sequence, out EnemyMarker marker))
                {
                    marker = CreateEnemyMarker();
                    _enemyMarkers.Add(enemy.Sequence, marker);
                }
                marker.Rect.anchoredPosition = CellToBoardPosition(enemy.Position);
                marker.Image.color = GetEnemyColor(enemy.Kind);
                marker.Label.text = GetEnemyLabel(enemy.Kind);
                marker.HealthFill.sizeDelta = new Vector2(30f * Mathf.Clamp01(enemy.Health / enemy.MaxHealth), 4f);
            }
            var removedEnemies = new List<int>();
            foreach (KeyValuePair<int, EnemyMarker> item in _enemyMarkers)
                if (!activeEnemies.Contains(item.Key)) removedEnemies.Add(item.Key);
            foreach (int id in removedEnemies)
            {
                Destroy(_enemyMarkers[id].Rect.gameObject);
                _enemyMarkers.Remove(id);
            }

            var activeImpacts = new HashSet<int>();
            foreach (PendingAttack impact in _combat.PendingAttacks)
            {
                activeImpacts.Add(impact.Sequence);
                if (!_impactMarkers.TryGetValue(impact.Sequence, out RectTransform marker))
                    _impactMarkers.Add(impact.Sequence, marker = CreateImpactMarker());
                marker.anchoredPosition = CellToBoardPosition(impact.ImpactPoint);
            }
            var removedImpacts = new List<int>();
            foreach (KeyValuePair<int, RectTransform> item in _impactMarkers)
                if (!activeImpacts.Contains(item.Key)) removedImpacts.Add(item.Key);
            foreach (int id in removedImpacts)
            {
                Destroy(_impactMarkers[id].gameObject);
                _impactMarkers.Remove(id);
            }
        }

        /// <summary>
        /// Creates a colored enemy token with a compact health bar.
        /// </summary>
        private EnemyMarker CreateEnemyMarker()
        {
            RectTransform rect = CreateRect(_boardRect, "Enemy", new Vector2(34f, 34f), Vector2.zero);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            TextMeshProUGUI label = CreateText(rect, "", 13);
            RectTransform healthBackground = CreateRect(rect, "Health Bar", new Vector2(30f, 4f), new Vector2(0f, -20f));
            var backgroundImage = healthBackground.gameObject.AddComponent<UnityEngine.UI.Image>();
            backgroundImage.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            backgroundImage.raycastTarget = false;
            RectTransform healthFill = CreateRect(healthBackground, "Health", new Vector2(30f, 4f), Vector2.zero);
            healthFill.anchorMin = new Vector2(0f, 0.5f);
            healthFill.anchorMax = new Vector2(0f, 0.5f);
            healthFill.pivot = new Vector2(0f, 0.5f);
            healthFill.anchoredPosition = new Vector2(-15f, 0f);
            var healthImage = healthFill.gameObject.AddComponent<UnityEngine.UI.Image>();
            healthImage.color = new Color(0.28f, 0.92f, 0.38f);
            healthImage.raycastTarget = false;
            return new EnemyMarker { Rect = rect, Image = image, Label = label, HealthFill = healthFill };
        }

        /// <summary>
        /// Creates a mortar marker at the fixed impact point while its attack is in flight.
        /// </summary>
        private RectTransform CreateImpactMarker()
        {
            RectTransform rect = CreateRect(_boardRect, "Mortar Impact", new Vector2(24f, 24f), Vector2.zero);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 0.48f, 0.1f, 0.75f);
            image.raycastTarget = false;
            CreateText(rect, "M", 14);
            return rect;
        }

        /// <summary>
        /// Converts a grid-space enemy position to the board's centered canvas coordinates.
        /// </summary>
        private static Vector2 CellToBoardPosition(Vector2 position)
        {
            return new Vector2((position.x - 4.5f) * CELL_SIZE, (position.y - 3.5f) * CELL_SIZE);
        }

        /// <summary>
        /// Returns the display color for one enemy movement profile.
        /// </summary>
        private static Color GetEnemyColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Fast: return new Color(0.96f, 0.78f, 0.18f);
                case EnemyKind.Tank: return new Color(0.68f, 0.48f, 0.30f);
                case EnemyKind.Boss: return new Color(0.83f, 0.18f, 0.23f);
                default: return new Color(0.82f, 0.85f, 0.9f);
            }
        }

        /// <summary>
        /// Returns the compact board label for one enemy movement profile.
        /// </summary>
        private static string GetEnemyLabel(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Fast: return "F";
                case EnemyKind.Tank: return "T";
                case EnemyKind.Boss: return "B";
                default: return "N";
            }
        }

        /// <summary>
        /// Returns the tower at the displayed bench index, or null for an empty slot.
        /// </summary>
        private TowerUnit GetBenchUnit(int index)
        {
            int position = 0;
            foreach (TowerUnit unit in _economy.Units)
            {
                if (unit.Placement != null) continue;
                if (position++ == index) return unit;
            }
            return null;
        }

        /// <summary>
        /// Starts dragging the owned tower in a bench slot without changing its inventory location.
        /// </summary>
        internal void BeginBenchDrag(int index)
        {
            if (_isFeeding || _combat == null || _combat.Phase != CombatPhase.Preparation) return;
            TowerUnit unit = GetBenchUnit(index);
            if (unit == null) return;
            SelectUnit(unit);
            _moving = null;
            _grabOffset = Vector2Int.zero;
            _isDragging = true;
            Refresh(null, false, null);
        }

        /// <summary>
        /// Completes a bench drag at the supplied screen position or restores the bench state.
        /// </summary>
        internal void EndBenchDrag(Vector2 screenPosition)
        {
            if (!_isDragging || _moving != null) return;
            if (_selectedUnit != null && TryGetCell(screenPosition, out Vector2Int cell))
                _economy.Place(_selectedUnit, cell - _grabOffset, _rotation);
            _isDragging = false;
            _moving = null;
            Refresh(null, false, null);
        }

        /// <summary>
        /// Selects an owned tower and resets pending material and sale choices.
        /// </summary>
        private void SelectUnit(TowerUnit unit)
        {
            _selectedUnit = unit;
            _isFeeding = false;
            _material = null;
            _isSaleArmed = false;
            _rotation = unit?.Placement?.Rotation ?? 0;
        }

        /// <summary>
        /// Selects a matching pristine material and shows the prospective path change.
        /// </summary>
        private void SelectMaterial(TowerUnit unit)
        {
            _material = _economy.CanFeed(_selectedUnit, unit) ? unit : null;
            List<Vector2Int> proposedPath = _material?.Placement == null ? null : _board.GetPathWithout(_material.Placement);
            Refresh(proposedPath, _material != null, null);
            if (_material == null) _status.text = "Choose a matching untouched 1-star tower";
            else
            {
                int oldLength = _board.GetPath().Count - 1;
                int newLength = _material.Placement == null ? oldLength : _board.GetPathWithout(_material.Placement).Count - 1;
                _status.text = "Confirm feed: multiplier " + _selectedUnit.DamageMultiplier.ToString("0.0") + " -> " +
                    _selectedUnit.NextDamageMultiplier.ToString("0.0") + " | Path " + oldLength + " -> " + newLength;
            }
        }

        /// <summary>
        /// Commits the selected single material and updates inventory and board previews.
        /// </summary>
        private void ConfirmFeed()
        {
            bool success = _selectedUnit != null && _material != null && _economy.Feed(_selectedUnit, _material);
            _isSaleArmed = false;
            _material = null;
            _isFeeding = false;
            Refresh(null, false, null);
            if (!success) _status.text = "Feed unavailable: select an eligible material";
        }

        /// <summary>
        /// Cancels any pending material selection before another economy transaction.
        /// </summary>
        private void CancelFeed()
        {
            _isFeeding = false;
            _material = null;
        }

        /// <summary>
        /// Sells the selection after an extra confirmation for upgraded towers.
        /// </summary>
        private void SellSelected()
        {
            CancelFeed();
            if (_selectedUnit == null) return;
            if (_selectedUnit.FeedCount > 0 && !_isSaleArmed)
            {
                _isSaleArmed = true;
                Refresh(null, false, null);
                _status.text = "Click SELL again to confirm refund of " + _selectedUnit.InvestedGold;
                return;
            }
            _economy.Sell(_selectedUnit);
            SelectUnit(null);
            Refresh(null, false, null);
        }

        /// <summary>
        /// Formats the tower footprint with A marking the attack anchor.
        /// </summary>
        private static string FormatShape(TowerShape shape, int rotation)
        {
            List<Vector2Int> cells = shape.GetCells(Vector2Int.zero, rotation);
            int minX = 0, maxX = 0, minY = 0, maxY = 0;
            foreach (Vector2Int cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }
            string result = "";
            for (int y = maxY; y >= minY; y--)
            {
                if (y < maxY) result += "/";
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, y);
                    result += cell == Vector2Int.zero ? "A" : cells.Contains(cell) ? "#" : ".";
                }
            }
            return result;
        }

        /// <summary>
        /// Counts owned field and bench copies of a catalog tower type.
        /// </summary>
        private int CountOwned(int typeIndex)
        {
            int count = 0;
            foreach (TowerUnit unit in _economy.Units)
                if (unit.TypeIndex == typeIndex) count++;
            return count;
        }

        /// <summary>
        /// Creates a centered UI rectangle with the given size and position.
        /// </summary>
        private static RectTransform CreateRect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        /// <summary>
        /// Creates a centered, non-interactive text element within a rectangle.
        /// </summary>
        private TextMeshProUGUI CreateText(RectTransform parent, string value, int size)
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Converts screen coordinates into a board cell if the pointer is over the board.
        /// </summary>
        private bool TryGetCell(Vector2 screen, out Vector2Int cell)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_boardRect, screen, null, out Vector2 local);
            int x = Mathf.FloorToInt((local.x + 320f) / CELL_SIZE);
            int y = Mathf.FloorToInt((local.y + 256f) / CELL_SIZE);
            cell = new Vector2Int(x, y);
            return x >= 0 && x < BoardModel.WIDTH && y >= 0 && y < BoardModel.HEIGHT;
        }

        /// <summary>
        /// Renders the committed board or a valid/invalid placement candidate and updates the path indicator.
        /// </summary>
        private void Refresh(List<Vector2Int> previewPath, bool valid, List<Vector2Int> previewCells,
            PlacedTower previewPlacement = null)
        {
            List<Vector2Int> committedPath = _board.GetPath();
            BoardSynergyEvaluation committedEvaluation = SynergyEvaluator.Evaluate(
                _economy.Units, _shapes, committedPath, _synergySettings);
            BoardSynergyEvaluation shownEvaluation = committedEvaluation;
            bool isSynergyPreview = false;
            if (previewPlacement != null && valid && _selectedUnit != null)
            {
                shownEvaluation = SynergyEvaluator.Evaluate(_economy.Units, _shapes,
                    previewPath ?? committedPath, _synergySettings, _selectedUnit, previewPlacement);
                isSynergyPreview = true;
            }
            else if (_isFeeding && _material != null && _selectedUnit != null)
            {
                shownEvaluation = SynergyEvaluator.Evaluate(_economy.Units, _shapes,
                    previewPath ?? committedPath, _synergySettings, feedTarget: _selectedUnit,
                    consumedMaterial: _material);
                isSynergyPreview = true;
            }

            for (int x = 0; x < BoardModel.WIDTH; x++)
            for (int y = 0; y < BoardModel.HEIGHT; y++)
            {
                _cells[x, y].color = new Color(0.13f, 0.16f, 0.2f);
                _anchorLabels[x, y].text = "";
            }
            List<Vector2Int> path = previewPath ?? _board.GetPath();
            foreach (Vector2Int point in path)
                _cells[point.x, point.y].color = new Color(0.25f, 0.48f, 0.5f);
            foreach (PlacedTower tower in _board.Towers)
            {
                if (_isDragging && tower == _moving) continue;
                if (_isFeeding && _material?.Placement == tower && previewPath != null) continue;
                bool isEligible = _isFeeding && _economy.CanFeed(_selectedUnit, _economy.FindUnit(tower));
                TowerUnit unit = _economy.FindUnit(tower);
                Color towerColor = isEligible ? new Color(0.32f, 0.65f, 0.38f) : GetTowerRaceColor(tower.Shape.Race);
                if (unit == _selectedUnit) towerColor = Color.Lerp(towerColor, Color.white, 0.22f);
                foreach (Vector2Int point in tower.Shape.GetCells(tower.Position, tower.Rotation))
                    _cells[point.x, point.y].color = towerColor;
                _anchorLabels[tower.Position.x, tower.Position.y].text = isEligible ? "A+" : "A";
            }
            if (previewCells != null)
                foreach (Vector2Int point in previewCells)
                    if (point.x >= 0 && point.x < BoardModel.WIDTH && point.y >= 0 && point.y < BoardModel.HEIGHT)
                        _cells[point.x, point.y].color = valid ? new Color(0.32f, 0.7f, 0.4f) : new Color(0.75f, 0.2f, 0.2f);
            _cells[_board.Spawn.x, _board.Spawn.y].color = new Color(0.3f, 0.7f, 0.9f);
            _cells[_board.Goal.x, _board.Goal.y].color = new Color(0.95f, 0.7f, 0.2f);
            if (previewCells == null)
            {
                for (int i = 0; i < _shopLabels.Length; i++)
                {
                    int offer = _economy.Offers[i];
                    _shopLabels[i].text = offer < 0 ? "SOLD" : _shapes[offer].Name + "  (" + _economy.GetPrice(offer) + ") x" + CountOwned(offer) +
                        "\n" + FormatShape(_shapes[offer], _shopRotations[i]);
                }
                for (int i = 0; i < _benchLabels.Length; i++)
                {
                    TowerUnit unit = GetBenchUnit(i);
                    bool isEligible = _isFeeding && _economy.CanFeed(_selectedUnit, unit);
                    _benchLabels[i].text = unit == null ? "EMPTY" : (isEligible ? "+ " : "") + _shapes[unit.TypeIndex].Name + "\n" + unit.Star + "*";
                    _benchLabels[i].color = _isFeeding && !isEligible ? Color.gray : Color.white;
                }
                _economyStatus.text = "Gold: " + _economy.Gold + "   Level: " + _economy.Level + "   Deployed: " + _board.Towers.Count + "/" + _economy.Level +
                    "   Level cost: " + (_economy.LevelCost == 0 ? "MAX" : _economy.LevelCost.ToString()) + "   Shop: " + (_economy.IsLocked ? "LOCKED" : "OPEN");
                _status.text = _isFeeding ? "Choose material, then CONFIRM FEED. Esc: cancel." : "Drag a bench tower to the board. R: rotate. Right-click: select.";
            }

            _synergyStatus.text = BuildSynergySummary(committedEvaluation, shownEvaluation, isSynergyPreview);
            bool showReport = _combat != null && _combat.LastReport != null &&
                (_combat.Phase == CombatPhase.WaveResult || _combat.Phase == CombatPhase.Victory ||
                 _combat.Phase == CombatPhase.Defeat);
            _selectedStatus.text = showReport
                ? BuildWaveReportStatus(_combat.LastReport)
                : BuildSelectedStatus(committedEvaluation, shownEvaluation, isSynergyPreview);
            UpdateControls();
            UpdateCombatHud();
            if (_combat != null && _combat.Phase != CombatPhase.Preparation) SetPhaseStatus();
        }

        /// <summary>
        /// Formats wave outcome, captured synergy, and per-tower attack and damage totals.
        /// </summary>
        private static string BuildWaveReportStatus(WaveReport report)
        {
            var text = new StringBuilder();
            text.Append("WAVE ").Append(report.WaveNumber).Append(" REPORT").AppendLine();
            text.Append("Path ").Append(report.PathLength).Append(" | Kills ").Append(report.Kills)
                .Append(" | Leaks ").Append(report.Leaks).AppendLine();
            text.Append("Base damage ").Append(report.BaseDamage).Append(" | HP ")
                .Append(report.RemainingBaseHealth).Append(" | Income ").Append(report.GoldReward).AppendLine();
            text.Append("Synergy: ").Append(report.SynergySummary).AppendLine();
            text.AppendLine("Tower | Attacks | Damage");
            foreach (TowerDamageReport tower in report.Towers)
                text.Append(tower.TowerName).Append(" | ").Append(tower.AttackCount).Append(" | ")
                    .AppendLine(tower.DamageDealt.ToString("0.0"));
            if (report.Towers.Count == 0) text.Append("No towers deployed");
            return text.ToString();
        }

        /// <summary>
        /// Builds the global type counts and lists every tower changed by the current layout preview.
        /// </summary>
        private string BuildSynergySummary(BoardSynergyEvaluation committed, BoardSynergyEvaluation shown, bool isPreview)
        {
            var text = new StringBuilder();
            text.AppendLine(isPreview ? "SYNERGY PREVIEW" : "BOARD SYNERGY");
            text.Append("Race types | ");
            for (int i = 0; i < 3; i++)
            {
                TowerRace race = (TowerRace)i;
                if (i > 0) text.Append("  ");
                int count = shown.GetRaceTypeCount(race);
                float bonus = shown.GetRaceGlobalAttackBonus(race);
                text.Append(SynergyEvaluator.GetRaceName(race)).Append(' ').Append(count).Append('/')
                    .Append(_synergySettings.ThirdTierTypeCount);
                if (bonus > 0f) text.Append(" (+").Append(Mathf.RoundToInt(bonus * 100f)).Append("% ATK)");
            }
            text.AppendLine();
            text.Append("Role types | ");
            for (int i = 0; i < 3; i++)
            {
                TowerRole role = (TowerRole)i;
                if (i > 0) text.Append("  ");
                int count = shown.GetRoleTypeCount(role);
                float bonus = shown.GetRoleGlobalAttackSpeedBonus(role);
                text.Append(SynergyEvaluator.GetRoleName(role)).Append(' ').Append(count).Append('/')
                    .Append(_synergySettings.ThirdTierTypeCount);
                if (bonus > 0f) text.Append(" (+").Append(Mathf.RoundToInt(bonus * 100f)).Append("% AS)");
            }

            if (isPreview)
            {
                text.AppendLine();
                text.AppendLine("Changed towers:");
                bool hasChanges = false;
                foreach (TowerSynergyStats stats in shown.Towers)
                {
                    TowerSynergyStats previous = committed.GetStats(stats.Unit);
                    string change = BuildStatDelta(previous, stats);
                    if (change.Length == 0) continue;
                    if (hasChanges) text.AppendLine();
                    text.Append(stats.Definition.Name).Append(": ").Append(change);
                    hasChanges = true;
                }
                if (!hasChanges) text.AppendLine().Append("No deployed stats change.");
            }

            return text.ToString();
        }

        /// <summary>
        /// Formats the selected tower's resolved stats, current feed state, and active source list.
        /// </summary>
        private string BuildSelectedStatus(BoardSynergyEvaluation committed, BoardSynergyEvaluation shown, bool isPreview)
        {
            if (_selectedUnit == null) return "Select a tower";

            TowerShape shape = _shapes[_selectedUnit.TypeIndex];
            TowerSynergyStats stats = shown.GetStats(_selectedUnit);
            TowerSynergyStats previous = committed.GetStats(_selectedUnit);
            int feedCount = _selectedUnit.FeedCount;
            bool isFeedPreview = _isFeeding && _material != null;
            if (isFeedPreview && _selectedUnit.FeedCount < 5) feedCount++;
            string progress = feedCount < 2 ? feedCount + "/2" : feedCount < 5 ? (feedCount - 2) + "/3" : "MAX";
            int displayedStar = feedCount < 2 ? 1 : feedCount < 5 ? 2 : 3;
            var text = new StringBuilder();
            text.Append(shape.Name).Append(" | ").Append(SynergyEvaluator.GetRaceName(shape.Race)).Append(" / ")
                .Append(SynergyEvaluator.GetRoleName(shape.Role));
            if (isPreview) text.Append(" | PREVIEW");
            text.AppendLine();
            text.Append(isFeedPreview ? "Star " + _selectedUnit.Star + " -> " + displayedStar : "Star " + _selectedUnit.Star)
                .Append(" | Feed ").Append(progress)
                .Append(" | Refund ").Append(_selectedUnit.InvestedGold);

            if (stats == null)
            {
                text.AppendLine();
                text.Append("Not deployed | Base attack ").Append(shape.BaseAttack.ToString("0.0"))
                    .Append(" | Damage multiplier x").Append(_selectedUnit.DamageMultiplierAt(feedCount).ToString("0.00"));
                return text.ToString();
            }

            text.AppendLine();
            text.Append("Attack ").Append(stats.Attack.ToString("0.0"))
                .Append(" | Attack speed x").Append(stats.AttackSpeedMultiplier.ToString("0.00"));
            text.AppendLine();
            text.Append("Interval ").Append(stats.AttackInterval.ToString("0.00")).Append("s | Range ")
                .Append(stats.Range.ToString("0.00"));
            if (shape.Role == TowerRole.Mortar) text.Append(" | Blast ").Append(stats.BlastRadius.ToString("0.00"));
            text.AppendLine();
            text.Append("Neighbors ").Append(stats.AdjacentTowerCount).Append(" | Same-race ")
                .Append(stats.SameRaceNeighborCount).Append(" | Cluster ").Append(stats.RaceClusterSize);
            text.AppendLine();
            text.Append("Path C ").Append(stats.PathContactCells).Append(" | Gap D ").Append(stats.PathGap);

            if (isPreview && previous != null)
            {
                string change = BuildStatDelta(previous, stats);
                if (change.Length > 0) text.AppendLine().Append("Change: ").Append(change);
            }

            text.AppendLine();
            text.AppendLine("Sources:");
            foreach (string source in stats.Sources)
                text.Append("• ").AppendLine(source);
            return text.ToString();
        }

        /// <summary>
        /// Returns changed combat values and spatial relationship counts for one tower.
        /// </summary>
        private static string BuildStatDelta(TowerSynergyStats previous, TowerSynergyStats current)
        {
            if (previous == null)
                return "new | ATK " + current.Attack.ToString("0.0") + " | AS x" +
                    current.AttackSpeedMultiplier.ToString("0.00") + " | Range " + current.Range.ToString("0.00");

            var changes = new StringBuilder();
            AppendStatDelta(changes, "ATK", previous.Attack, current.Attack);
            AppendStatDelta(changes, "AS", previous.AttackSpeedMultiplier, current.AttackSpeedMultiplier);
            AppendStatDelta(changes, "Interval", previous.AttackInterval, current.AttackInterval);
            AppendStatDelta(changes, "Range", previous.Range, current.Range);
            AppendStatDelta(changes, "Blast", previous.BlastRadius, current.BlastRadius);
            AppendIntegerDelta(changes, "Links", previous.AdjacentTowerCount, current.AdjacentTowerCount);
            AppendIntegerDelta(changes, "Cluster", previous.RaceClusterSize, current.RaceClusterSize);
            AppendIntegerDelta(changes, "C", previous.PathContactCells, current.PathContactCells);
            AppendIntegerDelta(changes, "D", previous.PathGap, current.PathGap);
            return changes.ToString();
        }

        /// <summary>
        /// Appends a formatted numeric change when two combat values differ.
        /// </summary>
        private static void AppendStatDelta(StringBuilder changes, string label, float previous, float current)
        {
            if (Mathf.Abs(previous - current) < 0.001f) return;
            if (changes.Length > 0) changes.Append(" | ");
            changes.Append(label).Append(' ').Append(previous.ToString("0.0")).Append("->")
                .Append(current.ToString("0.0"));
        }

        /// <summary>
        /// Appends a formatted integer change when two spatial counts differ.
        /// </summary>
        private static void AppendIntegerDelta(StringBuilder changes, string label, int previous, int current)
        {
            if (previous == current) return;
            if (changes.Length > 0) changes.Append(" | ");
            changes.Append(label).Append(' ').Append(previous).Append("->").Append(current);
        }

        /// <summary>
        /// Returns the 2D fill color used to distinguish each race on the board.
        /// </summary>
        private static Color GetTowerRaceColor(TowerRace race)
        {
            switch (race)
            {
                case TowerRace.Stone: return new Color(0.58f, 0.49f, 0.35f);
                case TowerRace.Fungal: return new Color(0.30f, 0.53f, 0.34f);
                case TowerRace.Resonant: return new Color(0.38f, 0.40f, 0.72f);
                default: return new Color(0.55f, 0.42f, 0.28f);
            }
        }
    }
}
