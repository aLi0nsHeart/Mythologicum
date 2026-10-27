using UnityEngine;
using UnityEngine.Rendering;

namespace Mythologicum
{
    internal static class VegetationVisuals
    {
        public static GameObject Create(GameObject prefab, Transform parent, float height, float yaw, bool tree)
        {
            var visual = Object.Instantiate(prefab, parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float scale = height / Mathf.Max(0.01f, bounds.size.y);
                visual.transform.localScale *= scale;
                // Place the mesh's lowest point on the ground regardless of its original pivot.
                visual.transform.position += Vector3.up * (parent.position.y - bounds.min.y) * scale;
            }
            foreach (LODGroup group in visual.GetComponentsInChildren<LODGroup>())
            {
                LOD[] levels = group.GetLODs();
                if (!tree)
                {
                    // The supplied plants have empty distant LODs, which hide them at our camera height.
                    group.enabled = false;
                    for (int i = 0; i < levels.Length; i++)
                        foreach (Renderer renderer in levels[i].renderers)
                            if (renderer != null) renderer.enabled = i == 0;
                }
                else
                {
                    for (int i = 0; i < levels.Length; i++)
                        levels[i].screenRelativeTransitionHeight = i == levels.Length - 1 ? 0.001f : 0.18f / (i + 1);
                    group.SetLODs(levels);
                    group.RecalculateBounds();
                }
            }
            foreach (Renderer renderer in renderers)
            {
                if (!tree) renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            return visual;
        }
    }
}