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
        private readonly Dictionary<int, ProjectileVisual> _projectileBySequence = new Dictionary<int, ProjectileVisual>();
        private readonly List<ProjectileVisual> _projectileVisuals = new List<ProjectileVisual>();
        private readonly List<ImpactVisual> _impactVisuals = new List<ImpactVisual>();
        private readonly List<FloatingDamageVisual> _floatingDamageVisuals = new List<FloatingDamageVisual>();
        private readonly HashSet<int> _activeEnemyIds = new HashSet<int>();
        private readonly List<int> _removedEnemyIds = new List<int>();
        private readonly HashSet<int> _activeImpactIds = new HashSet<int>();
        private readonly List<int> _removedImpactIds = new List<int>();
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
        private Vector2Int _previewAnchor;
        private int _previewRotation;
        private int _previewBoardVersion;
        private TowerUnit _previewUnit;
        private PlacedTower _previewMoving;
        private bool _hasPreviewCache;
        private bool _previewValid;
        private string _previewReason;
        private List<Vector2Int> _previewPath;
        private List<Vector2Int> _previewCells;
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

        private sealed class ProjectileVisual
        {
            public int Sequence;
            public int TargetEnemySequence;
            public RectTransform Beam;
            public UnityEngine.UI.Image BeamImage;
            public RectTransform Head;
            public UnityEngine.UI.Image HeadImage;
            public Vector2 Source;
            public Vector2 Target;
            public float Elapsed;
            public float Duration;
            public float Damage;
            public float BlastRadius;
            public bool IsMortar;
        }

        private sealed class ImpactVisual
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Image;
            public Vector2 Position;
            public float StartSize;
            public float EndSize;
            public float Elapsed;
            public float Duration;
            public Color Color;
        }

        private sealed class FloatingDamageVisual
        {
            public RectTransform Rect;
            public TextMeshProUGUI Label;
            public Vector2 StartPosition;
            public float Elapsed;
            public float Duration;
        }

        void Start()
        {
            _towerDefinitions = LocalizeTowerDefinitions(_towerDefinitions);
            _shapes = _towerDefinitions;
            _activeSeed = _shopSeed;
            _board = new BoardModel();
            _economy = new EconomyModel(_board, _shapes, _activeSeed, _economySettings);
            _combat = new CombatModel(_activeSeed, _combatSettings);
            _font = CreateKoreanFontAsset();
            if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            CreateInterface();
            HandlePhaseChange();
        }

        /// <summary>
        /// Rebuilds serialized tower definitions with Korean display names while preserving their stats and shapes.
        /// </summary>
        private static List<TowerShape> LocalizeTowerDefinitions(IReadOnlyList<TowerShape> definitions)
        {
            var localized = new List<TowerShape>(definitions == null ? 0 : definitions.Count);
            if (definitions == null) return localized;
            foreach (TowerShape definition in definitions)
            {
                if (definition == null) continue;
                List<Vector2Int> cellsFromAnchor = definition.GetCells(Vector2Int.zero, 0);
                string name = SynergyEvaluator.GetRaceName(definition.Race) + " " +
                    SynergyEvaluator.GetRoleName(definition.Role);
                localized.Add(new TowerShape(name, cellsFromAnchor.ToArray(), Vector2Int.zero,
                    definition.Cost, definition.DefinitionId, definition.Race, definition.Role,
                    definition.BaseAttack, definition.BaseAttackInterval, definition.BaseRange));
            }
            return localized;
        }

        void Update()
        {
            if (_combat == null) return;
            bool advanced = _combat.Advance(Time.unscaledDeltaTime);
            if (advanced) RenderCombatEntities();
            ProcessCombatVisualEvents();
            UpdateCombatVisuals(Time.unscaledDeltaTime);
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
                _hasPreviewCache = false;
            }
            if (_isDragging && Keyboard.current.rKey.wasPressedThisFrame)
            {
                _rotation = (_rotation + 1) % 4;
                _grabOffset = new Vector2Int(-_grabOffset.y, _grabOffset.x);
                _hasPreviewCache = false;
            }
            if (_isDragging && (Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
            {
                _isDragging = false;
                _moving = null;
                _hasPreviewCache = false;
                Refresh(null, false, null);
                return;
            }
            if (_isDragging && onBoard)
            {
                Vector2Int anchor = cell - _grabOffset;
                TowerShape shape = _shapes[_selectedUnit.TypeIndex];
                bool cacheMatches = _hasPreviewCache && _previewAnchor == anchor &&
                    _previewRotation == _rotation && _previewBoardVersion == _board.Version &&
                    _previewUnit == _selectedUnit && _previewMoving == _moving;
                if (!cacheMatches)
                {
                    _previewValid = _board.TryPreview(shape, anchor, _rotation, _moving,
                        out _previewPath, out _previewReason);
                    if (_moving == null && _board.Towers.Count >= _economy.Level)
                    {
                        _previewValid = false;
                        _previewReason = "배치 한도에 도달했습니다.";
                    }
                    _previewAnchor = anchor;
                    _previewRotation = _rotation;
                    _previewBoardVersion = _board.Version;
                    _previewUnit = _selectedUnit;
                    _previewMoving = _moving;
                    _previewCells = shape.GetCells(anchor, _rotation);
                    _hasPreviewCache = true;
                    PlacedTower previewPlacement = _previewValid ? new PlacedTower(shape, anchor, _rotation) : null;
                    Refresh(_previewPath, _previewValid, _previewCells, previewPlacement);
                    if (anchor.x >= 0 && anchor.x < BoardModel.WIDTH && anchor.y >= 0 && anchor.y < BoardModel.HEIGHT)
                        _anchorLabels[anchor.x, anchor.y].text = "포";
                    _status.text = _previewValid
                        ? "드래그: 배치  |  R 키: 회전  |  에스케이프 키: 취소  |  경로 길이: " + (_previewPath.Count - 1)
                        : _previewReason;
                }
            }
            else if (_isDragging && !onBoard && _hasPreviewCache)
            {
                _hasPreviewCache = false;
                Refresh(null, false, null);
                _status.text = "보드 바깥입니다.";
            }
            if (_isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                bool placed = onBoard && _economy.Place(_selectedUnit, cell - _grabOffset, _rotation);
                string failure = _previewReason;
                _isDragging = false;
                _moving = null;
                _hasPreviewCache = false;
                Refresh(null, false, null);
                if (!placed) _status.text = string.IsNullOrEmpty(failure)
                    ? "배치할 수 없어 이전 상태로 되돌렸습니다." : failure;
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
                    () => { CancelFeed(); bool success = _economy.Buy(index); Refresh(null, false, null); if (!success) _status.text = "구매할 수 없습니다. 골드, 판매 목록, 대기석 공간을 확인하세요."; });
                CreateButton(canvas.transform, new Vector2(-470, 250 - i * 65), new Vector2(40, 58),
                    () => { CancelFeed(); if (_economy.Offers[index] >= 0) _shopRotations[index] = (_shopRotations[index] + 1) % 4; Refresh(null, false, null); }).text = "회";
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
                () => { CancelFeed(); bool success = _economy.Reroll(); if (success) System.Array.Clear(_shopRotations, 0, _shopRotations.Length); Refresh(null, false, null); if (!success) _status.text = "새 목록을 만들 수 없습니다. 상점 고정을 풀고 골드를 확인하세요."; }).text = "새로고침 (" + _economy.RerollCost + ")";
            CreateButton(canvas.transform, new Vector2(-650, -115), new Vector2(300, 48),
                () => { CancelFeed(); _economy.ToggleLock(); Refresh(null, false, null); }).text = "상점 고정 전환";
            CreateButton(canvas.transform, new Vector2(650, 250), new Vector2(260, 48),
                () => { CancelFeed(); bool success = _economy.LevelUp(); Refresh(null, false, null); if (!success) _status.text = "레벨을 올릴 수 없습니다. 최대 레벨 또는 골드를 확인하세요."; }).text = "레벨 업";
            CreateButton(canvas.transform, new Vector2(650, 190), new Vector2(260, 48),
                () => { CancelFeed(); bool success = _selectedUnit != null && _economy.ReturnToBench(_selectedUnit); Refresh(null, false, null); if (!success) _status.text = "배치된 타워를 선택하고 대기석 공간을 비우세요."; }).text = "대기석으로 이동";
            CreateButton(canvas.transform, new Vector2(650, 130), new Vector2(260, 48), SellSelected).text = "판매";
            CreateButton(canvas.transform, new Vector2(650, 70), new Vector2(260, 48),
                () => { _isFeeding = _selectedUnit != null && _selectedUnit.FeedCount < 5; _material = null; Refresh(null, false, null); if (!_isFeeding) _status.text = "3성 미만 타워를 먼저 선택하세요."; }).text = "강화 재료 선택";
            CreateButton(canvas.transform, new Vector2(650, 10), new Vector2(260, 48), ConfirmFeed).text = "재료 투입";
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
            retryLabel.text = "같은 난수값으로 재도전";
            newRunLabel.text = "새 게임 시작";
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
            ClearCombatVisuals();
            _activeSeed = seed;
            _board = new BoardModel();
            _economy = new EconomyModel(_board, _shapes, seed, _economySettings);
            _combat = new CombatModel(seed, _combatSettings);
            _selectedUnit = null;
            _material = null;
            _moving = null;
            _rotation = 0;
            _isDragging = false;
            _hasPreviewCache = false;
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
                    _status.text = "웨이브를 시작할 수 없습니다. 경로와 전투 일정을 확인하세요.";
                    return;
                }
                _isDragging = false;
                _moving = null;
                _hasPreviewCache = false;
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
            _advanceLabel.text = preparing ? "웨이브 시작" : "다음 웨이브";
            _pauseButton.gameObject.SetActive(inCombat);
            _speedButton.gameObject.SetActive(inCombat);
            _retryButton.gameObject.SetActive(ended);
            _newRunButton.gameObject.SetActive(ended);
            _pauseButton.interactable = inCombat;
            _speedButton.interactable = inCombat;
            _pauseLabel.text = _combat.IsPaused ? "계속" : "일시 정지";
            _speedLabel.text = _combat.SimulationSpeed > 1.5f ? "2배" : "1배";
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
                    _status.text = "준비 단계 | 타워를 배치한 뒤 웨이브를 시작하세요. 엔터 키로도 시작할 수 있습니다.";
                    break;
                case CombatPhase.Combat:
                    _status.text = _combat.CurrentWave + " 웨이브 진행 중 | 스페이스바로 일시 정지할 수 있습니다. 보드와 상점은 잠겨 있습니다.";
                    break;
                case CombatPhase.WaveResult:
                    WaveReport report = _combat.LastReport;
                    _status.text = report.WaveNumber + " 웨이브 완료 | 경로 " + report.PathLength + "칸 | 처치 " + report.Kills + " | 누수 " +
                        report.Leaks + " | 기지 피해 " + report.BaseDamage + " (체력 " + report.RemainingBaseHealth + ") | 다음 웨이브 후 골드 +" +
                        report.GoldReward;
                    break;
                case CombatPhase.Victory:
                    _status.text = "승리 | " + _combat.WaveCount + "개 웨이브를 모두 막았습니다. 기지 체력 " +
                        _combat.BaseHealth + ". 같은 난수값으로 재도전하거나 새 게임을 시작하세요.";
                    break;
                case CombatPhase.Defeat:
                    string cause = _combat.DefeatCause == DefeatReason.BossReachedGoal
                        ? "우두머리가 목표에 도달했습니다." : "기지 체력이 모두 소진됐습니다.";
                    _status.text = "패배 | " + cause + " 같은 난수값으로 재도전하거나 새 게임을 시작하세요.";
                    break;
            }
        }

        /// <summary>
        /// Displays current wave, base health, economy, seed, time, and combat speed.
        /// </summary>
        private void UpdateCombatHud()
        {
            if (_combat == null || _economyStatus == null) return;
            int minutes = Mathf.FloorToInt((float)(_combat.SimulationTime / 60d));
            int seconds = Mathf.FloorToInt((float)(_combat.SimulationTime % 60d));
            string time = minutes.ToString("00") + ":" + seconds.ToString("00");
            string phase = _combat.Phase == CombatPhase.Combat ? " | " + time + " | " +
                (_combat.IsPaused ? "일시 정지" : (_combat.SimulationSpeed > 1.5f ? "2배" : "1배")) : "";
            _economyStatus.text = "웨이브 " + _combat.CurrentWave + "/" + _combat.WaveCount + "   기지 " +
                _combat.BaseHealth + "/" + _combat.StartingBaseHealth + "   골드 " + _economy.Gold +
                "   레벨 " + _economy.Level + "   배치 " + _board.Towers.Count + "/" + _economy.Level +
                "   난수값 " + _activeSeed + phase;
        }

        /// <summary>
        /// Creates a dynamic TextMesh Pro font from the Korean font installed on Windows.
        /// </summary>
        private static TMP_FontAsset CreateKoreanFontAsset()
        {
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
            if (font == null) font = TMP_FontAsset.CreateFontAsset("맑은 고딕", "Regular", 48);
            return font;
        }

        /// <summary>
        /// Converts newly simulated attacks into transient UI projectiles and impact effects.
        /// </summary>
        private void ProcessCombatVisualEvents()
        {
            if (_combat == null || _boardRect == null) return;
            foreach (CombatVisualEvent combatEvent in _combat.VisualEvents)
            {
                switch (combatEvent.Type)
                {
                    case CombatVisualEventType.ProjectileLaunched:
                        CreateProjectileVisual(combatEvent);
                        break;
                    case CombatVisualEventType.EnemyHit:
                        if (combatEvent.IsMortar)
                            CreateFloatingDamage(combatEvent.Position, combatEvent.Damage);
                        else if (_projectileBySequence.TryGetValue(combatEvent.Sequence, out ProjectileVisual projectile))
                            projectile.Damage = combatEvent.Damage;
                        else
                        {
                            CreateImpactVisual(combatEvent.Position, 0f, isMortar: false);
                            CreateFloatingDamage(combatEvent.Position, combatEvent.Damage);
                        }
                        break;
                    case CombatVisualEventType.AreaImpact:
                        CreateImpactVisual(combatEvent.Position, combatEvent.BlastRadius, isMortar: true);
                        if (_projectileBySequence.TryGetValue(combatEvent.Sequence, out ProjectileVisual mortar))
                            RemoveProjectileVisual(mortar);
                        break;
                }
            }
            _combat.ClearVisualEvents();
        }

        /// <summary>
        /// Creates a colored beam and moving head from a tower anchor to the captured target.
        /// </summary>
        private void CreateProjectileVisual(CombatVisualEvent combatEvent)
        {
            var visual = new ProjectileVisual
            {
                Sequence = combatEvent.Sequence,
                TargetEnemySequence = combatEvent.TargetEnemySequence,
                Source = CellToBoardPosition(combatEvent.SourcePosition),
                Target = CellToBoardPosition(combatEvent.Position),
                Duration = Mathf.Max(0.04f, combatEvent.TravelTime),
                Damage = combatEvent.Damage,
                BlastRadius = combatEvent.BlastRadius,
                IsMortar = combatEvent.IsMortar
            };
            Color color = GetProjectileColor(combatEvent.IsMortar);
            visual.Beam = CreateRect(_boardRect, "Attack Beam", new Vector2(1f, 7f), Vector2.zero);
            visual.BeamImage = visual.Beam.gameObject.AddComponent<UnityEngine.UI.Image>();
            visual.BeamImage.color = color;
            visual.BeamImage.raycastTarget = false;
            visual.Head = CreateRect(_boardRect, "Attack Bolt", new Vector2(16f, 16f), visual.Source);
            visual.HeadImage = visual.Head.gameObject.AddComponent<UnityEngine.UI.Image>();
            visual.HeadImage.color = Color.white;
            visual.HeadImage.raycastTarget = false;
            _projectileVisuals.Add(visual);
            _projectileBySequence[visual.Sequence] = visual;
        }

        /// <summary>
        /// Advances projectiles and fades impact rings and floating damage text using combat time.
        /// </summary>
        private void UpdateCombatVisuals(float unscaledDeltaTime)
        {
            if (_combat == null || _boardRect == null) return;
            float combatDelta = _combat.IsPaused ? 0f : unscaledDeltaTime * _combat.SimulationSpeed;
            for (int i = _projectileVisuals.Count - 1; i >= 0; i--)
            {
                ProjectileVisual projectile = _projectileVisuals[i];
                projectile.Elapsed += combatDelta;
                float progress = Mathf.Clamp01(projectile.Elapsed / projectile.Duration);
                Vector2 destination = projectile.IsMortar
                    ? projectile.Target
                    : GetEnemyMarkerPosition(projectile.TargetEnemySequence, projectile.Target);
                Vector2 headPosition = Vector2.Lerp(projectile.Source, destination, progress);
                Vector2 direction = headPosition - projectile.Source;
                projectile.Beam.anchoredPosition = (projectile.Source + headPosition) * 0.5f;
                projectile.Beam.sizeDelta = new Vector2(Mathf.Max(2f, direction.magnitude), projectile.IsMortar ? 10f : 7f);
                projectile.Beam.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                projectile.BeamImage.color = new Color(projectile.BeamImage.color.r, projectile.BeamImage.color.g,
                    projectile.BeamImage.color.b, 1f - progress * 0.3f);
                projectile.Head.anchoredPosition = headPosition;
                if (progress < 1f) continue;
                if (!projectile.IsMortar)
                {
                    CreateImpactVisual(destination, 0f, isMortar: false);
                    CreateFloatingDamage(destination, projectile.Damage);
                }
                RemoveProjectileVisual(projectile);
            }

            for (int i = _impactVisuals.Count - 1; i >= 0; i--)
            {
                ImpactVisual impact = _impactVisuals[i];
                impact.Elapsed += combatDelta;
                float progress = Mathf.Clamp01(impact.Elapsed / impact.Duration);
                float size = Mathf.Lerp(impact.StartSize, impact.EndSize, progress);
                impact.Rect.anchoredPosition = impact.Position;
                impact.Rect.sizeDelta = new Vector2(size, size);
                Color color = impact.Color;
                color.a *= 1f - progress;
                impact.Image.color = color;
                if (progress < 1f) continue;
                Destroy(impact.Rect.gameObject);
                _impactVisuals.RemoveAt(i);
            }

            for (int i = _floatingDamageVisuals.Count - 1; i >= 0; i--)
            {
                FloatingDamageVisual damage = _floatingDamageVisuals[i];
                damage.Elapsed += combatDelta;
                float progress = Mathf.Clamp01(damage.Elapsed / damage.Duration);
                damage.Rect.anchoredPosition = damage.StartPosition + Vector2.up * (24f * progress);
                Color color = damage.Label.color;
                color.a = 1f - progress;
                damage.Label.color = color;
                if (progress < 1f) continue;
                Destroy(damage.Rect.gameObject);
                _floatingDamageVisuals.RemoveAt(i);
            }
        }

        /// <summary>
        /// Creates a growing flash whose size reflects whether the hit is direct or explosive.
        /// </summary>
        private void CreateImpactVisual(Vector2 position, float blastRadius, bool isMortar)
        {
            float endSize = isMortar ? Mathf.Max(44f, blastRadius * CELL_SIZE * 2f) : 40f;
            float startSize = isMortar ? Mathf.Max(18f, endSize * 0.25f) : 12f;
            var rect = CreateRect(_boardRect, isMortar ? "Blast Flash" : "Hit Flash",
                new Vector2(startSize, startSize), position);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            Color color = isMortar ? new Color(1f, 0.40f, 0.08f, 0.76f) : new Color(1f, 0.86f, 0.35f, 0.9f);
            image.color = color;
            image.raycastTarget = false;
            _impactVisuals.Add(new ImpactVisual
            {
                Rect = rect,
                Image = image,
                Position = position,
                StartSize = startSize,
                EndSize = endSize,
                Duration = isMortar ? 0.28f : 0.20f,
                Color = color
            });
        }

        /// <summary>
        /// Adds a Korean numeric damage popup at the point where an enemy was hit.
        /// </summary>
        private void CreateFloatingDamage(Vector2 position, float damage)
        {
            RectTransform rect = CreateRect(_boardRect, "Damage Number", new Vector2(130f, 38f), position + Vector2.up * 12f);
            TextMeshProUGUI label = CreateText(rect, Mathf.RoundToInt(damage) + " 피해", 20);
            label.color = new Color(1f, 0.96f, 0.70f, 1f);
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color(0.12f, 0.08f, 0.04f, 0.95f);
            _floatingDamageVisuals.Add(new FloatingDamageVisual
            {
                Rect = rect,
                Label = label,
                StartPosition = rect.anchoredPosition,
                Duration = 0.65f
            });
        }

        /// <summary>
        /// Returns the current enemy marker position or the launch-time target fallback.
        /// </summary>
        private Vector2 GetEnemyMarkerPosition(int enemySequence, Vector2 fallback)
        {
            if (_combat != null)
                foreach (EnemyInstance enemy in _combat.Enemies)
                    if (enemy.Sequence == enemySequence && enemy.IsAlive)
                        return CellToBoardPosition(enemy.Position);
            return fallback;
        }

        /// <summary>
        /// Removes a projectile's beam and head and releases its sequence lookup.
        /// </summary>
        private void RemoveProjectileVisual(ProjectileVisual projectile)
        {
            if (projectile == null) return;
            Destroy(projectile.Beam.gameObject);
            Destroy(projectile.Head.gameObject);
            _projectileBySequence.Remove(projectile.Sequence);
            _projectileVisuals.Remove(projectile);
        }

        /// <summary>
        /// Selects a contrasting attack color for direct shots and mortar shells.
        /// </summary>
        private static Color GetProjectileColor(bool isMortar)
        {
            return isMortar ? new Color(1f, 0.48f, 0.12f) : new Color(0.36f, 0.86f, 1f);
        }

        /// <summary>
        /// Clears active attack visuals before a run is replaced.
        /// </summary>
        private void ClearCombatVisuals()
        {
            foreach (ProjectileVisual projectile in _projectileVisuals)
            {
                if (projectile.Beam != null) Destroy(projectile.Beam.gameObject);
                if (projectile.Head != null) Destroy(projectile.Head.gameObject);
            }
            foreach (ImpactVisual impact in _impactVisuals)
                if (impact.Rect != null) Destroy(impact.Rect.gameObject);
            foreach (FloatingDamageVisual damage in _floatingDamageVisuals)
                if (damage.Rect != null) Destroy(damage.Rect.gameObject);
            _projectileVisuals.Clear();
            _projectileBySequence.Clear();
            _impactVisuals.Clear();
            _floatingDamageVisuals.Clear();
        }

        /// <summary>
        /// Creates or updates enemy and mortar-landing tokens on the two-dimensional board.
        /// </summary>
        private void RenderCombatEntities()
        {
            if (_combat == null) return;
            _activeEnemyIds.Clear();
            foreach (EnemyInstance enemy in _combat.Enemies)
            {
                _activeEnemyIds.Add(enemy.Sequence);
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
            _removedEnemyIds.Clear();
            foreach (KeyValuePair<int, EnemyMarker> item in _enemyMarkers)
                if (!_activeEnemyIds.Contains(item.Key)) _removedEnemyIds.Add(item.Key);
            foreach (int id in _removedEnemyIds)
            {
                Destroy(_enemyMarkers[id].Rect.gameObject);
                _enemyMarkers.Remove(id);
            }

            _activeImpactIds.Clear();
            foreach (PendingAttack impact in _combat.PendingAttacks)
            {
                _activeImpactIds.Add(impact.Sequence);
                if (!_impactMarkers.TryGetValue(impact.Sequence, out RectTransform marker))
                    _impactMarkers.Add(impact.Sequence, marker = CreateImpactMarker());
                marker.anchoredPosition = CellToBoardPosition(impact.ImpactPoint);
            }
            _removedImpactIds.Clear();
            foreach (KeyValuePair<int, RectTransform> item in _impactMarkers)
                if (!_activeImpactIds.Contains(item.Key)) _removedImpactIds.Add(item.Key);
            foreach (int id in _removedImpactIds)
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
            CreateText(rect, "탄", 14);
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
                case EnemyKind.Fast: return "빠";
                case EnemyKind.Tank: return "탱";
                case EnemyKind.Boss: return "보";
                default: return "일";
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
            _hasPreviewCache = false;
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
            _hasPreviewCache = false;
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
            if (_material == null) _status.text = "같은 종류의 강화하지 않은 1성 타워를 고르세요.";
            else
            {
                int oldLength = _board.GetPath().Count - 1;
                int newLength = _material.Placement == null ? oldLength : _board.GetPathWithout(_material.Placement).Count - 1;
                _status.text = "재료 투입 확인: 공격력 배율 " + _selectedUnit.DamageMultiplier.ToString("0.0") + " → " +
                    _selectedUnit.NextDamageMultiplier.ToString("0.0") + " | 경로 " + oldLength + " → " + newLength + "칸";
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
            if (!success) _status.text = "재료를 투입할 수 없습니다. 조건에 맞는 타워를 선택하세요.";
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
                _status.text = "환급 골드 " + _selectedUnit.InvestedGold + "을 받으려면 판매를 한 번 더 누르세요.";
                return;
            }
            _economy.Sell(_selectedUnit);
            SelectUnit(null);
            Refresh(null, false, null);
        }

        /// <summary>
        /// Formats the tower footprint with a Korean marker for its attack anchor.
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
                    result += cell == Vector2Int.zero ? "포" : cells.Contains(cell) ? "■" : "·";
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
                _anchorLabels[tower.Position.x, tower.Position.y].text = isEligible ? "+포" : "포";
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
                    _shopLabels[i].text = offer < 0 ? "품절" : _shapes[offer].Name + "  (" + _economy.GetPrice(offer) + "골드) 보유 " + CountOwned(offer) +
                        "\n" + FormatShape(_shapes[offer], _shopRotations[i]);
                }
                for (int i = 0; i < _benchLabels.Length; i++)
                {
                    TowerUnit unit = GetBenchUnit(i);
                    bool isEligible = _isFeeding && _economy.CanFeed(_selectedUnit, unit);
                    _benchLabels[i].text = unit == null ? "비어 있음" : (isEligible ? "+ " : "") + _shapes[unit.TypeIndex].Name + "\n" + unit.Star + "성";
                    _benchLabels[i].color = _isFeeding && !isEligible ? Color.gray : Color.white;
                }
                _economyStatus.text = "골드: " + _economy.Gold + "   레벨: " + _economy.Level + "   배치: " + _board.Towers.Count + "/" + _economy.Level +
                    "   다음 레벨 비용: " + (_economy.LevelCost == 0 ? "최대" : _economy.LevelCost.ToString()) + "   상점: " + (_economy.IsLocked ? "고정" : "교체 가능");
                _status.text = _isFeeding ? "재료를 고른 뒤 재료 투입을 누르세요. 에스케이프 키로 취소합니다." : "대기석 타워를 보드로 끌어 놓으세요. R 키: 회전. 마우스 오른쪽 버튼: 선택.";
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
            text.Append(report.WaveNumber).Append(" 웨이브 결과").AppendLine();
            text.Append("경로 ").Append(report.PathLength).Append("칸 | 처치 ").Append(report.Kills)
                .Append(" | 누수 ").Append(report.Leaks).AppendLine();
            text.Append("기지 피해 ").Append(report.BaseDamage).Append(" | 남은 체력 ")
                .Append(report.RemainingBaseHealth).Append(" | 보상 골드 ").Append(report.GoldReward).AppendLine();
            text.Append("시너지: ").Append(report.SynergySummary).AppendLine();
            text.AppendLine("타워 | 공격 횟수 | 피해량");
            foreach (TowerDamageReport tower in report.Towers)
                text.Append(tower.TowerName).Append(" | ").Append(tower.AttackCount).Append(" | ")
                    .AppendLine(tower.DamageDealt.ToString("0.0"));
            if (report.Towers.Count == 0) text.Append("배치된 타워가 없습니다.");
            return text.ToString();
        }

        /// <summary>
        /// Builds the global type counts and lists every tower changed by the current layout preview.
        /// </summary>
        private string BuildSynergySummary(BoardSynergyEvaluation committed, BoardSynergyEvaluation shown, bool isPreview)
        {
            var text = new StringBuilder();
            text.AppendLine(isPreview ? "시너지 미리보기" : "보드 시너지");
            text.Append("종족별 종류 | ");
            for (int i = 0; i < 3; i++)
            {
                TowerRace race = (TowerRace)i;
                if (i > 0) text.Append("  ");
                int count = shown.GetRaceTypeCount(race);
                float bonus = shown.GetRaceGlobalAttackBonus(race);
                text.Append(SynergyEvaluator.GetRaceName(race)).Append(' ').Append(count).Append('/')
                    .Append(_synergySettings.ThirdTierTypeCount);
                if (bonus > 0f) text.Append(" (공격력 +").Append(Mathf.RoundToInt(bonus * 100f)).Append("%)");
            }
            text.AppendLine();
            text.Append("직업별 종류 | ");
            for (int i = 0; i < 3; i++)
            {
                TowerRole role = (TowerRole)i;
                if (i > 0) text.Append("  ");
                int count = shown.GetRoleTypeCount(role);
                float bonus = shown.GetRoleGlobalAttackSpeedBonus(role);
                text.Append(SynergyEvaluator.GetRoleName(role)).Append(' ').Append(count).Append('/')
                    .Append(_synergySettings.ThirdTierTypeCount);
                if (bonus > 0f) text.Append(" (공격 속도 +").Append(Mathf.RoundToInt(bonus * 100f)).Append("%)");
            }

            if (isPreview)
            {
                text.AppendLine();
                text.AppendLine("변경된 타워:");
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
                if (!hasChanges) text.AppendLine().Append("배치 타워 능력치 변화 없음");
            }

            return text.ToString();
        }

        /// <summary>
        /// Formats the selected tower's resolved stats, current feed state, and active source list.
        /// </summary>
        private string BuildSelectedStatus(BoardSynergyEvaluation committed, BoardSynergyEvaluation shown, bool isPreview)
        {
            if (_selectedUnit == null) return "타워를 선택하세요.";

            TowerShape shape = _shapes[_selectedUnit.TypeIndex];
            TowerSynergyStats stats = shown.GetStats(_selectedUnit);
            TowerSynergyStats previous = committed.GetStats(_selectedUnit);
            int feedCount = _selectedUnit.FeedCount;
            bool isFeedPreview = _isFeeding && _material != null;
            if (isFeedPreview && _selectedUnit.FeedCount < 5) feedCount++;
            string progress = feedCount < 2 ? feedCount + "/2" : feedCount < 5 ? (feedCount - 2) + "/3" : "최대";
            int displayedStar = feedCount < 2 ? 1 : feedCount < 5 ? 2 : 3;
            var text = new StringBuilder();
            text.Append(shape.Name).Append(" | ").Append(SynergyEvaluator.GetRaceName(shape.Race)).Append(" / ")
                .Append(SynergyEvaluator.GetRoleName(shape.Role));
            if (isPreview) text.Append(" | 미리보기");
            text.AppendLine();
            text.Append(isFeedPreview ? "성급 " + _selectedUnit.Star + " → " + displayedStar : "성급 " + _selectedUnit.Star)
                .Append(" | 강화 진행 ").Append(progress)
                .Append(" | 누적 비용 ").Append(_selectedUnit.InvestedGold).Append("골드");

            if (stats == null)
            {
                text.AppendLine();
                text.Append("미배치 | 기본 공격력 ").Append(shape.BaseAttack.ToString("0.0"))
                    .Append(" | 공격력 배율 x").Append(_selectedUnit.DamageMultiplierAt(feedCount).ToString("0.00"));
                return text.ToString();
            }

            text.AppendLine();
            text.Append("공격력 ").Append(stats.Attack.ToString("0.0"))
                .Append(" | 공격 속도 배율 x").Append(stats.AttackSpeedMultiplier.ToString("0.00"));
            text.AppendLine();
            text.Append("공격 간격 ").Append(stats.AttackInterval.ToString("0.00")).Append("초 | 사거리 ")
                .Append(stats.Range.ToString("0.00"));
            if (shape.Role == TowerRole.Mortar) text.Append(" | 폭발 범위 ").Append(stats.BlastRadius.ToString("0.00"));
            text.AppendLine();
            text.Append("인접 타워 ").Append(stats.AdjacentTowerCount).Append(" | 같은 종족 인접 ")
                .Append(stats.SameRaceNeighborCount).Append(" | 종족 군집 ").Append(stats.RaceClusterSize);
            text.AppendLine();
            text.Append("경로 접촉 칸 ").Append(stats.PathContactCells).Append(" | 경로 이격 ").Append(stats.PathGap);

            if (isPreview && previous != null)
            {
                string change = BuildStatDelta(previous, stats);
                if (change.Length > 0) text.AppendLine().Append("변화: ").Append(change);
            }

            text.AppendLine();
            text.AppendLine("능력치 출처:");
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
                return "신규 | 공격력 " + current.Attack.ToString("0.0") + " | 공격 속도 x" +
                    current.AttackSpeedMultiplier.ToString("0.00") + " | 사거리 " + current.Range.ToString("0.00");

            var changes = new StringBuilder();
            AppendStatDelta(changes, "공격력", previous.Attack, current.Attack);
            AppendStatDelta(changes, "공격 속도", previous.AttackSpeedMultiplier, current.AttackSpeedMultiplier);
            AppendStatDelta(changes, "공격 간격", previous.AttackInterval, current.AttackInterval);
            AppendStatDelta(changes, "사거리", previous.Range, current.Range);
            AppendStatDelta(changes, "폭발 범위", previous.BlastRadius, current.BlastRadius);
            AppendIntegerDelta(changes, "인접 타워", previous.AdjacentTowerCount, current.AdjacentTowerCount);
            AppendIntegerDelta(changes, "종족 군집", previous.RaceClusterSize, current.RaceClusterSize);
            AppendIntegerDelta(changes, "경로 접촉 칸", previous.PathContactCells, current.PathContactCells);
            AppendIntegerDelta(changes, "경로 이격", previous.PathGap, current.PathGap);
            return changes.ToString();
        }

        /// <summary>
        /// Appends a formatted numeric change when two combat values differ.
        /// </summary>
        private static void AppendStatDelta(StringBuilder changes, string label, float previous, float current)
        {
            if (Mathf.Abs(previous - current) < 0.001f) return;
            if (changes.Length > 0) changes.Append(" | ");
            changes.Append(label).Append(' ').Append(previous.ToString("0.0")).Append(" → ")
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
