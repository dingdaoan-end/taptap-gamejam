using UnityEngine;

public class BuildSlot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer highlight;
    [SerializeField] private Transform towerOrigin;
    [SerializeField] private GameObject existingTower;

    public bool Occupied => existingTower != null;

    private void Awake()
    {
        SetVisual(false, false);
    }

    public void SetVisual(bool hovered, bool selected)
    {
        highlight.enabled = hovered || selected;

        // 点击选中比悬停更明显。
        highlight.color = selected
            ? new Color(1f, 0.85f, 0.2f, 0.55f)
            : new Color(0.4f, 1f, 0.5f, 0.35f);
    }

    public bool TryDestroyTower()
    {
        if (!Occupied)
            return false;

        // Destroy completes at the end of the frame; hide the tower immediately.
        existingTower.SetActive(false);
        Destroy(existingTower);
        existingTower = null;
        return true;
    }

    public bool TryBuild(GameObject prefab)
    {
        if (Occupied || prefab == null)
            return false;

        existingTower = Instantiate(
            prefab,
            towerOrigin.position,
            Quaternion.identity
        );

        SetVisual(false, false);
        return true;
    }
}
