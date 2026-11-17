using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Shared-material attraction prototypes and pause-aware ride motion; disposable native fixture only.</summary>
public static class AttractionVisualNativeChecks
{
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Attraction visuals: " + reason); checks++; }
        var owner = new GameObject("Attraction prototype fixture");
        owner.transform.position = new Vector3(50000, 0, 50000);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var signature = new Dictionary<AttractionKind, string> {
            { AttractionKind.PettingZoo, "Entrance arch" }, { AttractionKind.ViewingTower, "Tower stair" },
            { AttractionKind.BalloonRide, "Balloon envelope" }, { AttractionKind.PredatorFeedingGallery, "Gallery canopy" },
            { AttractionKind.TricksterTheatre, "Theatre curtain" }, { AttractionKind.CabinetOfCuriosities, "Mythical egg" },
            { AttractionKind.ExpeditionPavilion, "Exhibition globe" }, { AttractionKind.ClockworkCarousel, "Clockwork mythical mount" },
            { AttractionKind.ViewingWindow, "Viewing bay glass" }
        };
        try
        {
            foreach (var definition in BuildingDefinition.Defaults().Where(item => item.IsAttraction))
            {
                Vector3 size = new Vector3(definition.width * 2 - .16f, definition.height, definition.depth * 2 - .16f);
                var body = AttractionVisuals.Create(owner.transform, definition, size, material);
                body.transform.localPosition = new Vector3((int)definition.kind * 20, 0, 0);
                var names = body.GetComponentsInChildren<Transform>(true).Select(item => item.name).ToArray();
                Check(names.Contains(signature[definition.attraction.kind]), "the attraction has its distinctive prototype geometry: " + definition.kind);
                var renderers = body.GetComponentsInChildren<MeshRenderer>(true);
                if (definition.kind == BuildingKind.ViewingWindow)
                {
                    var panes = renderers.Where(renderer => renderer.gameObject.name.Contains("glass")).ToArray();
                    Check(panes.Length == 3 && panes.All(renderer => renderer.sharedMaterial == panes[0].sharedMaterial)
                        && panes[0].sharedMaterial.renderQueue == 3000, "the viewing bay reuses one transparent glass material across its three panes");
                    Check(renderers.Except(panes).All(renderer => renderer.sharedMaterial == material),
                        "the viewing bay's solid structure reuses its supplied material");
                }
                else Check(renderers.Length >= 5 && renderers.All(renderer => renderer.sharedMaterial == material),
                    "visual parts reuse their supplied material without cloning assets");
                Check(body.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), "prototype visuals do not add physical obstacles");
                Check(body.GetComponentsInChildren<TextMesh>(true).Length == 0, "attraction models do not restore floating labels");
                var animation = body.GetComponent<AttractionVisualAnimator>();
                Check(animation != null, "each prototype has its own initialized visual controller");
                if (definition.attraction.kind == AttractionKind.BalloonRide || definition.attraction.kind == AttractionKind.ViewingTower
                    || definition.attraction.kind == AttractionKind.ClockworkCarousel)
                    Check(animation.RideAnchor != null, "elevated and rotating attractions expose their actual ride anchor");
                if (definition.attraction.kind == AttractionKind.BalloonRide)
                {
                    var rig = body.transform.Find("Moving rig"); var dock = rig.localPosition;
                    animation.Bind(() => 0); Advance(animation, 1);
                    Check(rig.localPosition == dock, "unoccupied balloons remain docked");
                    animation.Bind(() => 2); Advance(animation, definition.serviceSeconds * .5f);
                    Check(rig.localPosition.y > dock.y && Vector3.Distance(rig.localPosition, dock) > definition.attraction.rideHeight * .5f,
                        "occupied balloons rise and follow their configured route");
                    var held = rig.localPosition; Advance(animation, 0);
                    Check(rig.localPosition == held, "pausing simulation freezes balloon motion");
                    animation.Bind(() => 0); Advance(animation, definition.serviceSeconds * 2);
                    Check(Vector3.Distance(rig.localPosition, dock) < .001f, "empty balloons return to their station");
                }
                if (definition.attraction.kind == AttractionKind.ClockworkCarousel)
                {
                    var rig = body.transform.Find("Moving rig"); var idle = rig.localRotation;
                    animation.Bind(() => 0); Advance(animation, 1);
                    Check(Quaternion.Angle(idle, rig.localRotation) < .001f, "unoccupied carousels remain still");
                    animation.Bind(() => 1); Advance(animation, definition.serviceSeconds * .25f);
                    Check(Quaternion.Angle(idle, rig.localRotation) > 45, "occupied carousels rotate their mythical mounts");
                    var held = rig.localRotation; Advance(animation, 0); Advance(animation, float.NaN);
                    Check(Quaternion.Angle(held, rig.localRotation) < .001f, "paused or invalid time never advances ride motion");
                }
            }
            checks += PortalPlacementChecks(owner.transform, material);
        }
        finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(material); }
        return checks;
    }
    private static void Advance(AttractionVisualAnimator animation, float seconds)
        => typeof(AttractionVisualAnimator).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animation, new object[] { seconds });

    private static int PortalPlacementChecks(Transform owner, Material material)
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Petting placement: " + reason); checks++; }
        var grid = new SquareBuildGrid(new Vector3(70000, 0, 70000), 2, 24, 24);
        var layout = new FenceLayout(grid);
        var construction = new BuildingConstruction(grid, layout, owner, material, material);
        Check(construction.Network.TryPlace(new BuildingFootprint(new BuildingDefinition
            { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, Vector2Int.one, 0)) != null, "the portal fixture has an entrance");
        var definition = AttractionDefinition.Building(BuildingKind.PettingZoo);
        var footprint = new BuildingFootprint(definition, new Vector2Int(10, 8), 0);
        var center = footprint.Center(grid) + Vector3.up;
        var fence = new GameObject("Rear boundary fence"); fence.transform.SetParent(owner, false);
        fence.transform.position = layout.Center(footprint.RearEdge) + Vector3.up;
        fence.AddComponent<BoxCollider>().size = new Vector3(2, 2, .3f);
        fence.AddComponent<FenceSegment>().Initialize(footprint.RearEdge); Physics.SyncTransforms();
        Check(construction.CanPlace(footprint), "the portal permits its exact rear fence where the model overhangs the footprint");
        var obstacle = new GameObject("Unrelated placement obstacle"); obstacle.transform.SetParent(owner, false);
        obstacle.transform.position = center; obstacle.AddComponent<BoxCollider>().size = Vector3.one * .3f; Physics.SyncTransforms();
        Check(!construction.CanPlace(footprint), "unrelated physical obstacles still block the portal");
        Object.DestroyImmediate(obstacle); Physics.SyncTransforms();
        var foreignFence = new GameObject("Other fence edge"); foreignFence.transform.SetParent(owner, false);
        foreignFence.transform.position = center; foreignFence.AddComponent<BoxCollider>().size = Vector3.one * .3f;
        foreignFence.AddComponent<FenceSegment>().Initialize(new FenceEdge(footprint.RearEdge.Start + Vector2Int.right, footprint.RearEdge.AlongX));
        Physics.SyncTransforms(); Check(!construction.CanPlace(footprint), "fences belonging to another edge are never ignored");
        Object.DestroyImmediate(foreignFence); Physics.SyncTransforms();
        var ordinary = new BuildingDefinition { kind = BuildingKind.Kiosk, width = 1, depth = 1, height = 2 };
        Check(!construction.CanPlace(new BuildingFootprint(ordinary, footprint.Anchor, 0)), "ordinary buildings keep their original physical collision checks");
        var overflow = new GameObject("Overlap saturation"); overflow.transform.SetParent(owner, false);
        for (int index = 0; index < 65; index++)
        {
            var part = new GameObject("Rear fence detail"); part.transform.SetParent(overflow.transform, false);
            part.transform.position = center; part.AddComponent<BoxCollider>().size = Vector3.one * .1f;
            part.AddComponent<FenceSegment>().Initialize(footprint.RearEdge);
        }
        Physics.SyncTransforms(); Check(!construction.CanPlace(footprint), "a saturated nonalloc overlap buffer rejects placement safely");
        Object.DestroyImmediate(overflow); Object.DestroyImmediate(fence);
        return checks;
    }
}
