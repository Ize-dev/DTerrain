using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DTerrain
{
    /// <summary>
    /// Destroys a circle and builds a circle but only on secondary layer.
    /// Primary serves as logical layer for reducing sprite renderers and only handles collisions.
    /// Used in SampleScene2.
    /// </summary>
    public class ClickAndDestroyOptimized : ClickAndDestroy
    {
        public Texture2D[] brushTextures;
        private Shape[] brushShapes;
        public int selectedBrushIndex = 0;
        [Range(0f, 1f)] public float brushAlphaThreshold = 0.1f;
        public float brushRotationDeg = 0f;
        private bool brushStampedThisHold = false;


        List<(int start, int end)> GetColumnSegments(Texture2D src, int x, float alphaThreshold)
        {
            var segs = new List<(int, int)>();
            int y = 0;
            int h = src.height;

            while (y < h)
            {
                while (y < h && src.GetPixel(x, y).a <= alphaThreshold) y++;
                if (y >= h) break;

                int start = y;
                while (y < h && src.GetPixel(x, y).a > alphaThreshold) y++;
                int end = y; // exklusiv
                if (end > start)
                    segs.Add((start, end));
            }
            return segs;
        }

        List<RectInt> BuildMergedRects(Texture2D src, float alphaThreshold)
        {
            var result = new List<RectInt>();
            var active = new Dictionary<(int start, int end), (int x0, int x1, int y, int h)>();

            for (int x = 0; x < src.width; x++)
            {
                var segs = GetColumnSegments(src, x, alphaThreshold);
                var nextActive = new Dictionary<(int start, int end), (int x0, int x1, int y, int h)>();

                foreach (var seg in segs)
                {
                    int start = seg.start;
                    int end = seg.end;
                    int height = end - start;
                    var key = (start, end);

                    if (active.TryGetValue(key, out var rect))
                    {
                        rect.x1 = x;
                        nextActive[key] = rect;
                    }
                    else
                    {
                        nextActive[key] = (x, x, start, height);
                    }
                }

                foreach (var kv in active)
                {
                    if (!nextActive.ContainsKey(kv.Key))
                    {
                        var r = kv.Value;
                        int width = r.x1 - r.x0 + 1;
                        result.Add(new RectInt(r.x0, r.y, width, r.h));
                    }
                }

                active = nextActive;
            }

            foreach (var kv in active)
            {
                var r = kv.Value;
                int width = r.x1 - r.x0 + 1;
                result.Add(new RectInt(r.x0, r.y, width, r.h));
            }

            return result;
        }


        protected override void OnLeftMouseButtonClick()
        {

            Vector3 p = Camera.main.ScreenToWorldPoint(Input.mousePosition) - primaryLayer.transform.position;

            primaryLayer?.Paint(new PaintingParameters() 
            { 
                Color = Color.clear, 
                Position = new Vector2Int((int)(p.x * primaryLayer.PPU) - circleSize, (int)(p.y * primaryLayer.PPU) - circleSize), 
                Shape = destroyCircle, 
                PaintingMode=PaintingMode.REPLACE_COLOR,
                DestructionMode = DestructionMode.DESTROY
            });

            secondaryLayer?.Paint(new PaintingParameters() 
            {
                Color = Color.clear,
                Position = new Vector2Int((int)(p.x * secondaryLayer.PPU) - circleSize, (int)(p.y * secondaryLayer.PPU) - circleSize), 
                Shape = destroyCircle, 
                PaintingMode=PaintingMode.REPLACE_COLOR,
                DestructionMode = DestructionMode.NONE
            });
            
        }


        protected void OnBrushMaskClick()
        {
            if (brushTextures == null || brushTextures.Length == 0)
            {
                Debug.LogWarning("Kein Brush zugewiesen."); return;
            }
            var brush = brushTextures[Mathf.Clamp(selectedBrushIndex, 0, brushTextures.Length - 1)];
            if (!brush) { Debug.LogWarning("Brush Texture ist null."); return; }

            var src = RotateTexture(brush, brushRotationDeg);
            int bw = src.width, bh = src.height;

            Vector3 pWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            Vector3 lpSec = pWorld - secondaryLayer.transform.position;
            int secX = (int)(lpSec.x * secondaryLayer.PPU) - bw / 2;
            int secY = (int)(lpSec.y * secondaryLayer.PPU) - bh / 2;

            Vector3 lpPrim = pWorld - primaryLayer.transform.position;
            int primX = (int)(lpPrim.x * primaryLayer.PPU) - bw / 2;
            int primY = (int)(lpPrim.y * primaryLayer.PPU) - bh / 2;

            BlitBrushToVisibleLayer(src, secX, secY);

            var rects = BuildMergedRects(src, brushAlphaThreshold);
            foreach (var r in rects)
            {
                var rectShape = DTerrain.Shape.GenerateShapeRect(r.width, r.height);

                primaryLayer?.Paint(new DTerrain.PaintingParameters
                {
                    Color = Color.black, // alpha = 1
                    Position = new Vector2Int(primX + r.x, primY + r.y),
                    Shape = rectShape,
                    PaintingMode = DTerrain.PaintingMode.REPLACE_COLOR,
                    DestructionMode = DTerrain.DestructionMode.BUILD
                });
            }

        }




        protected override void OnRightMouseButtonClick()
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (brushStampedThisHold) return;   
                OnBrushMaskClick();                 
                brushStampedThisHold = true;
                return;
            }

            Vector3 p = Camera.main.ScreenToWorldPoint(Input.mousePosition) - primaryLayer.transform.position;

            primaryLayer?.Paint(new PaintingParameters()
            {
                Color = Color.black,
                Position = new Vector2Int((int)(p.x * primaryLayer.PPU) - circleSize, (int)(p.y * primaryLayer.PPU) - circleSize),
                Shape = destroyCircle,
                PaintingMode = PaintingMode.NONE,
                DestructionMode = DestructionMode.BUILD
            });

            secondaryLayer?.Paint(new PaintingParameters()
            {
                Color = Color.black,
                Position = new Vector2Int((int)(p.x * secondaryLayer.PPU) - circleSize, (int)(p.y * secondaryLayer.PPU) - circleSize),
                Shape = destroyCircle,
                PaintingMode = PaintingMode.REPLACE_COLOR,
                DestructionMode = DestructionMode.BUILD
            });
        }


        protected new void Start()
        {
            base.Start();

            brushShapes = new Shape[brushTextures.Length];
            for (int i = 0; i < brushTextures.Length; i++)
            {
                if (brushTextures[i] == null)
                {
                    Debug.LogError($"brushTextures[{i}] ist null!");
                    continue;
                }

                brushShapes[i] = BrushShapeUtils.ShapeFromTexture(brushTextures[i]);
                Debug.Log($"Brush {i} geladen: {brushTextures[i].name}, Shape.Ranges.Count = {brushShapes[i].Ranges.Count}");
            }
        }

        protected new void Update()
        {
            base.Update();

            if (Input.GetKey(KeyCode.Q)) brushRotationDeg -= 5f;
            if (Input.GetKey(KeyCode.E)) brushRotationDeg += 5f;

            if (Input.GetKeyDown(KeyCode.Alpha1))
                selectedBrushIndex = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) && brushShapes.Length > 1)
                selectedBrushIndex = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) && brushShapes.Length > 2)
                selectedBrushIndex = 2;

            if (Input.GetMouseButtonUp(1)) brushStampedThisHold = false;
        }

        void BlitTexture(Texture2D target, Texture2D src, int dstX, int dstY, float alphaThreshold)
        {
            int w = src.width;
            int h = src.height;

            for (int y = 0; y < h; y++)
            {
                int ty = dstY + y;
                if (ty < 0 || ty >= target.height) continue;

                for (int x = 0; x < w; x++)
                {
                    int tx = dstX + x;
                    if (tx < 0 || tx >= target.width) continue;

                    Color sc = src.GetPixel(x, y);
                    if (sc.a <= alphaThreshold) continue; 

                    Color dc = target.GetPixel(tx, ty);

                    
                    float outA = sc.a + dc.a * (1f - sc.a);
                    Color outRGB = (sc * sc.a + dc * dc.a * (1f - sc.a));
                    if (outA > 0f) outRGB /= outA;

                    Color outC = new Color(outRGB.r, outRGB.g, outRGB.b, outA);
                    target.SetPixel(tx, ty, outC);
                }
            }
            target.Apply();
        }

        
        Texture2D GetSecondaryTexture()
        {
            var sr = secondaryLayer != null ? secondaryLayer.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr.sprite?.texture : null;
        }

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

        static Color BlendSrcOver(Color dst, Color src)
        {
            float outA = src.a + dst.a * (1f - src.a);

            float outR = src.r * src.a + dst.r * dst.a * (1f - src.a);
            float outG = src.g * src.a + dst.g * dst.a * (1f - src.a);
            float outB = src.b * src.a + dst.b * dst.a * (1f - src.a);

            if (outA > 1e-6f) { outR /= outA; outG /= outA; outB /= outA; }
            else { outR = outG = outB = 0f; }

            return new Color(outR, outG, outB, outA);
        }
        void BlitBrushToVisibleLayer(Texture2D src, int baseX, int baseY)
        {
            var affected = new HashSet<PaintableChunk>();
            for (int y = 0; y < src.height; y++)
            {
                for (int x = 0; x < src.width; x++)
                {
                    Color sc = src.GetPixel(x, y);
                    if (sc.a <= brushAlphaThreshold) continue;

                    int gx = baseX + x;
                    int gy = baseY + y;
                    if (gx < 0 || gy < 0) continue; 

                    
                    var chunk = secondaryLayer.GetChunkByPosition(new Vector2Int(gx, gy));
                    if (chunk == null || chunk.TextureSource?.Texture == null) continue;

                    Vector3 worldPos = secondaryLayer.transform.position + new Vector3(gx / (float)secondaryLayer.PPU, gy / (float)secondaryLayer.PPU, 0f);
                    Vector3 chunkLocal = worldPos - chunk.transform.position;
                    int cx = Mathf.FloorToInt(chunkLocal.x * secondaryLayer.PPU);
                    int cy = Mathf.FloorToInt(chunkLocal.y * secondaryLayer.PPU);

                    var tex = chunk.TextureSource.Texture;
                    if (cx < 0 || cy < 0 || cx >= tex.width || cy >= tex.height) continue;

                    Color dc = tex.GetPixel(cx, cy);
                    tex.SetPixel(cx, cy, BlendSrcOver(dc, sc));
                    affected.Add(chunk);
                }
            }
            foreach (var c in affected) c.TextureSource.Texture.Apply();
        }

        void BlitTextureAlpha(Texture2D target, Texture2D src, int dstX, int dstY, float alphaThreshold)
        {
            int w = src.width, h = src.height;

            for (int y = 0; y < h; y++)
            {
                int ty = dstY + y;
                if (ty < 0 || ty >= target.height) continue;

                for (int x = 0; x < w; x++)
                {
                    int tx = dstX + x;
                    if (tx < 0 || tx >= target.width) continue;

                    Color sc = src.GetPixel(x, y);
                    if (sc.a <= alphaThreshold) continue;

                    Color dc = target.GetPixel(tx, ty);

                    float outA = sc.a + dc.a * (1f - sc.a);
                    Color outRGB = (sc * sc.a) + (dc * dc.a * (1f - sc.a));
                    if (outA > 0f) outRGB /= outA;

                    target.SetPixel(tx, ty, new Color(outRGB.r, outRGB.g, outRGB.b, outA));
                }
            }
            target.Apply();
        }

        Texture2D GetSecondaryTextureRW()
        {
            var sr = secondaryLayer ? secondaryLayer.GetComponent<SpriteRenderer>() : null;
            var tex = sr ? sr.sprite?.texture : null;
            return tex;
        }


    }
}
