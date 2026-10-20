using UnityEngine;

// Fit a fixed world rectangle inside the display without revealing neighboring map cells.
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class FixedWorldCameraFrame : MonoBehaviour
{
    Camera worldCamera, backdrop;
    Rect originalViewport, bounds;
    bool active, locked;
    public bool IsActive => active;

    public void Begin(Rect worldBounds)
    {
        if (worldCamera == null) worldCamera = GetComponent<Camera>();
        if (!active) originalViewport = worldCamera.rect;
        bounds = worldBounds;
        bounds.width = Mathf.Max(.1f, bounds.width);
        bounds.height = Mathf.Max(.1f, bounds.height);
        active = true; locked = false;
        if (backdrop == null)
        {
            var obj = new GameObject("Camera frame background", typeof(Camera));
            obj.transform.SetParent(transform, false);
            backdrop = obj.GetComponent<Camera>();
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = Color.black;
            backdrop.cullingMask = 0;
            backdrop.orthographic = true;
        }
        backdrop.enabled = true;
        Refresh();
    }

    public void Complete()
    {
        if (!active) return;
        locked = true;
        Refresh();
    }

    public void Release()
    {
        if (!active) return;
        active = locked = false;
        if (worldCamera != null) { worldCamera.rect = originalViewport; worldCamera.ResetAspect(); }
        if (backdrop != null) backdrop.enabled = false;
    }

    void LateUpdate() { if (active) Refresh(); }

    void Refresh()
    {
        if (worldCamera == null) return;
        float width = worldCamera.targetTexture != null ? worldCamera.targetTexture.width : Screen.width;
        float height = worldCamera.targetTexture != null ? worldCamera.targetTexture.height : Screen.height;
        worldCamera.rect = FitViewport(originalViewport, width, height, bounds.width / bounds.height);
        worldCamera.aspect = bounds.width / bounds.height;
        backdrop.rect = originalViewport;
        backdrop.depth = worldCamera.depth - 1;
        backdrop.targetDisplay = worldCamera.targetDisplay;
        if (!locked) return;
        worldCamera.orthographicSize = bounds.height / 2;
        var position = worldCamera.transform.position;
        worldCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, position.z);
    }

    static Rect FitViewport(Rect available, float pixelWidth, float pixelHeight, float aspect)
    {
        float displayAspect = Mathf.Max(1, pixelWidth) * available.width / (Mathf.Max(1, pixelHeight) * available.height);
        if (displayAspect > aspect)
        {
            float width = available.width * aspect / displayAspect;
            return new Rect(available.x + (available.width - width) / 2, available.y, width, available.height);
        }
        float height = available.height * displayAspect / aspect;
        return new Rect(available.x, available.y + (available.height - height) / 2, available.width, height);
    }

    void OnDisable() { Release(); }
    void OnDestroy()
    {
        if (backdrop == null) return;
        if (Application.isPlaying) Destroy(backdrop.gameObject); else DestroyImmediate(backdrop.gameObject);
    }
}
