using UnityEngine;

namespace TapTapGameJam.Combat
{
    // Instant-hit beam: impact and health change share the beat, with no travel delay.
    public sealed class TowerAttackFeedback : MonoBehaviour
    {
        LineRenderer line;
        Material material;
        float hideAt;
        void Awake()
        {
            var child = new GameObject("Beat attack beam"); child.transform.SetParent(transform, false);
            line = child.AddComponent<LineRenderer>(); line.useWorldSpace = true;
            material = new Material(Shader.Find("Sprites/Default")); line.sharedMaterial = material;
            line.positionCount = 2; line.widthMultiplier = .065f;
            line.sortingLayerName = "UI"; line.sortingOrder = 90; line.enabled = false;
        }
        public void Show(Vector3 from, Vector3 to, Color color)
        {
            from.z = to.z = -.1f;
            line.SetPosition(0, from); line.SetPosition(1, to);
            line.startColor = line.endColor = color;
            line.enabled = true; hideAt = Time.unscaledTime + .12f;
        }
        void Update() { if (Time.unscaledTime >= hideAt) Hide(); }
        public void Hide() { if (line != null) line.enabled = false; }
        void OnDisable() => Hide();
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
