using UnityEngine;

public class BuildSlot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer highlight;
    [SerializeField] private Transform towerOrigin;
    [SerializeField] private GameObject existingTower;

    public bool Occupied => existingTower != null;
    public bool GeneratedPlacement { get; private set; }
    private BuildPlacementValidator placementValidator;
    private BuildFootprint footprint;

    public void ConfigurePlacement(BuildPlacementValidator validator, BuildFootprint site)
    {
        placementValidator = validator; footprint = site; GeneratedPlacement = true;
        towerOrigin = transform; transform.position = site.Center;
    }

    private void Awake()
    {
        SetVisual(false, false);
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
        if (Application.isPlaying) Destroy(existingTower); else DestroyImmediate(existingTower);
        existingTower = null;
        return true;
    }

    public bool TryBuild(GameObject prefab)
    {
        if (Occupied || prefab == null)
            return false;

        if (placementValidator == null)
            foreach (var validator in FindObjectsOfType<BuildPlacementValidator>())
                if (validator.gameObject.scene == gameObject.scene) { placementValidator = validator; break; }
        if (placementValidator == null) return false;
        if (footprint.surface == null && !placementValidator.TryFootprintAtCenter(transform.position, out footprint)) return false;
        var selection = placementValidator.GetComponent<BuildSelection>();
        // Enforce the same world budget even when construction bypasses the menu.
        if (selection != null && !selection.CanBuildMore) return false;
        var camera = selection != null ? selection.WorldCamera : Camera.main;
        if (!placementValidator.Validate(footprint, camera).Allowed) return false;

        existingTower = Instantiate(
            prefab,
            footprint.Center,
            Quaternion.identity
        );

        SetVisual(false, false);
        return true;
    }
}
