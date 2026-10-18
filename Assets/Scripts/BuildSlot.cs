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
        BindTowerSection();
    }

    private void BindTowerSection()
    {
        if (existingTower == null) return;
        foreach (var bell in existingTower.GetComponentsInChildren<TapTapGameJam.Music.BellTowerMusicController>(true))
            bell.BindDeploymentSlot(transform);
    }

    public void SetVisual(bool hovered, bool selected)
    {
        if (highlight == null) return;
        if (TryGetSectionColor(out Color sectionColor))
        {
            // Keep the section visible at rest; interaction changes brightness
            // and opacity rather than replacing the section's identifying hue.
            highlight.enabled = true;
            Color color = selected ? Color.Lerp(sectionColor, Color.white, 0.25f) : sectionColor;
            color.a = selected ? 0.65f : hovered ? 0.5f : 0.3f;
            highlight.color = color;
            return;
        }

        highlight.enabled = hovered || selected;

        // 点击选中比悬停更明显。
        highlight.color = selected
            ? new Color(1f, 0.85f, 0.2f, 0.55f)
            : new Color(0.4f, 1f, 0.5f, 0.35f);
    }

    private bool TryGetSectionColor(out Color color)
    {
        for (Transform ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent)
        {
            string section = ancestor.name.Replace(" ", "").Replace("_", "").Replace("-", "").ToUpperInvariant();
            switch (section)
            {
                case "SECTIONA": color = new Color(0.4f, 1f, 0.5f); return true;
                case "SECTIONB": color = new Color(1f, 0.85f, 0.2f); return true;
                case "SECTIONC": color = new Color(0.25f, 0.6f, 1f); return true;
            }
        }
        color = default;
        return false;
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
        BindTowerSection();

        SetVisual(false, false);
        return true;
    }
}
