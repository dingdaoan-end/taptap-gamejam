using UnityEngine;
using UnityEngine.EventSystems;

public class BuildSelection : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask slotLayer;
    [SerializeField] private RectTransform buildMenu;
    [SerializeField] private GameObject[] buildOptions;
    [SerializeField] private GameObject destroyOption;
    [SerializeField] private Vector2 menuOffset = new Vector2(0f, 0f);

    private BuildSlot hovered;
    private BuildSlot selected;

    private void Awake()
    {
        buildMenu.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            CancelBuild();
            return;
        }

        // 点击 UI 时，不把这次点击传给地图。
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            SetHovered(null);
            return;
        }

        // 把屏幕上的鼠标位置投影到地图所在的 XY 平面。
        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        Plane mapPlane = new Plane(Vector3.forward, Vector3.zero);

        BuildSlot candidate = null;

        if (mapPlane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);

            Collider2D hit = Physics2D.OverlapPoint(
                new Vector2(point.x, point.y),
                slotLayer
            );

            if (hit != null)
                candidate = hit.GetComponent<BuildSlot>();
        }

        SetHovered(candidate);

        if (Input.GetMouseButtonDown(0))
            Select(candidate);
    }

    private void LateUpdate()
    {
        if (selected == null)
            return;

        // Overlay Canvas 使用屏幕坐标定位。
        Vector3 screenPoint =
            worldCamera.WorldToScreenPoint(selected.transform.position);

        buildMenu.position =
            screenPoint + new Vector3(menuOffset.x, menuOffset.y, 0f);
    }

    private void SetHovered(BuildSlot next)
    {
        if (hovered == next)
            return;

        BuildSlot previous = hovered;
        hovered = next;

        Refresh(previous);
        Refresh(hovered);
    }

    private void Select(BuildSlot next)
    {
        BuildSlot previous = selected;
        selected = next;

        Refresh(previous);
        Refresh(selected);

        bool occupied = selected != null && selected.Occupied;
        foreach (GameObject option in buildOptions)
            option.SetActive(selected != null && !occupied);
        destroyOption.SetActive(occupied);
        buildMenu.gameObject.SetActive(selected != null);
    }

    private void Refresh(BuildSlot slot)
    {
        if (slot != null)
            slot.SetVisual(slot == hovered, slot == selected);
    }

    public void CancelBuild()
    {
        Select(null);
        SetHovered(null);
    }

    public void DestroySelectedTower()
    {
        if (selected == null || !selected.TryDestroyTower())
            return;

        CancelBuild();
    }

    public void Build(GameObject towerPrefab)
    {
        if (selected == null)
            return;

        if (selected.TryBuild(towerPrefab))
        {
            Select(null);
            SetHovered(null);
        }
    }
}
