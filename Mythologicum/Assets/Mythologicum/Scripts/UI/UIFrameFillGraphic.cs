using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>One uniformly tinted backing, fitted to the enclosed opening in an illustrated frame.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Exhibition frame fill")]
    public sealed class UIFrameFillGraphic : Image
    {
        // Keep the frame sprite separate from Image.sprite: this graphic renders white, tinted quads.
        private Sprite source;
        private FillShape shape;
        private CacheKey appliedKey;
        private bool configured, appliedMatch;
        private static readonly Dictionary<CacheKey, FillShape> Shapes = new Dictionary<CacheKey, FillShape>();

        public Sprite Source => source;
        public bool HasMatchedInterior => shape != null;

        /// <summary>Reads each frame once. Insets remain available to the caller when matching is disabled or fails.</summary>
        public void Configure(Sprite artwork, bool matchArtwork = true, int overlapPixels = 2)
        {
            var key = new CacheKey(artwork, Mathf.Clamp(overlapPixels, 0, 8));
            if (configured && appliedMatch == matchArtwork && appliedKey.Equals(key)) return;
            configured = true;
            appliedMatch = matchArtwork;
            appliedKey = key;
            source = artwork;
            sprite = null;
            shape = null;
            if (matchArtwork && artwork != null)
            {
                if (!Shapes.TryGetValue(key, out shape))
                {
                    shape = BuildShape(artwork, key.overlap);
                    Shapes.Add(key, shape);
                }
            }
            SetVerticesDirty();
        }

        /// <summary>Tests a point in the backing's normalized rectangle, with its origin at the bottom left.</summary>
        public bool Covers(Vector2 normalized)
        {
            if (normalized.x < 0 || normalized.y < 0 || normalized.x >= 1 || normalized.y >= 1) return false;
            if (shape == null) return true;
            int x = Mathf.FloorToInt(normalized.x * shape.width);
            int y = Mathf.FloorToInt(normalized.y * shape.height);
            return shape.coverage[y * shape.width + x];
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            if (shape == null) { base.OnPopulateMesh(vertices); return; }
            vertices.Clear();
            Rect target = GetPixelAdjustedRect();
            Color32 tint = color;
            foreach (RectInt span in shape.spans)
            {
                float left = target.xMin + span.xMin * target.width / shape.width;
                float right = target.xMin + span.xMax * target.width / shape.width;
                float bottom = target.yMin + span.yMin * target.height / shape.height;
                float top = target.yMin + span.yMax * target.height / shape.height;
                int start = vertices.currentVertCount;
                vertices.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
                vertices.AddVert(new Vector3(left, top), tint, Vector2.zero);
                vertices.AddVert(new Vector3(right, top), tint, Vector2.zero);
                vertices.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start + 2, start + 3, start);
            }
        }

        private static FillShape BuildShape(Sprite artwork, int overlap)
        {
            Color32[] pixels;
            int width, height;
            try
            {
                if (!ReadPixels(artwork, out pixels, out width, out height)) return null;
            }
            catch (Exception)
            {
                // A missing texture or unsupported sprite packing must not prevent opening a menu.
                return null;
            }

            // Transparent regions connected to the atlas crop's edges are outside the frame.
            // Pick the largest enclosed opening instead of its many tiny decorative cutouts.
            const byte transparentAlpha = 8;
            int count = width * height;
            var visited = new bool[count];
            var queue = new int[count];
            int[] largest = null;
            for (int seed = 0; seed < count; seed++)
            {
                if (visited[seed] || pixels[seed].a > transparentAlpha) continue;
                int head = 0, tail = 1;
                queue[0] = seed; visited[seed] = true;
                bool touchesEdge = false;
                while (head < tail)
                {
                    int current = queue[head++], x = current % width, y = current / width;
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1) touchesEdge = true;
                    if (x > 0) Visit(current - 1);
                    if (x < width - 1) Visit(current + 1);
                    if (y > 0) Visit(current - width);
                    if (y < height - 1) Visit(current + width);
                }
                if (!touchesEdge && (largest == null || tail > largest.Length))
                {
                    largest = new int[tail];
                    Array.Copy(queue, largest, tail);
                }

                void Visit(int point)
                {
                    if (visited[point] || pixels[point].a > transparentAlpha) return;
                    visited[point] = true;
                    queue[tail++] = point;
                }
            }
            if (largest == null || largest.Length < count / 100) return null;

            var fill = new bool[count];
            foreach (int index in largest) fill[index] = true;
            // Extend underneath the artwork to hide antialiased seams, never into exterior clear pixels.
            for (int pass = 0; pass < overlap; pass++)
            {
                var expanded = (bool[])fill.Clone();
                for (int index = 0; index < count; index++)
                {
                    if (!fill[index]) continue;
                    int x = index % width, y = index / width;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int next = ny * width + nx;
                        if (pixels[next].a > transparentAlpha) expanded[next] = true;
                    }
                }
                fill = expanded;
            }

            // Identical spans on consecutive rows merge into rectangles. No quads overlap,
            // so partially transparent lining has the same opacity everywhere.
            var spans = new List<RectInt>();
            var previous = new Dictionary<long, int>();
            for (int y = 0; y < height; y++)
            {
                var current = new Dictionary<long, int>();
                int x = 0;
                while (x < width)
                {
                    if (!fill[y * width + x]) { x++; continue; }
                    int left = x++;
                    while (x < width && fill[y * width + x]) x++;
                    long key = ((long)left << 32) | (uint)x;
                    int index;
                    if (previous.TryGetValue(key, out index))
                    {
                        RectInt span = spans[index]; span.height++; spans[index] = span;
                    }
                    else { index = spans.Count; spans.Add(new RectInt(left, y, x - left, 1)); }
                    current.Add(key, index);
                }
                previous = current;
            }
            // UGUI uses 16-bit mesh indices. Unusually noisy replacement art gets the safe fallback.
            if (spans.Count > 16000) return null;
            return new FillShape(width, height, fill, spans.ToArray());
        }

        private static bool ReadPixels(Sprite artwork, out Color32[] pixels, out int width, out int height)
        {
            pixels = null; width = height = 0;
            Texture2D texture = artwork.texture;
            if (texture == null) return false;
            // The project uses rectangular sprite crops; rotated/tightly packed atlas sprites fall back.
            if (artwork.packed && artwork.packingRotation != SpritePackingRotation.None) return false;
            Rect crop = artwork.textureRect;
            width = Mathf.RoundToInt(crop.width); height = Mathf.RoundToInt(crop.height);
            if (width < 2 || height < 2) return false;
            if (texture.isReadable)
            {
                Color[] sourcePixels = texture.GetPixels(Mathf.RoundToInt(crop.x), Mathf.RoundToInt(crop.y), width, height);
                pixels = new Color32[sourcePixels.Length];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = sourcePixels[i];
                return true;
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D readable = null;
            try
            {
                temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, temporary,
                    new Vector2(crop.width / texture.width, crop.height / texture.height),
                    new Vector2(crop.x / texture.width, crop.y / texture.height));
                RenderTexture.active = temporary;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readable.Apply(false, false);
                pixels = readable.GetPixels32();
                return true;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (readable != null)
                {
                    if (Application.isPlaying) Destroy(readable);
                    else DestroyImmediate(readable);
                }
            }
        }

        private sealed class FillShape
        {
            public readonly int width, height;
            public readonly bool[] coverage;
            public readonly RectInt[] spans;
            public FillShape(int width, int height, bool[] coverage, RectInt[] spans)
            { this.width = width; this.height = height; this.coverage = coverage; this.spans = spans; }
        }

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly int spriteId, textureId;
            private readonly uint textureVersion;
            public readonly int overlap;
            public CacheKey(Sprite artwork, int overlap)
            {
                spriteId = artwork != null ? artwork.GetInstanceID() : 0;
                Texture2D texture = artwork != null ? artwork.texture : null;
                textureId = texture != null ? texture.GetInstanceID() : 0;
                textureVersion = texture != null ? texture.updateCount : 0;
                this.overlap = overlap;
            }
            public bool Equals(CacheKey other) => spriteId == other.spriteId && textureId == other.textureId
                && textureVersion == other.textureVersion && overlap == other.overlap;
            public override bool Equals(object other) => other is CacheKey key && Equals(key);
            public override int GetHashCode() => unchecked(((spriteId * 397 ^ textureId) * 397 ^ (int)textureVersion) * 397 ^ overlap);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => Shapes.Clear();
    }
}
