using UnityEngine;
using DTerrain;
using System.Collections.Generic;

public static class BrushShapeUtils
{
    public static Shape ShapeFromTextureOld(Texture2D tex, float alphaThreshold = 0.1f)
    {
        Shape s = new Shape(tex.width, tex.height);

        for (int y = 0; y < tex.height; y++)
        {
            int startX = -1;
            bool foundRange = false;

            for (int x = 0; x < tex.width; x++)
            {
                float a = tex.GetPixel(x, y).a;

                if (a > alphaThreshold)
                {
                    if (startX == -1)
                        startX = x;
                }
                else
                {
                    if (startX != -1)
                    {
                        s.Ranges.Add(new Range(startX, x));
                        foundRange = true;
                        startX = -1;
                    }
                }
            }

            // Wenn Reihe am Ende noch Range offen hat
            if (startX != -1)
            {
                s.Ranges.Add(new Range(startX, tex.width));
                foundRange = true;
            }

            // Falls diese Y-Reihe keine Range hat, trotzdem leeren Eintrag (wichtig für Y-Positionen)
            if (!foundRange)
            {
                //s.Ranges.Add(null);
            }
        }

        return s;
    }

    public static Shape ShapeFromTexture(Texture2D tex, float alphaThreshold = 0.1f)
    {
        Shape s = new Shape(tex.width, tex.height);

        for (int x = 0; x < tex.width; x++)
        {
            bool inside = false;
            int minY = 0;
            int maxY = 0;

            for (int y = 0; y < tex.height; y++)
            {
                float a = tex.GetPixel(x, y).a;

                if (!inside && a > alphaThreshold)
                {
                    inside = true;
                    minY = y;
                }
                else if (inside && a <= alphaThreshold)
                {
                    maxY = y;
                    break; // genau eine Range pro Spalte
                }
            }

            if (inside)
            {
                // falls bis zum Ende „inside“, nimm Höhe als max
                if (maxY == 0) maxY = tex.height;
                s.Ranges.Add(new Range(minY, maxY));
            }
            else
            {
                // keine Treffer in dieser Spalte → kein Eintrag (oder optional Dummy)
                //s.Ranges.Add(null); // many DTerrain-Pfade erwarten positionsgetreue Liste
            }
        }

        return s;
    }
}
