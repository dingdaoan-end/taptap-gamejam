using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildSelection : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask slotLayer;
    [SerializeField] private RectTransform buildMenu;
    [SerializeField] private GameObject[] buildOptions;
    [SerializeField] private GameObject destroyOption;
    [SerializeField] private Vector2 menuOffset;
    [SerializeField] private BuildPlacementValidator placementValidator;
    [SerializeField] private Text placementStatus;
    private BuildSlot hovered, selected;
    private BuildFootprint selectedSite;
    private bool siteSelected;
    private BuildPlacementPreview preview;
    public Camera WorldCamera => worldCamera;
    public BuildValidation LastValidation { get; private set; }

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (placementValidator == null) placementValidator = GetComponent<BuildPlacementValidator>();
        if (placementValidator == null) placementValidator = gameObject.AddComponent<BuildPlacementValidator>();
        if (buildMenu != null) buildMenu.gameObject.SetActive(false);
        SetStatus(string.Empty, false);
    }

    private void Update()
    {
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
        CancelBuild();
        LastValidation = placementValidator.Validate(site, worldCamera);
        ShowSite(site, LastValidation);
        if (!LastValidation.Allowed) return false;
        selectedSite = site; siteSelected = true;
        RefreshMenu(); return true;
    }

    private void LateUpdate()
    {
        if (buildMenu == null || worldCamera == null || selected == null && !siteSelected) return;
        Vector3 center = selected != null ? selected.transform.position : selectedSite.Center;
        buildMenu.position = worldCamera.WorldToScreenPoint(center) + (Vector3)menuOffset;
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
        if (buildOptions != null) foreach (var option in buildOptions)
        {
            if (option == null) continue;
            option.SetActive(siteSelected && !occupied);
            var button = option.GetComponent<Button>();
            if (button != null) button.interactable = siteSelected && LastValidation.Allowed;
        }
        if (destroyOption != null) destroyOption.SetActive(occupied);
        if (buildMenu != null) buildMenu.gameObject.SetActive(occupied || siteSelected && LastValidation.failure != BuildFailure.OutsideView);
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
        if (selected == null || !selected.TryDestroyTower()) return;
        CancelBuild();
    }
    public void Build(GameObject towerPrefab) => TryBuildSelected(towerPrefab);
    public bool TryBuildSelected(GameObject towerPrefab)
    {
        if (!siteSelected || towerPrefab == null || placementValidator == null) return false;
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
        if (!slot.TryBuild(towerPrefab))
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
