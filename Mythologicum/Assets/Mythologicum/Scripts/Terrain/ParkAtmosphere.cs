using BKPureNature;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mythologicum
{
    /// <summary>Connects the park's editable mesh terrain to the retained fog renderer.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PureVolumetricFog))]
    public sealed class ParkAtmosphere : MonoBehaviour
    {
        private SquareBuildGrid grid;
        private Texture2D heightTexture;
        private PureVolumetricFog fog;
        private int uploadedRevision = -1;
        private bool previousFog, replacedFog, suspendedFog;

        public Texture2D HeightTexture => heightTexture;
        public PureVolumetricFog Fog => fog;
        public Bounds HeightBounds { get; private set; }
        public int UploadedRevision => uploadedRevision;

        public void Initialize(SquareBuildGrid source)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (grid == null) previousFog = RenderSettings.fog;
            if (grid != source) uploadedRevision = -1;
            grid = source;
            fog = GetComponent<PureVolumetricFog>();
            SyncTerrain();
        }

        private void LateUpdate() => SyncTerrain();

        private void OnEnable()
        {
            if (suspendedFog && fog != null) fog.enabled = true;
            suspendedFog = false;
            SyncTerrain();
        }

        public void SyncTerrain()
        {
            if (grid == null || fog == null) return;
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RFloat)) return;
            int revision = grid.Terrain?.Revision ?? 0;
            if (heightTexture == null || uploadedRevision != revision)
            {
                int width = grid.Width * GridHeightField.Subdivisions;
                int depth = grid.Depth * GridHeightField.Subdivisions;
                if (heightTexture == null || heightTexture.width != width || heightTexture.height != depth)
                {
                    if (heightTexture != null) Destroy(heightTexture);
                    heightTexture = new Texture2D(width, depth, TextureFormat.RFloat, false, true)
                    {
                        name = "Park fog heightmap",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        hideFlags = HideFlags.DontSave
                    };
                }
                float minimum = 0f, maximum = 0f;
                if (grid.Terrain != null)
                {
                    minimum = float.MaxValue; maximum = float.MinValue;
                    for (int z = 0; z <= grid.Depth; z++)
                    for (int x = 0; x <= grid.Width; x++)
                    {
                        float height = grid.Terrain.Node(x, z);
                        minimum = Mathf.Min(minimum, height);
                        maximum = Mathf.Max(maximum, height);
                    }
                }
                float range = Mathf.Max(1f, maximum - minimum);
                HeightBounds = new Bounds(
                    grid.Origin + new Vector3(grid.Width * grid.CellSize * .5f, minimum + range * .5f, grid.Depth * grid.CellSize * .5f),
                    new Vector3(grid.Width * grid.CellSize, range, grid.Depth * grid.CellSize));
                var heights = new float[width * depth];
                for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    float height = grid.Terrain?.Sample((x + .5f) * grid.Width / width, (z + .5f) * grid.Depth / depth) ?? 0f;
                    heights[z * width + x] = (height - minimum) / range;
                }
                heightTexture.SetPixelData(heights, 0);
                heightTexture.Apply(false, false);
                fog.SetHeightSource(heightTexture, HeightBounds, grid.Origin.y + maximum);
                uploadedRevision = revision;
            }

            // Keep the existing haze on hardware without the required rendering support.
            bool ready = fog.isActiveAndEnabled && fog.IsReady;
            if (ready && !replacedFog)
            {
                RenderSettings.fog = false;
                replacedFog = true;
            }
            else if (!ready && replacedFog)
            {
                RenderSettings.fog = previousFog;
                replacedFog = false;
            }
        }

        private void OnDisable()
        {
            if (gameObject.activeInHierarchy && fog != null && fog.enabled)
            {
                fog.enabled = false;
                suspendedFog = true;
            }
            if (replacedFog && gameObject.scene == SceneManager.GetActiveScene()) RenderSettings.fog = previousFog;
            replacedFog = false;
        }

        private void OnDestroy()
        {
            if (fog != null) fog.ClearHeightSource();
            if (heightTexture != null) Destroy(heightTexture);
        }
    }
}
