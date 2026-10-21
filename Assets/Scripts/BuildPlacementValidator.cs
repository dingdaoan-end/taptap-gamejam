using System.Collections.Generic;
using System.Linq;
using TapTapGameJam.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

public struct BuildFootprint
{
    public Tilemap surface;
    public Vector3Int anchor;
    public Vector3 Corner(int x, int y) => surface.CellToWorld(anchor + new Vector3Int(x, y, 0));
    public Vector3 Center => (Corner(0, 0) + Corner(2, 2)) * .5f;
    public Vector3 CellCenter(int x, int y) => (Corner(x, y) + Corner(x + 1, y + 1)) * .5f;
    public Bounds Bounds
    {
        get
        {
            var b = new Bounds(Corner(0, 0), Vector3.zero);
            b.Encapsulate(Corner(2, 0)); b.Encapsulate(Corner(2, 2)); b.Encapsulate(Corner(0, 2));
            return b;
        }
    }
}
public enum BuildFailure { None, MissingGround, MixedGroundLevels, OutsideView, TowerRange, Occupied }
public struct BuildValidation
{
    public BuildFailure failure;
    public bool Allowed => failure == BuildFailure.None;
    public string Message
    {
        get
        {
            switch (failure)
            {
                case BuildFailure.None: return "可以建造 · 同层完整 2×2，四格均在视口内";
                case BuildFailure.MissingGround: return "不可建造 · 需要 Ground level 1–4 的完整 2×2 地块";
                case BuildFailure.MixedGroundLevels: return "不可建造 · 2×2 四格必须属于同一 Ground 层级";
                case BuildFailure.OutsideView: return "不可建造 · 2×2 的四格必须全部在当前视口内";
                case BuildFailure.TowerRange: return "不可建造 · 与已有防御塔攻击范围相交";
                default: return "不可建造 · 存在障碍物、道路、基地或已建造的塔";
            }
        }
    }
}
[DisallowMultipleComponent]
public sealed class BuildPlacementValidator : MonoBehaviour
{
    [SerializeField] Tilemap[] groundLevels = new Tilemap[0];
    [SerializeField] Tilemap[] blockedTilemaps = new Tilemap[0];
    [SerializeField] LayerMask obstacleLayers = 1 << 6;
    PolygonCollider2D probe;
    readonly List<Collider2D> contacts = new List<Collider2D>();
    const float Epsilon = .00001f;
    public static int GroundLevel(Tilemap map)
    {
        if (map == null) return 0;
        string n = map.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        for (int i = 1; i <= 4; i++) if (n == "groundlevel" + i) return i;
        return 0;
    }
    void Awake() => RefreshSurfaces();
    public void RefreshSurfaces()
    {
        var maps = FindObjectsOfType<Tilemap>().Where(m => m.gameObject.scene == gameObject.scene).ToArray();
        groundLevels = maps.Where(m => GroundLevel(m) > 0).OrderByDescending(GroundLevel).ToArray();
        var blockers = new HashSet<Tilemap>(blockedTilemaps.Where(m => m != null));
        foreach (var map in maps)
        {
            string n = map.name.Replace(" ", "").ToLowerInvariant();
            if (GroundLevel(map) == 0 && (groundLevels.Any(g => map != g && map.transform.IsChildOf(g.transform)) || n == "paths" || n == "base" || n.Contains("obstacle") || n.Contains("障碍"))) blockers.Add(map);
        }
        blockedTilemaps = blockers.ToArray();
    }
    Tilemap SurfaceAt(Vector3 world)
    {
        if (groundLevels == null || groundLevels.Length == 0) RefreshSurfaces();
        foreach (var map in groundLevels)
            if (map != null && map.isActiveAndEnabled && GroundLevel(map) > 0 && map.HasTile(map.WorldToCell(world))) return map;
        return null;
    }
    public bool TryFootprint(Vector3 world, out BuildFootprint footprint)
    {
        var surface = SurfaceAt(world) ?? groundLevels.FirstOrDefault(m => m != null && m.isActiveAndEnabled);
        footprint = new BuildFootprint { surface = surface, anchor = surface == null ? Vector3Int.zero : surface.WorldToCell(world) };
        return surface != null;
    }
    public bool TryFootprintAtCenter(Vector3 center, out BuildFootprint footprint)
    {
        if (!TryFootprint(center, out footprint)) return false;
        Vector3 axes = footprint.surface.CellToWorld(new Vector3Int(1, 1, 0)) - footprint.surface.CellToWorld(Vector3Int.zero);
        footprint.anchor = footprint.surface.WorldToCell(center + axes * Epsilon) - new Vector3Int(1, 1, 0);
        return true;
    }
    public BuildValidation Validate(BuildFootprint f, Camera camera)
    {
        if (f.surface == null || !f.surface.isActiveAndEnabled || GroundLevel(f.surface) == 0) return Fail(BuildFailure.MissingGround);
        for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
        {
            var surface = SurfaceAt(f.CellCenter(x, y));
            if (surface == null) return Fail(BuildFailure.MissingGround);
            if (GroundLevel(surface) != GroundLevel(f.surface)) return Fail(BuildFailure.MixedGroundLevels);
        }
        if (!FullyVisible(f, camera)) return Fail(BuildFailure.OutsideView);
        PrepareProbe(f);
        try
        {
            foreach (var slot in FindObjectsOfType<BuildSlot>())
                if (slot.gameObject.scene == gameObject.scene && slot.Occupied)
                {
                    var collider = slot.GetComponent<Collider2D>();
                    if (collider != null ? Intersects(collider, false) : Overlaps(f.Bounds, new Bounds(slot.transform.position, f.Bounds.size))) return Fail(BuildFailure.Occupied);
                }
            foreach (var map in blockedTilemaps)
                if (map != null && map.isActiveAndEnabled && TilemapOverlaps(map, f)) return Fail(BuildFailure.Occupied);
            foreach (var ground in groundLevels)
            {
                if (ground == null || !ground.isActiveAndEnabled) continue;
                foreach (var collider in ground.GetComponentsInChildren<Collider2D>())
                    if (collider.gameObject != ground.gameObject && collider.GetComponent<BuildSlot>() == null && Intersects(collider, false)) return Fail(BuildFailure.Occupied);
                foreach (var sprite in ground.GetComponentsInChildren<SpriteRenderer>())
                    if (sprite.enabled && !IsAttackRange(sprite) && Overlaps(f.Bounds, sprite.bounds)) return Fail(BuildFailure.Occupied);
            }
            foreach (var obstacle in FindObjectsOfType<BuildObstacle>())
                if (obstacle.isActiveAndEnabled && obstacle.gameObject.scene == gameObject.scene && !IsAttackRange(obstacle) && Overlaps(f.Bounds, obstacle.WorldBounds)) return Fail(BuildFailure.Occupied);
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(obstacleLayers);
            probe.OverlapCollider(filter, contacts);
            if (contacts.Any(c => c != null && c != probe && c.GetComponent<BuildSlot>() == null && Intersects(c, false))) return Fail(BuildFailure.Occupied);
            return Fail(BuildFailure.None);
        }
        finally { probe.enabled = false; }
    }
    public static bool FullyVisible(BuildFootprint f, Camera camera)
    {
        if (camera == null || !camera.isActiveAndEnabled || camera.pixelRect.width < 1 || camera.pixelRect.height < 1) return false;
        for (int y = 0; y <= 2; y++) for (int x = 0; x <= 2; x++)
        {
            Vector3 p = camera.WorldToViewportPoint(f.Corner(x, y));
            if (p.z < camera.nearClipPlane || p.z > camera.farClipPlane || p.x < -Epsilon || p.x > 1 + Epsilon || p.y < -Epsilon || p.y > 1 + Epsilon) return false;
        }
        return true;
    }
    void PrepareProbe(BuildFootprint f)
    {
        if (probe == null)
        {
            var obj = new GameObject("Construction footprint query") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            probe = obj.AddComponent<PolygonCollider2D>(); probe.isTrigger = true;
        }
        probe.points = new Vector2[] { f.Corner(0, 0), f.Corner(2, 0), f.Corner(2, 2), f.Corner(0, 2) };
        probe.enabled = true; Physics2D.SyncTransforms();
    }
    // Range sensors and their visual children are not physical tower occupancy.
    // Apply this to every obstacle query, including Building-layer triggers.
    static bool IsAttackRange(Component component) => component.GetComponentInParent<TowerEnemyDetector>() != null;
    bool Intersects(Collider2D collider, bool includeTouch)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || IsAttackRange(collider)) return false;
        var d = Physics2D.Distance(probe, collider);
        return d.isValid && (includeTouch ? d.distance <= Epsilon : d.distance < -Epsilon);
    }
    static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x - Epsilon && a.max.x > b.min.x + Epsilon && a.min.y < b.max.y - Epsilon && a.max.y > b.min.y + Epsilon;
    static bool TilemapOverlaps(Tilemap map, BuildFootprint f)
    {
        var a = map.WorldToCell(f.Corner(0, 0)); var b = map.WorldToCell(f.Corner(2, 2));
        var c = map.WorldToCell(f.Corner(2, 0)); var d = map.WorldToCell(f.Corner(0, 2));
        int minX = Mathf.Min(a.x, b.x, c.x, d.x), maxX = Mathf.Max(a.x, b.x, c.x, d.x);
        int minY = Mathf.Min(a.y, b.y, c.y, d.y), maxY = Mathf.Max(a.y, b.y, c.y, d.y);
        if ((long)(maxX - minX + 1) * (maxY - minY + 1) > 10000) return true;
        for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
        {
            var cell = new Vector3Int(x, y, a.z);
            if (!map.HasTile(cell)) continue;
            var bounds = new Bounds(map.CellToWorld(cell), Vector3.zero);
            bounds.Encapsulate(map.CellToWorld(cell + Vector3Int.right)); bounds.Encapsulate(map.CellToWorld(cell + Vector3Int.up));
            bounds.Encapsulate(map.CellToWorld(cell + new Vector3Int(1, 1, 0)));
            if (Overlaps(f.Bounds, bounds)) return true;
        }
        return false;
    }
    public Transform DeploymentParent(Vector3 world)
    {
        Transform nearest = null; float distance = float.PositiveInfinity;
        foreach (var slot in FindObjectsOfType<BuildSlot>())
        {
            if (slot.gameObject.scene != gameObject.scene || slot.GeneratedPlacement) continue;
            float candidate = (slot.transform.position - world).sqrMagnitude;
            if (candidate >= distance) continue;
            nearest = slot.transform.parent; distance = candidate;
        }
        return nearest;
    }
    static BuildValidation Fail(BuildFailure reason) => new BuildValidation { failure = reason };
    void OnDestroy()
    {
        if (probe == null) return;
        if (Application.isPlaying) Destroy(probe.gameObject); else DestroyImmediate(probe.gameObject);
    }
}
