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
        public float brushRotationDeg = 0f; // Q/E ansteuerbar, wenn du magst


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

        protected void OnBrushMaskClickOld()
        {
            Vector3 p = Camera.main.ScreenToWorldPoint(Input.mousePosition) - primaryLayer.transform.position;

            if (brushShapes.Length == 0 || brushTextures.Length == 0 || selectedBrushIndex >= brushShapes.Length)
            {
                Debug.LogWarning("Kein gültiger Brush ausgewählt.");
                return;
            }

            Shape shape = brushShapes[selectedBrushIndex];
            Texture2D brush = brushTextures[selectedBrushIndex];

            Vector2Int position = new Vector2Int(
                (int)(p.x * primaryLayer.PPU) - brush.width / 2,
                (int)(p.y * primaryLayer.PPU) - brush.height / 2
            );

            if (brushShapes == null || brushTextures == null)
            {
                Debug.LogError("Brush-Daten nicht initialisiert!");
                return;
            }

            if (selectedBrushIndex >= brushShapes.Length || brushShapes[selectedBrushIndex] == null)
            {
                Debug.LogError("Ungültiger oder leerer BrushShape bei Index " + selectedBrushIndex);
                return;
            }

            if (primaryLayer == null)
            {
                Debug.LogError("primaryLayer ist null!");
                return;
            }

            if (secondaryLayer == null)
            {
                Debug.LogError("secondaryLayer ist null!");
                return;
            }

            Debug.Log($"BrushShape valid: {brushShapes[selectedBrushIndex] != null}");
            Debug.Log($"PrimaryLayer valid: {primaryLayer != null}");
            Debug.Log($"SecondaryLayer valid: {secondaryLayer != null}");
            Debug.Log($"Ranges count: {brushShapes[selectedBrushIndex]?.Ranges?.Count}");


            primaryLayer?.Paint(new PaintingParameters()
            {
                Color = Color.black,
                Position = position,
                Shape = shape,
                PaintingMode = PaintingMode.NONE,
                DestructionMode = DestructionMode.BUILD
            });

            Debug.Log($"BrushShape valid: {brushShapes[selectedBrushIndex] != null}");
            Debug.Log($"PrimaryLayer valid: {primaryLayer != null}");
            Debug.Log($"SecondaryLayer valid: {secondaryLayer != null}");
            Debug.Log($"Ranges count: {brushShapes[selectedBrushIndex]?.Ranges?.Count}");


            secondaryLayer?.Paint(new PaintingParameters()
            {
                Color = Color.black,
                Position = position,
                Shape = shape,
                PaintingMode = PaintingMode.REPLACE_COLOR,
                DestructionMode = DestructionMode.BUILD
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

            // Optional Rotation
            var src = RotateTexture(brush, brushRotationDeg);
            int bw = src.width, bh = src.height;

            // Maus → Welt
            Vector3 pWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Welt → Pixel je Layer (PPU/Transform sind pro Layer!)
            // — Visible (secondary)
            Vector3 lpSec = pWorld - secondaryLayer.transform.position;
            int secX = (int)(lpSec.x * secondaryLayer.PPU) - bw / 2;
            int secY = (int)(lpSec.y * secondaryLayer.PPU) - bh / 2;

            // — Collidable (primary)
            Vector3 lpPrim = pWorld - primaryLayer.transform.position;
            int primX = (int)(lpPrim.x * primaryLayer.PPU) - bw / 2;
            int primY = (int)(lpPrim.y * primaryLayer.PPU) - bh / 2;

            // 1) SICHTBAR: PNG farbig + Alpha in die Runtime-Chunk-Texturen blenden
            BlitBrushToVisibleLayer(src, secX, secY);

            // 2) KOLLISION: exakte Silhouette – Alpha-Runs pro Spalte als 1px-Shape stempeln
            for (int x = 0; x < bw; x++)
            {
                int y = 0;
                while (y < bh)
                {
                    // transparente Bereiche überspringen
                    while (y < bh && src.GetPixel(x, y).a <= brushAlphaThreshold) y++;
                    if (y >= bh) break;

                    int startY = y;
                    while (y < bh && src.GetPixel(x, y).a > brushAlphaThreshold) y++;
                    int endY = y; // exklusiv
                    int len = endY - startY;
                    if (len <= 0) continue;

                    var colShape = DTerrain.Shape.GenerateShapeRange(len);

                    // primaryLayer: REPLACE_COLOR mit Deckkraft > 0, damit IsOccupied() solide erkennt
                    primaryLayer?.Paint(new DTerrain.PaintingParameters
                    {
                        Color = Color.black, // alpha 1 → solide
                        Position = new Vector2Int(primX + x, primY + startY),
                        Shape = colShape,
                        PaintingMode = DTerrain.PaintingMode.REPLACE_COLOR,
                        DestructionMode = DTerrain.DestructionMode.BUILD
                    });

                    // secondaryLayer: GEOMETRIE optional mitbauen (Farbe kommt oben vom Blit)
                    secondaryLayer?.Paint(new DTerrain.PaintingParameters
                    {
                        Color = Color.black, // egal, wir blitten; NONE wäre okay, wenn du hier nichts willst
                        Position = new Vector2Int(secX + x, secY + startY),
                        Shape = colShape,
                        PaintingMode = DTerrain.PaintingMode.NONE,
                        DestructionMode = DTerrain.DestructionMode.BUILD
                    });
                }
            }
        }




        protected override void OnRightMouseButtonClick()
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                OnBrushMaskClick(); // PNG Brush anwenden
                return;
            }

            // Original-Kreis-Mal-Logik
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

        // Alpha-blit: PNG-Farben inkl. Alpha auf Ziel-Texture stempeln
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
                    if (sc.a <= alphaThreshold) continue; // echte Transparenz bleibt transparent

                    Color dc = target.GetPixel(tx, ty);

                    // klassisches „source over“-Blending
                    float outA = sc.a + dc.a * (1f - sc.a);
                    Color outRGB = (sc * sc.a + dc * dc.a * (1f - sc.a));
                    if (outA > 0f) outRGB /= outA;

                    Color outC = new Color(outRGB.r, outRGB.g, outRGB.b, outA);
                    target.SetPixel(tx, ty, outC);
                }
            }
            target.Apply();
        }

        // Sichtbare Texture des Secondary Layers ermitteln
        Texture2D GetSecondaryTexture()
        {
            // Variante A: wenn dein Layer eine Property hat:
            // return secondaryLayer.Texture;

            // Variante B: über den SpriteRenderer
            var sr = secondaryLayer != null ? secondaryLayer.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr.sprite?.texture : null;
        }

        // 2a) Rotation (einfach/nearest)
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

        // 2b) Alpha-Blending (source-over)
        static Color BlendSrcOver(Color dst, Color src)
        {
            // src über dst
            float outA = src.a + dst.a * (1f - src.a);

            float outR = src.r * src.a + dst.r * dst.a * (1f - src.a);
            float outG = src.g * src.a + dst.g * dst.a * (1f - src.a);
            float outB = src.b * src.a + dst.b * dst.a * (1f - src.a);

            if (outA > 1e-6f) { outR /= outA; outG /= outA; outB /= outA; }
            else { outR = outG = outB = 0f; }

            return new Color(outR, outG, outB, outA);
        }
        // 2c) Sichtbare Textur blenden – über Chunks (multi-chunk sicher!)
        void BlitBrushToVisibleLayer(Texture2D src, int baseX, int baseY)
        {
            // Wir iterieren nur über nicht-transparente Brush-Pixel und schreiben in den passenden Chunk.
            var affected = new HashSet<PaintableChunk>();
            for (int y = 0; y < src.height; y++)
            {
                for (int x = 0; x < src.width; x++)
                {
                    Color sc = src.GetPixel(x, y);
                    if (sc.a <= brushAlphaThreshold) continue;

                    int gx = baseX + x;
                    int gy = baseY + y;
                    if (gx < 0 || gy < 0) continue; // grobe Grenze (obere Grenze prüfen wir pro Chunk)

                    // passenden Chunk finden (arbeitet in Pixelkoordinaten)
                    var chunk = secondaryLayer.GetChunkByPosition(new Vector2Int(gx, gy));
                    if (chunk == null || chunk.TextureSource?.Texture == null) continue;

                    // Pixelposition im Chunk: Welt -> Chunk-Lokal -> Pixel
                    // Layer-World-Pos + (Pixel/PPU) = World-Pos; invertieren:
                    // schnell und stabil: nutze Chunk-Transform
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

        // „Source over“-Alpha-Blending
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

                    // src-over
                    float outA = sc.a + dc.a * (1f - sc.a);
                    Color outRGB = (sc * sc.a) + (dc * dc.a * (1f - sc.a));
                    if (outA > 0f) outRGB /= outA;

                    target.SetPixel(tx, ty, new Color(outRGB.r, outRGB.g, outRGB.b, outA));
                }
            }
            target.Apply();
        }

        // Sichtbare Texture der Secondary-Schicht holen
        Texture2D GetSecondaryTextureRW()
        {
            // Variante A: dein VisibleLayer hat eine Property -> direkt zurückgeben (falls vorhanden)
            // return secondaryLayer.Texture;

            // Variante B: über SpriteRenderer
            var sr = secondaryLayer ? secondaryLayer.GetComponent<SpriteRenderer>() : null;
            var tex = sr ? sr.sprite?.texture : null;
            return tex;
        }


    }
}
