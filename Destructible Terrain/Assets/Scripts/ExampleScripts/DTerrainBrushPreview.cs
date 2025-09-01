using UnityEngine;
using DTerrain;

public class DTerrainBrushPreview : MonoBehaviour
{
    [Header("References")]
    public ClickAndDestroyOptimized controller;   // dein Input/Brush-Script
    public BasicPaintableLayer secondaryLayer;    // sichtbare Ebene (für PPU & Sorting)

    [Header("Visuals")]
    [Range(0f, 1f)] public float previewAlpha = 0.5f;
    public int sortingOrderOffset = 999;          // über Terrain

    private GameObject go;
    private SpriteRenderer sr;
    private Texture2D cachedTex;
    private int cachedIndex = -1;
    private float cachedAngle = 9999f;

    void Start()
    {
        go = new GameObject("BrushPreview");
        go.transform.SetParent(secondaryLayer.transform, worldPositionStays: true);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = secondaryLayer.SortingLayerID;
        sr.sortingOrder = sortingOrderOffset;
        sr.color = new Color(1f, 1f, 1f, previewAlpha);
        sr.enabled = false; // erst zeigen wenn Shift gedrückt
    }

    void Update()
    {
        bool show = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        sr.enabled = show;
        if (!show || controller.brushTextures == null || controller.brushTextures.Length == 0) return;

        var idx = Mathf.Clamp(controller.selectedBrushIndex, 0, controller.brushTextures.Length - 1);
        var brush = controller.brushTextures[idx];
        if (brush == null) return;

        // Textur bei Brush-/Winkel-Wechsel neu rotieren (damit Preview 1:1 zum Stempel passt)
        if (idx != cachedIndex || Mathf.Abs(controller.brushRotationDeg - cachedAngle) > 0.01f || cachedTex == null)
        {
            cachedTex = RotateTexture(brush, controller.brushRotationDeg);
            cachedIndex = idx;
            cachedAngle = controller.brushRotationDeg;

            sr.sprite = Sprite.Create(
                cachedTex,
                new Rect(0, 0, cachedTex.width, cachedTex.height),
                new Vector2(0.5f, 0.5f),                 // Pivot zentriert
                secondaryLayer.PPU,
                0,
                SpriteMeshType.FullRect
            );
        }

        // Maus → Welt → local zum VisibleLayer
        Vector3 pWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 lp = pWorld - secondaryLayer.transform.position;

        // auf Pixelraster snappen (optional, für crisp rendering)
        float step = 1f / secondaryLayer.PPU;
        lp.x = Mathf.Round(lp.x / step) * step;
        lp.y = Mathf.Round(lp.y / step) * step;

        // zentrieren – Sprite hat Pivot=Center, also keine halbe Größe abziehen
        go.transform.localPosition = new Vector3(lp.x, lp.y, 0);
    }

    // einfache Rotation (gleich wie in ClickAndDestroyOptimized)
    Texture2D RotateTexture(Texture2D src, float angleDeg)
    {
        if (Mathf.Approximately(angleDeg, 0f)) return src;

        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        int sw = src.width, sh = src.height;
        int nw = Mathf.CeilToInt(Mathf.Abs(sw * cos) + Mathf.Abs(sh * sin));
        int nh = Mathf.CeilToInt(Mathf.Abs(sw * sin) + Mathf.Abs(sh * cos));

        var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, false, false);
        float cx = (sw - 1) * 0.5f, cy = (sh - 1) * 0.5f;
        float ncx = (nw - 1) * 0.5f, ncy = (nh - 1) * 0.5f;

        for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
            {
                float dx = x - ncx, dy = y - ncy;
                float sx = cos * dx + sin * dy + cx;
                float sy = -sin * dx + cos * dy + cy;

                Color c = new Color(0, 0, 0, 0);
                int isx = Mathf.RoundToInt(sx), isy = Mathf.RoundToInt(sy);
                if (isx >= 0 && isx < sw && isy >= 0 && isy < sh)
                    c = src.GetPixel(isx, isy);
                dst.SetPixel(x, y, c);
            }
        dst.Apply();
        return dst;
    }
}
