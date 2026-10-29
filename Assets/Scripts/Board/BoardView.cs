using System.Collections.Generic;

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

        [Header("Towers")]
        [SerializeField] private List<TowerShape> _towerDefinitions = new List<TowerShape>(TowerCatalog.CreateShapes());

        private readonly BoardModel _board = new BoardModel();
        private readonly UnityEngine.UI.Image[,] _cells = new UnityEngine.UI.Image[BoardModel.WIDTH, BoardModel.HEIGHT];
        private readonly TextMeshProUGUI[,] _anchorLabels = new TextMeshProUGUI[BoardModel.WIDTH, BoardModel.HEIGHT];
        private readonly TextMeshProUGUI[] _shopLabels = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] _benchLabels = new TextMeshProUGUI[6];
        private readonly int[] _shopRotations = new int[4];
        private IReadOnlyList<TowerShape> _shapes;
        private EconomyModel _economy;
        private RectTransform _boardRect;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _economyStatus;
        private TextMeshProUGUI _selectedStatus;
        private TMP_FontAsset _font;
        private TowerUnit _selectedUnit;
        private TowerUnit _material;
        private int _rotation;
        private bool _isDragging;
        private bool _isFeeding;
        private bool _isSaleArmed;
        private PlacedTower _moving;
        private Vector2Int _grabOffset;

        void Start()
        {
            _shapes = _towerDefinitions;
            _economy = new EconomyModel(_board, _shapes, _shopSeed, _economySettings);
            _font = TMP_Settings.defaultFontAsset;
            CreateInterface();
            Refresh(null, false, null);
        }

        void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;
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
                Refresh(path, valid, shape.GetCells(anchor, _rotation));
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
            _economyStatus = CreateText(CreateRect(canvas.transform, "Economy Status", new Vector2(1000, 50), new Vector2(0, 410)), "", 24);
            _selectedStatus = CreateText(CreateRect(canvas.transform, "Selected Status", new Vector2(420, 130), new Vector2(650, -100)), "", 20);
            RectTransform statusRect = CreateRect(canvas.transform, "Status", new Vector2(1400, 70), new Vector2(0, -430));
            _status = CreateText(statusRect, "", 24);
        }

        /// <summary>
        /// Creates an interactive button with a text label at the requested position.
        /// </summary>
        private TextMeshProUGUI CreateButton(Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(parent, "Button", size, position);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.18f, 0.24f, 0.31f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            return CreateText(rect, "", 20);
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
            if (_isFeeding) return;
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
        private void Refresh(List<Vector2Int> previewPath, bool valid, List<Vector2Int> previewCells)
        {
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
                foreach (Vector2Int point in tower.Shape.GetCells(tower.Position, tower.Rotation))
                    _cells[point.x, point.y].color = isEligible ? new Color(0.32f, 0.65f, 0.38f) : new Color(0.55f, 0.42f, 0.28f);
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
                string progress = _selectedUnit == null ? "" : _selectedUnit.FeedCount < 2 ? _selectedUnit.FeedCount + "/2" :
                    _selectedUnit.FeedCount < 5 ? (_selectedUnit.FeedCount - 2) + "/3" : "MAX";
                _selectedStatus.text = _selectedUnit == null ? "Select a tower" : _shapes[_selectedUnit.TypeIndex].Name + "\n" + _selectedUnit.Star +
                    "*  Progress: " + progress + "\nDamage multiplier: " + _selectedUnit.DamageMultiplier.ToString("0.0") +
                    "\nRefund: " + _selectedUnit.InvestedGold;
                _status.text = _isFeeding ? "Choose material, then CONFIRM FEED. Esc: cancel." : "Drag a bench tower to the board. R: rotate. Right-click: select.";
            }
        }
    }
}
