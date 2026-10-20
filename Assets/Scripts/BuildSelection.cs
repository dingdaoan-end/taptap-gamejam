using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Position UI after the fixed camera updates its viewport during window resizing.
[DefaultExecutionOrder(100)]
public class BuildSelection : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask slotLayer;
    [SerializeField] private RectTransform buildMenu;
    [SerializeField] private GameObject[] buildOptions;
    [SerializeField] private GameObject destroyOption;
    [SerializeField] private Vector2 menuOffset;
    [SerializeField, Min(0)] private float menuEdgePadding = 8;
    [SerializeField] private BuildPlacementValidator placementValidator;
    [SerializeField] private Text placementStatus;
    [Header("World tower capacity (shared by all instruments)")]
    [SerializeField, Min(0)] private int maxTowerCount = 4;
    public int MaxTowerCount
    {
        get => Mathf.Max(0, maxTowerCount);
        set => maxTowerCount = Mathf.Max(0, value);
    }
    // Count actual occupancy rather than a lifetime build total: demolition,
    // external tower removal and level cleanup all release capacity naturally.
    public int BuiltTowerCount
    {
        get
        {
            int count = 0;
            foreach (var slot in FindObjectsOfType<BuildSlot>(true))
                if (slot.gameObject.scene == gameObject.scene && slot.Occupied) count++;
            return count;
        }
    }
    public int AvailableTowerCount => Mathf.Max(0, MaxTowerCount - BuiltTowerCount);
    public bool CanBuildMore => AvailableTowerCount > 0;
    [System.Serializable]
    public sealed class OptionBinding
    {
        public GameObject button;
        public GameObject prefab;
    }
    [SerializeField] private OptionBinding[] optionBindings = new OptionBinding[0];
    private GameObject[] unlockedPrefabs;
    private readonly System.Collections.Generic.Dictionary<GameObject, GameObject> prefabReplacements =
        new System.Collections.Generic.Dictionary<GameObject, GameObject>();
    public void SetPrefabReplacement(GameObject original, GameObject replacement)
    {
        if (original == null) return;
        if (replacement == null) prefabReplacements.Remove(original);
        else prefabReplacements[original] = replacement;
    }
    public GameObject ResolveBuildPrefab(GameObject original) => original != null &&
        prefabReplacements.TryGetValue(original, out var replacement) && replacement != null ? replacement : original;
    private bool interactionAllowed = true;
    private int resumeAfterFrame = -1;
    public bool InteractionAllowed => interactionAllowed && isActiveAndEnabled;
    public bool IsUnlocked(GameObject prefab) => prefab != null &&
        (unlockedPrefabs == null || System.Array.IndexOf(unlockedPrefabs, prefab) >= 0);

    public void SetInteractionAllowed(bool allowed)
    {
        interactionAllowed = allowed;
        resumeAfterFrame = Time.frameCount;
        CancelBuild();
    }
    public void SetUnlockedPrefabs(GameObject[] prefabs)
    {
        unlockedPrefabs = prefabs == null ? new GameObject[0] : (GameObject[])prefabs.Clone();
        RefreshMenu();
    }
    bool CanReceiveInput => InteractionAllowed && Time.frameCount > resumeAfterFrame;
    bool OptionUnlocked(GameObject button)
    {
        if (unlockedPrefabs == null) return true; // Existing scenes without a flow keep their current behaviour.
        foreach (var binding in optionBindings)
            if (binding != null && binding.button == button) return IsUnlocked(binding.prefab);
        return false;
    }
    private BuildSlot hovered, selected;
    private BuildFootprint selectedSite;
    private bool siteSelected;
    private BuildPlacementPreview preview;
    private Vector3 authoredMenuScale;
    private readonly Vector3[] menuCorners = new Vector3[4];
    private readonly System.Collections.Generic.List<Graphic> menuGraphics = new System.Collections.Generic.List<Graphic>();
    public Camera WorldCamera => worldCamera;
    public BuildValidation LastValidation { get; private set; }

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (placementValidator == null) placementValidator = GetComponent<BuildPlacementValidator>();
        if (placementValidator == null) placementValidator = gameObject.AddComponent<BuildPlacementValidator>();
        if (buildMenu != null)
        {
            authoredMenuScale = buildMenu.localScale;
            buildMenu.gameObject.SetActive(false);
        }
        SetStatus(string.Empty, false);
    }

    private void Update()
    {
        if (!CanReceiveInput) return;
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { CancelBuild(); return; }
        if (siteSelected)
        {
            LastValidation = placementValidator.Validate(selectedSite, worldCamera);
            RefreshMenu();
        }
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Vector3 mouse = Input.mousePosition;
        if (overUI || worldCamera == null || !worldCamera.isActiveAndEnabled || !worldCamera.pixelRect.Contains(new Vector2(mouse.x, mouse.y)))
        {
            SetHovered(null);
            if (siteSelected) ShowSite(selectedSite, LastValidation); else HidePreview();
            return;
        }
        Ray ray = worldCamera.ScreenPointToRay(mouse);
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) { HidePreview(); return; }
        Vector3 world = ray.GetPoint(distance);
        BuildSlot candidate = null;
        foreach (var hit in Physics2D.OverlapPointAll(world, slotLayer))
        {
            var slot = hit.GetComponent<BuildSlot>();
            if (slot != null && slot.Occupied) { candidate = slot; break; }
        }
        SetHovered(candidate);
        if (candidate != null)
        {
            HidePreview();
            if (Input.GetMouseButtonDown(0)) SelectTower(candidate);
            return;
        }
        if (!placementValidator.TryFootprint(world, out var site)) { HidePreview(); return; }
        var validation = placementValidator.Validate(site, worldCamera);
        ShowSite(site, validation);
        if (Input.GetMouseButtonDown(0)) SelectSite(site);
    }

    public bool SelectSite(BuildFootprint site)
    {
        if (!CanReceiveInput) return false;
        CancelBuild();
        LastValidation = placementValidator.Validate(site, worldCamera);
        ShowSite(site, LastValidation);
        if (!LastValidation.Allowed) return false;
        selectedSite = site; siteSelected = true;
        RefreshMenu(); return true;
    }

    private void LateUpdate()
    {
        if (buildMenu == null || !buildMenu.gameObject.activeInHierarchy || worldCamera == null || selected == null && !siteSelected) return;
        Vector3 center = selected != null ? selected.transform.position : selectedSite.Center;
        var canvas = buildMenu.GetComponentInParent<Canvas>()?.rootCanvas;
        var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var viewport = worldCamera.pixelRect;
        var safeArea = Screen.safeArea;
        var area = Rect.MinMaxRect(Mathf.Max(viewport.xMin, safeArea.xMin), Mathf.Max(viewport.yMin, safeArea.yMin),
            Mathf.Min(viewport.xMax, safeArea.xMax), Mathf.Min(viewport.yMax, safeArea.yMax));
        if (area.width <= 0 || area.height <= 0) return;
        float padding = Mathf.Min(menuEdgePadding, Mathf.Min(area.width, area.height) * .25f);
        area = Rect.MinMaxRect(area.xMin + padding, area.yMin + padding, area.xMax - padding, area.yMax - padding);

        // Measure active visuals, including buttons extending beyond the empty menu root.
        buildMenu.localScale = authoredMenuScale;
        PlaceMenuAtScreenPoint((Vector2)worldCamera.WorldToScreenPoint(center) + menuOffset, uiCamera);
        var bounds = MenuScreenBounds(uiCamera);
        float fit = Mathf.Min(1, area.width / Mathf.Max(1, bounds.width), area.height / Mathf.Max(1, bounds.height));
        if (fit < 1)
        {
            buildMenu.localScale = authoredMenuScale * fit;
            bounds = MenuScreenBounds(uiCamera);
        }
        var shift = new Vector2(
            bounds.xMin < area.xMin ? area.xMin - bounds.xMin : bounds.xMax > area.xMax ? area.xMax - bounds.xMax : 0,
            bounds.yMin < area.yMin ? area.yMin - bounds.yMin : bounds.yMax > area.yMax ? area.yMax - bounds.yMax : 0);
        PlaceMenuAtScreenPoint(RectTransformUtility.WorldToScreenPoint(uiCamera, buildMenu.position) + shift, uiCamera);
    }

    private void PlaceMenuAtScreenPoint(Vector2 point, Camera uiCamera)
    {
        if (buildMenu.parent is RectTransform parent &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, point, uiCamera, out var position))
            buildMenu.position = position;
    }

    private Rect MenuScreenBounds(Camera uiCamera)
    {
        buildMenu.GetComponentsInChildren<Graphic>(false, menuGraphics);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var graphic in menuGraphics)
        {
            if (!graphic.isActiveAndEnabled) continue;
            graphic.rectTransform.GetWorldCorners(menuCorners);
            foreach (var corner in menuCorners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(uiCamera, corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
        }
        return float.IsInfinity(min.x) ? new Rect(RectTransformUtility.WorldToScreenPoint(uiCamera, buildMenu.position), Vector2.zero)
            : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private void SetHovered(BuildSlot next)
    {
        if (hovered == next) return;
        var previous = hovered; hovered = next; Refresh(previous); Refresh(hovered);
    }
    private void SelectTower(BuildSlot slot)
    {
        CancelBuild(); selected = slot; Refresh(selected); RefreshMenu();
        SetStatus("已选中防御塔 · 可拆除并释放占位", true);
    }
    private void RefreshMenu()
    {
        bool occupied = selected != null && selected.Occupied;
        bool hasCapacity = !siteSelected || CanBuildMore;
        if (buildOptions != null) foreach (var option in buildOptions)
        {
            if (option == null) continue;
            option.SetActive(InteractionAllowed && siteSelected && !occupied && OptionUnlocked(option));
            var button = option.GetComponent<Button>();
            if (button != null) button.interactable = InteractionAllowed && siteSelected && hasCapacity && LastValidation.Allowed && OptionUnlocked(option);
        }
        if (destroyOption != null) destroyOption.SetActive(InteractionAllowed && occupied);
        if (buildMenu != null) buildMenu.gameObject.SetActive(InteractionAllowed && (occupied || siteSelected && LastValidation.failure != BuildFailure.OutsideView));
    }
    private void Refresh(BuildSlot slot) { if (slot != null) slot.SetVisual(slot == hovered, slot == selected); }
    public void CancelBuild()
    {
        var previous = selected; selected = null; siteSelected = false;
        Refresh(previous); SetHovered(null); HidePreview(); RefreshMenu();
        SetStatus("选择同层完整 2×2 地块建造 · 右键 / Esc 取消", true);
    }
    public void DestroySelectedTower()
    {
        if (!CanReceiveInput) return;
        if (selected == null || !selected.TryDestroyTower()) return;
        var demolished = selected;
        CancelBuild();
        // Repeated rebuilding must not leave an ever-growing list of empty
        // generated slots; authored map slots remain available for reuse.
        if (demolished.GeneratedPlacement)
        {
            demolished.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(demolished.gameObject); else DestroyImmediate(demolished.gameObject);
        }
    }
    public void Build(GameObject towerPrefab) => TryBuildSelected(towerPrefab);
    public bool TryBuildSelected(GameObject towerPrefab)
    {
        if (!CanReceiveInput || !IsUnlocked(towerPrefab)) return false;
        if (!siteSelected || towerPrefab == null || placementValidator == null) return false;
        if (!CanBuildMore) { RefreshMenu(); return false; }
        LastValidation = placementValidator.Validate(selectedSite, worldCamera);
        if (!LastValidation.Allowed) { ShowSite(selectedSite, LastValidation); RefreshMenu(); return false; }
        var obj = new GameObject("BuildSlot " + selectedSite.anchor);
        obj.layer = LayerMask.NameToLayer("BuildSlot");
        obj.transform.SetParent(placementValidator.DeploymentParent(selectedSite.Center), false);
        obj.transform.position = selectedSite.Center;
        var collider = obj.AddComponent<PolygonCollider2D>();
        collider.points = new Vector2[] {
            obj.transform.InverseTransformPoint(selectedSite.Corner(0, 0)), obj.transform.InverseTransformPoint(selectedSite.Corner(2, 0)),
            obj.transform.InverseTransformPoint(selectedSite.Corner(2, 2)), obj.transform.InverseTransformPoint(selectedSite.Corner(0, 2)) };
        var slot = obj.AddComponent<BuildSlot>(); slot.ConfigurePlacement(placementValidator, selectedSite);
        if (!slot.TryBuild(ResolveBuildPrefab(towerPrefab)))
        {
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
            return false;
        }
        CancelBuild(); SetStatus("建造完成 · 已占用 2×2 地块", true); return true;
    }
    void ShowSite(BuildFootprint site, BuildValidation validation)
    {
        if (Application.isPlaying)
        {
            if (preview == null) preview = new GameObject("2x2 build preview").AddComponent<BuildPlacementPreview>();
            preview.Show(site, validation.Allowed);
        }
        SetStatus(validation.Message, validation.Allowed);
    }
    void SetStatus(string message, bool allowed)
    {
        if (placementStatus == null) return;
        var parent = placementStatus.transform.parent;
        var statusRoot = parent != null && parent.name == "Build placement status"
            ? parent.gameObject : placementStatus.gameObject;
        statusRoot.SetActive(false);
        placementStatus.text = string.Empty;
    }
    void HidePreview() { if (preview != null) preview.Hide(); }
    void OnDisable() { CancelBuild(); }
    void OnDestroy()
    {
        if (preview == null) return;
        if (Application.isPlaying) Destroy(preview.gameObject); else DestroyImmediate(preview.gameObject);
    }
}
