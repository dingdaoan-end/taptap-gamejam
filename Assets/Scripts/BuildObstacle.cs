using UnityEngine;

// Mark obstacles outside Ground hierarchies, or props without a collider.
[DisallowMultipleComponent]
public sealed class BuildObstacle : MonoBehaviour
{
    public Vector2 size = Vector2.one;
    public Vector2 offset;
    public bool useRendererBounds = true;
    public Bounds WorldBounds
    {
        get
        {
            var sprites = GetComponentsInChildren<SpriteRenderer>();
            if (useRendererBounds && sprites.Length > 0)
            {
                var b = sprites[0].bounds;
                foreach (var sprite in sprites) b.Encapsulate(sprite.bounds);
                return b;
            }
            Vector3 s = transform.lossyScale;
            return new Bounds(transform.TransformPoint(offset), new Vector3(Mathf.Abs(size.x * s.x), Mathf.Abs(size.y * s.y), .1f));
        }
    }
    void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size); }
}
