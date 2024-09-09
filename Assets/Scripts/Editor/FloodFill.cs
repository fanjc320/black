#if IMAGESHARP
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UnityEngine;

namespace black_dev_tools
{
    
    internal class FloodFill
    {
        static readonly Rgba32 Black = Rgba32.ParseHex("000000ff");
        static readonly Rgba32 White = Rgba32.ParseHex("ffffffff");

        static bool ColorMatch(Rgba32 a, Rgba32 b)
        {
            return a == b;
        }

        static bool ColorIsNotBlack(Rgba32 a)
        {
            return a != Black;
        }

        static bool ColorIsNotAndNotBlack(Rgba32 a, Rgba32 thisColor)
        {
            return ColorIsNotBlack(a) && a != thisColor;
        }

        static Rgba32 GetPixel(Image<Rgba32> bitmap, int x, int y)
        {
            return bitmap[x, y];
        }

        static Rgba32 SetPixel(Image<Rgba32> bitmap, int x, int y, Rgba32 c)
        {
            var originalColor = bitmap[x, y];
            bitmap[x, y] = c;
            return originalColor;
        }

        public static Vector2Int ExecuteFill(Image<Rgba32> bitmap, Vector2Int pt, Rgba32 targetColor,
            Rgba32 replacementColor, out int pixelArea, out List<Vector2Int> points)
        {
            points = new List<Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(pt);
            var fillMinPoint = new Vector2Int(bitmap.Width, bitmap.Height);
            pixelArea = 0;
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                // targetColor否则，跳过它。
                if (ColorMatch(GetPixel(bitmap, n.x, n.y), targetColor) == false) continue;
                Vector2Int w = n, e = new Vector2Int(n.x + 1, n.y);
                while (w.x >= 0 && ColorMatch(GetPixel(bitmap, w.x, w.y), targetColor))
                {
                    SetPixel(bitmap, w.x, w.y, replacementColor);
                    UpdateFillMinPoint(ref fillMinPoint, w);
                    points.Add(w);
                    pixelArea++;
                    if (w.y > 0 && ColorMatch(GetPixel(bitmap, w.x, w.y - 1), targetColor))
                        q.Enqueue(new Vector2Int(w.x, w.y - 1));
                    if (w.y < bitmap.Height - 1 && ColorMatch(GetPixel(bitmap, w.x, w.y + 1), targetColor))
                        q.Enqueue(new Vector2Int(w.x, w.y + 1));
                    w.x--;
                }

                while (e.x <= bitmap.Width - 1 && ColorMatch(GetPixel(bitmap, e.x, e.y), targetColor))
                {
                    SetPixel(bitmap, e.x, e.y, replacementColor);
                    points.Add(e);
                    UpdateFillMinPoint(ref fillMinPoint, e);
                    pixelArea++;
                    if (e.y > 0 && ColorMatch(GetPixel(bitmap, e.x, e.y - 1), targetColor))
                        q.Enqueue(new Vector2Int(e.x, e.y - 1));
                    if (e.y < bitmap.Height - 1 && ColorMatch(GetPixel(bitmap, e.x, e.y + 1), targetColor))
                        q.Enqueue(new Vector2Int(e.x, e.y + 1));
                    e.x++;
                }
            }

            return fillMinPoint;
        }

        public static Vector2Int ExecuteFillIfNotBlack(Image<Rgba32> bitmap, Vector2Int pt, Rgba32 replacementColor,
            out int pixelArea, out List<Vector2Int> points, out Dictionary<Rgba32, int> originalColors)
        {
            points = new List<Vector2Int>();
            originalColors = new Dictionary<Rgba32, int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(pt);
            var fillMinPoint = new Vector2Int(bitmap.Width, bitmap.Height);
            pixelArea = 0;
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                // 跳过，除非它是黑色的（即如果它是黑色的）.
                if (ColorIsNotBlack(GetPixel(bitmap, n.x, n.y)) == false) continue;
                Vector2Int w = n, e = new Vector2Int(n.x + 1, n.y);
                while (w.x >= 0 && ColorIsNotBlack(GetPixel(bitmap, w.x, w.y)))
                {
                    var oldColor = SetPixel(bitmap, w.x, w.y, replacementColor);
                    Program.IncreaseCountOfDictionaryValue(originalColors, oldColor);
                    UpdateFillMinPoint(ref fillMinPoint, w);
                    Logger.WriteLine($"ExecuteFillIfNotBlack oldColor:{oldColor} originalColors:{originalColors} fillMinPoint:{fillMinPoint} w:{w} pt:{pt}");
                    points.Add(w);
                    pixelArea++;
                    if (w.y > 0 && ColorIsNotBlack(GetPixel(bitmap, w.x, w.y - 1)))
                        q.Enqueue(new Vector2Int(w.x, w.y - 1));
                    if (w.y < bitmap.Height - 1 && ColorIsNotBlack(GetPixel(bitmap, w.x, w.y + 1)))
                        q.Enqueue(new Vector2Int(w.x, w.y + 1));
                    w.x--;
                }

                while (e.x <= bitmap.Width - 1 && ColorIsNotBlack(GetPixel(bitmap, e.x, e.y)))
                {
                    var oldColor = SetPixel(bitmap, e.x, e.y, replacementColor);
                    Program.IncreaseCountOfDictionaryValue(originalColors, oldColor);
                    UpdateFillMinPoint(ref fillMinPoint, e);
                    points.Add(e);
                    pixelArea++;
                    if (e.y > 0 && ColorIsNotBlack(GetPixel(bitmap, e.x, e.y - 1)))
                        q.Enqueue(new Vector2Int(e.x, e.y - 1));
                    if (e.y < bitmap.Height - 1 && ColorIsNotBlack(GetPixel(bitmap, e.x, e.y + 1)))
                        q.Enqueue(new Vector2Int(e.x, e.y + 1));
                    e.x++;
                }

                //using (var stream = new FileStream(@"C:\black\dev-tools\bin\Debug\Assets\Stages\test.png", FileMode.Create)) {
                //    bitmap.SaveAsPng(stream);
                //    stream.Close();
                //}
            }

            return fillMinPoint;
        }

        public static Vector2Int ExecuteFillIf(Image<Rgba32> bitmap, Vector2Int pt, Rgba32 beforeColor,
            Rgba32 replacementColor, out int pixelArea, out List<Vector2Int> points,
            out Dictionary<Rgba32, int> originalColors, int islandIndex, System.Action<int, int, int> setPixelCallback)
        {
            points = new List<Vector2Int>();
            originalColors = new Dictionary<Rgba32, int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(pt);
            var fillMinPoint = new Vector2Int(bitmap.Width, bitmap.Height);
            pixelArea = 0;
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                var nc = GetPixel(bitmap, n.x, n.y);
                if (ColorIsNotAndNotBlack(nc, replacementColor) == false)
                {
                    continue;
                }

                {
                    var w = n;
                    while (w.x >= 0 && ColorIsNotAndNotBlack(GetPixel(bitmap, w.x, w.y), replacementColor))
                    {
                        var oldColor = SetPixel(bitmap, w.x, w.y, replacementColor);
                        setPixelCallback?.Invoke(islandIndex, w.x, w.y);
                        Program.IncreaseCountOfDictionaryValue(originalColors, oldColor);
                        UpdateFillMinPoint(ref fillMinPoint, w);
                        points.Add(w);
                        pixelArea++;
                        if (w.y > 0 && ColorIsNotAndNotBlack(GetPixel(bitmap, w.x, w.y - 1), replacementColor))
                            q.Enqueue(new Vector2Int(w.x, w.y - 1));
                        if (w.y < bitmap.Height - 1 &&
                            ColorIsNotAndNotBlack(GetPixel(bitmap, w.x, w.y + 1), replacementColor))
                            q.Enqueue(new Vector2Int(w.x, w.y + 1));
                        w.x--;
                    }
                }

                {
                    var e = new Vector2Int(n.x + 1, n.y);
                    while (e.x <= bitmap.Width - 1 &&
                           ColorIsNotAndNotBlack(GetPixel(bitmap, e.x, e.y), replacementColor))
                    {
                        var oldColor = SetPixel(bitmap, e.x, e.y, replacementColor);
                        setPixelCallback?.Invoke(islandIndex, e.x, e.y);
                        Program.IncreaseCountOfDictionaryValue(originalColors, oldColor);
                        UpdateFillMinPoint(ref fillMinPoint, e);
                        points.Add(e);
                        pixelArea++;
                        if (e.y > 0 && ColorIsNotAndNotBlack(GetPixel(bitmap, e.x, e.y - 1), replacementColor))
                            q.Enqueue(new Vector2Int(e.x, e.y - 1));
                        if (e.y < bitmap.Height - 1 &&
                            ColorIsNotAndNotBlack(GetPixel(bitmap, e.x, e.y + 1), replacementColor))
                            q.Enqueue(new Vector2Int(e.x, e.y + 1));
                        e.x++;
                    }
                }
            }

            //using (var stream = new FileStream(@"C:\black\dev-tools\bin\Debug\Assets\Stages\test.png", FileMode.Create)) {
            //    bitmap.SaveAsPng(stream);
            //    stream.Close();
            //}

            return fillMinPoint;
        }

        static void UpdateFillMinPoint(ref Vector2Int fillMinPoint, Vector2Int w)
        {
            if (fillMinPoint.x > w.x || fillMinPoint.x == w.x && fillMinPoint.y > w.y) fillMinPoint = w;
        }
    }
}
#endif


//算法介绍 | 泛洪算法（Flood fill Algorithm）
//https://blog.csdn.net/Eason_Y/article/details/127782837
//这里的floodfill算法感觉能根据这个链接简化