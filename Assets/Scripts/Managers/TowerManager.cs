using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TowerManager : Singleton<TowerManager>
{
    private List<Tower> activeTowers = new List<Tower>();
    private readonly Dictionary<int, Tower> towersById = new Dictionary<int, Tower>();
    private int nextTowerId = 1;
    [Header("Tower setup")]
    [SerializeField] private Tower towerPrefab;
    private TowerData selectedTower = null;
    private bool isPlacingTower = false;
    
    private GameObject previewGhost;
    private GameObject previewFloor;
    private Renderer[] ghostRenderers;
    private Renderer[] floorSquares;    // the four 1x1 squares of the footprint, each coloured by its own buildability
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);
    private bool lastHoverIsValid = false;

    public System.Action<Tower> OnTowerPlaced;

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return; // no input devices (e.g. headless run)
        if (GameInput.Blocked) return;

        if (isPlacingTower)
        {
            UpdatePreview();

            // Shift+drag keeps placing towers; otherwise one click = one attempt
            bool clicked = Mouse.current.leftButton.wasPressedThisFrame
                || (Keyboard.current.shiftKey.isPressed && Mouse.current.leftButton.isPressed);
            if (clicked)
            {
                // Ignore clicks over UI
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                TryPlaceTower();
            }
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

    /// <summary>One simulation tick: every tower acts, in the order they were built.</summary>
    public void SimTick(float dt)
    {
        for (int i = 0; i < activeTowers.Count; i++) activeTowers[i].SimTick(dt);
    }

    public IReadOnlyList<Tower> Towers => activeTowers;
    public int TowerCount => activeTowers.Count;

    /// <summary>The tower whose footprint starts at <paramref name="origin"/>, or null (tests).</summary>
    public Tower FindAt(Vector2Int origin)
    {
        foreach (var t in activeTowers)
            if (GridManager.Instance.FootprintOrigin(t.transform.position) == origin) return t;
        return null;
    }

    public void RegisterTower(Tower tower)
    {
        towersById[tower.Id] = tower;
        activeTowers.Add(tower);
        OnTowerPlaced?.Invoke(tower);
    }

    public void UnregisterTower(Tower tower)
    {
        towersById.Remove(tower.Id);
        activeTowers.Remove(tower);
    }

    /// <summary>Towers on the build menu: those of the active race.</summary>
    public TowerData[] BuildableTowers => RaceManager.Instance != null ? RaceManager.Instance.ActiveTowers : System.Array.Empty<TowerData>();

    public void SelectTower(TowerData tower)
    {
        selectedTower = tower;
        isPlacingTower = true;
        CreatePreviewObjects();
    }

    private void CreatePreviewObjects()
    {
        DestroyPreviewObjects();
        if (selectedTower == null) return;
        
        previewGhost = Instantiate(towerPrefab.gameObject);
        previewGhost.GetComponent<Tower>().Init(selectedTower); // applies level-1 visuals before we strip the scripts
        
        // Clean up ghost
        foreach (var comp in previewGhost.GetComponentsInChildren<MonoBehaviour>()) Destroy(comp);
        foreach (var comp in previewGhost.GetComponentsInChildren<Collider>()) Destroy(comp);
        
        ghostRenderers = previewGhost.GetComponentsInChildren<Renderer>();
        foreach (var rend in ghostRenderers)
        {
            rend.material = CreateGhostMaterial(3000);
        }
        
        previewFloor = new GameObject("PlacementFootprint");
        int squaresPerSide = GridManager.Footprint / 2;
        float square = GridManager.Instance.FootprintWorldSize / squaresPerSide;
        floorSquares = new Renderer[squaresPerSide * squaresPerSide];
        for (int i = 0; i < floorSquares.Length; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(previewFloor.transform, false);
            q.transform.localRotation = Quaternion.Euler(90, 0, 0);
            q.transform.localScale = new Vector3(square * 0.96f, square * 0.96f, 1);
            q.transform.localPosition = new Vector3(((i % squaresPerSide) - (squaresPerSide - 1) / 2f) * square, 0, ((i / squaresPerSide) - (squaresPerSide - 1) / 2f) * square);
            floorSquares[i] = q.GetComponent<Renderer>();
            floorSquares[i].material = CreateGhostMaterial(3001);
        }
        
        lastHoveredCell = new Vector2Int(-999, -999);
    }
    
    // Sprites/Default is unlit, alpha-blended, tintable via color, and works in both built-in and URP.
    private static Material CreateGhostMaterial(int renderQueue)
    {
        return new Material(Shader.Find("Sprites/Default")) { renderQueue = renderQueue };
    }

    private void DestroyPreviewObjects()
    {
        if (previewGhost != null) Destroy(previewGhost);
        if (previewFloor != null) Destroy(previewFloor);
    }

    private void UpdatePreview()
    {
        if (previewGhost == null || previewFloor == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Ground"))
            {
                previewGhost.SetActive(true);
                previewFloor.SetActive(true);

                Vector3 snappedPos = GridManager.Instance.SnapToFootprint(hit.point);
                previewGhost.transform.position = snappedPos;
                previewFloor.transform.position = snappedPos + new Vector3(0, 0.05f, 0);

                Vector2Int cell = GridManager.Instance.FootprintOrigin(snappedPos);
                if (cell != lastHoveredCell)
                {
                    lastHoveredCell = cell;
                    lastHoverIsValid = CanPlaceFor(PlayerManager.Instance.LocalPlayerId, cell, selectedTower, out _);

                    Color ghostColor = lastHoverIsValid ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);
                    var good = new Color(0, 1, 0, 0.8f);
                    var bad = new Color(1, 0, 0, 0.8f);

                    foreach (var rend in ghostRenderers)
                    {
                        if (rend != null) rend.material.color = ghostColor;
                    }
                    ColorFootprint(cell, good, bad);
                }
            }
            else
            {
                previewGhost.SetActive(false);
                previewFloor.SetActive(false);
            }
        }
        else
        {
            previewGhost.SetActive(false);
            previewFloor.SetActive(false);
        }
    }

    /// <summary>
    /// Colours each square of the footprint by whether the ground under it can be built on, ignoring cost and the maze rule,
    /// so the player sees the terrain as if they could afford the tower. (The tower model still tints red when the build
    /// would be refused for any reason.)
    /// </summary>
    private void ColorFootprint(Vector2Int origin, Color good, Color bad)
    {
        int perSide = GridManager.Footprint / 2;
        for (int i = 0; i < floorSquares.Length; i++)
        {
            var block = new Vector2Int(origin.x + (i % perSide) * 2, origin.y + (i / perSide) * 2);
            bool ok = GridManager.Instance.CanBuildBlock(block, 2);
            if (floorSquares[i] != null) floorSquares[i].material.color = ok ? good : bad;
        }
    }

    /// <summary>
    /// Whether <paramref name="playerId"/> can place <paramref name="tower"/> on the footprint at <paramref name="cell"/>: the
    /// ground is buildable, the player can afford it, and it would not cut off any spawner.
    /// </summary>
    private bool CanPlaceFor(int playerId, Vector2Int cell, TowerData tower, out string failReason)
    {
        failReason = null;
        var player = PlayerManager.Instance.Get(playerId);
        if (player == null || !player.active) { failReason = "no such player"; return false; }
        if (!GridManager.Instance.CanBuildFootprint(cell)) { failReason = "occupied or unbuildable"; return false; }
        if (player.gold < tower.BaseCost) { failReason = "not enough gold"; return false; }
        if (player.lumber < tower.lumberCost) { failReason = "not enough lumber"; return false; }

        GridManager.Instance.OccupyFootprint(cell);
        bool pathOk = PathManager.Instance.ValidateFullMaze();
        GridManager.Instance.FreeFootprint(cell);

        if (!pathOk) failReason = "would block the path for at least one spawner";
        return pathOk;
    }

    /// <summary>
    /// Applies a placement command: builds the tower for <paramref name="playerId"/> if it is affordable, buildable and does not
    /// cut off any spawner. Runs on the simulation tick (see <see cref="PlaceTowerCommand"/>).
    /// </summary>
    public bool ExecutePlace(int playerId, TowerData tower, Vector2Int origin, out string failReason)
    {
        if (!CanPlaceFor(playerId, origin, tower, out failReason))
        {
            if (playerId == PlayerManager.Instance.LocalPlayerId) Debug.Log($"Tower placement failed at {origin}: {failReason}");
            return false;
        }

        var player = PlayerManager.Instance.Get(playerId);
        player.TrySpendGold(tower.BaseCost);
        player.TrySpendLumber(tower.lumberCost);

        GridManager.Instance.OccupyFootprint(origin);
        Tower newTower = Instantiate(towerPrefab, GridManager.Instance.FootprintCenter(origin), Quaternion.identity);
        newTower.Init(tower, playerId);
        newTower.Id = nextTowerId++;
        RegisterTower(newTower);

        // Notify enemies that the maze has changed
        PathManager.Instance.NotifyMazeChanged();
        return true;
    }

    /// <summary>
    /// Builds a tower for the local player immediately, bypassing the command queue. For tests and tools; the game itself
    /// places towers with <see cref="PlaceTowerCommand"/>.
    /// </summary>
    public bool TryPlaceTowerAt(TowerData tower, Vector3 worldPos)
    {
        Vector2Int origin = GridManager.Instance.FootprintOrigin(worldPos);
        return ExecutePlace(PlayerManager.Instance.LocalPlayerId, tower, origin, out _);
    }

    /// <summary>Hands every tower of one player to another (a player left the game).</summary>
    public void TransferTowers(int fromId, int toId)
    {
        foreach (var t in activeTowers)
            if (t.Owner == fromId) t.SetOwner(toId);
    }

    public void ExecuteSell(int playerId, int towerInstanceId)
    {
        if (!towersById.TryGetValue(towerInstanceId, out Tower tower) || tower == null || tower.Owner != playerId) return;
        tower.Sell();
    }

    public void ExecuteUpgrade(int playerId, int towerInstanceId)
    {
        if (!towersById.TryGetValue(towerInstanceId, out Tower tower) || tower == null || tower.Owner != playerId) return;
        if (!tower.CanUpgrade()) return;
        var player = PlayerManager.Instance.Get(playerId);
        if (player == null || !player.TrySpendGold(tower.GetUpgradeCost())) return;
        tower.Upgrade();
    }

    /// <summary>Asks to sell one of the local player's towers.</summary>
    public void RequestSell(Tower tower)
    {
        CommandQueue.Submit(new SellTowerCommand { playerId = PlayerManager.Instance.LocalPlayerId, towerInstanceId = tower.Id });
    }

    /// <summary>Asks to upgrade one of the local player's towers.</summary>
    public void RequestUpgrade(Tower tower)
    {
        CommandQueue.Submit(new UpgradeTowerCommand { playerId = PlayerManager.Instance.LocalPlayerId, towerInstanceId = tower.Id });
    }

    /// <summary>Handles a click while placing: issues a placement command if the spot looks valid to the local player.</summary>
    private void TryPlaceTower()
    {
        if (selectedTower == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Check if we hit the ground plane
            if (hit.collider.CompareTag("Ground") || hit.collider.GetComponent<TerrainBuilder>() != null)
            {
                int localId = PlayerManager.Instance.LocalPlayerId;
                Vector2Int origin = GridManager.Instance.FootprintOrigin(hit.point);
                if (!CanPlaceFor(localId, origin, selectedTower, out string failReason))
                {
                    Debug.Log($"Tower placement failed at {origin}: {failReason}");
                    return;
                }

                CommandQueue.Submit(new PlaceTowerCommand { playerId = localId, towerId = selectedTower.wc3Id, originX = origin.x, originY = origin.y });
                if (!Keyboard.current.shiftKey.isPressed) CancelPlacement();
            }
        }
    }

    public void CancelPlacement()
    {
        isPlacingTower = false;
        selectedTower = null;
        DestroyPreviewObjects();
    }
    
    public TowerData GetSelectedTower() => selectedTower;

    public bool IsPlacingTower() => isPlacingTower;
}
