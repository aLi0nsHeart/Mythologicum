using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Recognizable temporary attraction models using only shared materials and collider-free primitives.</summary>
    public static class AttractionVisuals
    {
        private static Material viewingGlassMaterial;

        private static Material ViewingGlassMaterial()
        {
            if (viewingGlassMaterial != null) return viewingGlassMaterial;
            viewingGlassMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"))
                { name = "Viewing bay glass", renderQueue = 3000 };
            viewingGlassMaterial.SetOverrideTag("RenderType", "Transparent");
            viewingGlassMaterial.SetFloat("_Surface", 1); viewingGlassMaterial.SetFloat("_ZWrite", 0);
            viewingGlassMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            viewingGlassMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            viewingGlassMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            viewingGlassMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            viewingGlassMaterial.EnableKeyword("_ALPHABLEND_ON");
            return viewingGlassMaterial;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ReleaseSharedMaterials()
        {
            if (viewingGlassMaterial != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(viewingGlassMaterial);
                else UnityEngine.Object.DestroyImmediate(viewingGlassMaterial);
            }
            viewingGlassMaterial = null;
        }

        public static GameObject Create(Transform parent, BuildingDefinition definition, Vector3 size, Material material)
        {
            if (definition == null || !definition.IsAttraction) throw new ArgumentException("An attraction definition is required.");
            var root = new GameObject("Attraction visual"); root.transform.SetParent(parent, false);
            var builder = new Builder(root.transform, definition, size, material);
            builder.Build();
            var animation = root.AddComponent<AttractionVisualAnimator>();
            animation.Initialize(definition); builder.ConfigurePassengers(animation);
            return root;
        }

        private sealed class Builder
        {
            private readonly Transform parent;
            private readonly BuildingDefinition definition;
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private readonly float w, d, h;
            private Vector3 passengerCentre;
            private Vector2 passengerArea;
            private float passengerYaw, passengerFloor;
            private bool passengerCircular, passengerOutward, passengerCarousel;
            private readonly Color body, brass = new Color(.77f, .57f, .24f), wood = new Color(.31f, .18f, .1f),
                dark = new Color(.1f, .13f, .14f), cream = new Color(.94f, .85f, .62f);
            public Builder(Transform parent, BuildingDefinition definition, Vector3 size, Material material)
            {
                this.parent = parent; this.definition = definition; this.material = material;
                w = size.x; d = size.z; h = size.y; body = definition.openColor;
            }
            private Transform Group(string name, Transform owner = null, Vector3 position = default)
            {
                var group = new GameObject(name); group.transform.SetParent(owner != null ? owner : parent, false);
                group.transform.localPosition = position; return group.transform;
            }
            private Transform Part(string name, PrimitiveType primitive, Vector3 point, Vector3 size, Color color,
                Transform owner = null, Vector3 rotation = default)
            {
                var part = GameObject.CreatePrimitive(primitive); part.name = name;
                part.GetComponent<Collider>().enabled = false;
                part.transform.SetParent(owner != null ? owner : parent, false);
                part.transform.localPosition = point; part.transform.localScale = size; part.transform.localRotation = Quaternion.Euler(rotation);
                var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color); renderer.SetPropertyBlock(properties);
                return part.transform;
            }
            private Transform Box(string name, Vector3 point, Vector3 size, Color color, Transform owner = null)
                => Part(name, PrimitiveType.Cube, point, size, color, owner);
            private void Post(Vector3 point, float height, Transform owner = null)
                => Box("Support", point + Vector3.up * height * .5f, new Vector3(.16f, height, .16f), wood, owner);
            private void Anchor(Transform owner, Vector3 point) => Group("Ride anchor", owner, point);
            private int PassengerCount => Math.Max(1, definition.serviceCapacity);
            private float PassengerSpacing => float.IsNaN(definition.attraction.passengerSpacing)
                || float.IsInfinity(definition.attraction.passengerSpacing) ? .65f : Mathf.Max(.1f, definition.attraction.passengerSpacing);

            private Transform Seat(Transform seats, int slot, Vector3 position, float yaw = 0)
            {
                var seat = Group("Passenger " + slot, seats, position);
                seat.localRotation = Quaternion.Euler(0, yaw, 0); return seat;
            }

            private void EntryRoute(int slot, params Vector3[] points)
            {
                var route = Group("Entry route " + slot);
                for (int point = 0; point < points.Length; point++) Group("Waypoint " + point, route, points[point]);
            }

            private void GridSeats(Transform seats, Vector3 centre, Vector2 area, float yaw = 0, bool lookOutward = false)
            {
                passengerCentre = centre; passengerArea = area; passengerYaw = yaw; passengerOutward = lookOutward;
                int columns = Math.Max(1, Mathf.CeilToInt(Mathf.Sqrt(PassengerCount * Mathf.Max(.1f, area.x) / Mathf.Max(.1f, area.y))));
                columns = Math.Min(columns, PassengerCount);
                int rows = Math.Max(1, Mathf.CeilToInt(PassengerCount / (float)columns));
                float dx = Mathf.Min(PassengerSpacing, area.x / Math.Max(1, columns - 1));
                float dz = Mathf.Min(PassengerSpacing, area.y / Math.Max(1, rows - 1));
                for (int slot = 0; slot < PassengerCount; slot++)
                {
                    var offset = new Vector3((slot % columns - (columns - 1) * .5f) * dx, 0,
                        (slot / columns - (rows - 1) * .5f) * dz);
                    float facing = lookOutward && offset.sqrMagnitude > .001f ? Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg : yaw;
                    Seat(seats, slot, centre + offset, facing);
                }
            }

            private Vector2 BasketSize()
            {
                int columns = Math.Max(1, Mathf.CeilToInt(Mathf.Sqrt(PassengerCount)));
                int rows = Math.Max(1, Mathf.CeilToInt(PassengerCount / (float)columns));
                return new Vector2(Mathf.Max(1.5f, columns * PassengerSpacing + .4f), Mathf.Max(1.5f, rows * PassengerSpacing + .4f));
            }

            public void ConfigurePassengers(AttractionVisualAnimator animation)
                => animation.ConfigurePassengerLayout(passengerCentre, passengerArea, passengerYaw,
                    passengerCircular, passengerOutward, passengerCarousel, passengerFloor);

            private void PassengerAreas()
            {
                var rig = parent.Find("Moving rig");
                var seats = Group("Passenger anchors", rig != null ? rig : parent);
                float doorX = w * ((definition.DoorColumn + .5f) / definition.width - .5f);
                Group("Entrance anchor", position: new Vector3(doorX, 0, -d * .5f));
                switch (definition.attraction.kind)
                {
                    case AttractionKind.ViewingTower:
                    {
                        float level = Mathf.Max(.5f, definition.attraction.rideHeight);
                        GridSeats(seats, new Vector3(0, level + .2f, -d * .04f), new Vector2(w * .55f, d * .45f), lookOutward: true);
                        for (int slot = 0; slot < PassengerCount; slot++)
                            EntryRoute(slot, new Vector3(-w * .22f, .05f, -d * .45f),
                                new Vector3(-w * .22f, .05f, d * .34f), new Vector3(-w * .22f, level + .2f, -d * .255f));
                        break;
                    }
                    case AttractionKind.BalloonRide:
                    {
                        var basket = BasketSize();
                        GridSeats(seats, new Vector3(0, .15f, 0), new Vector2(basket.x - .7f, basket.y - .7f));
                        for (int slot = 0; slot < PassengerCount; slot++)
                            EntryRoute(slot, new Vector3(doorX, .28f, -d * .4f), new Vector3(0, .7f, -basket.y * .6f));
                        break;
                    }
                    case AttractionKind.ClockworkCarousel:
                        passengerCentre = new Vector3(0, .62f, 0); passengerArea = new Vector2(w * .58f, d * .58f);
                        passengerCircular = passengerCarousel = true; passengerFloor = .12f;
                        for (int slot = 0; slot < PassengerCount; slot++)
                        {
                            float angle = slot * Mathf.PI * 2 / PassengerCount;
                            Seat(seats, slot, new Vector3(Mathf.Sin(angle) * w * .29f, .62f, Mathf.Cos(angle) * d * .29f),
                                slot * 360f / PassengerCount + 90);
                            float side = Mathf.Sin(angle) < 0 ? -1 : 1;
                            if (Mathf.Cos(angle) > 0)
                                EntryRoute(slot, new Vector3(side * w * .38f, .48f, -d * .35f), new Vector3(side * w * .38f, .48f, d * .32f));
                            else EntryRoute(slot, new Vector3(Mathf.Sin(angle) * w * .35f, .48f, -d * .35f));
                        }
                        break;
                    case AttractionKind.PredatorFeedingGallery:
                        GridSeats(seats, new Vector3(0, .04f, -d * .3f), new Vector2(w * .6f, d * .25f));
                        break;
                    case AttractionKind.TricksterTheatre:
                        GridSeats(seats, new Vector3(0, .45f, -d * .2f), new Vector2(w * .55f, d * .25f));
                        for (int slot = 0; slot < PassengerCount; slot++) EntryRoute(slot, new Vector3(doorX, .45f, -d * .4f));
                        break;
                    case AttractionKind.CabinetOfCuriosities:
                        GridSeats(seats, new Vector3(0, .03f, -d * .35f), new Vector2(w * .6f, d * .08f));
                        break;
                    case AttractionKind.ExpeditionPavilion:
                        passengerCentre = new Vector3(0, .03f, 0); passengerArea = new Vector2(w * .8f, d * .8f); passengerCircular = true;
                        for (int slot = 0; slot < PassengerCount; slot++)
                        {
                            float angle = Mathf.PI + slot * Mathf.PI * 2 / PassengerCount;
                            var point = new Vector3(Mathf.Sin(angle) * w * .4f, .03f, Mathf.Cos(angle) * d * .4f);
                            Seat(seats, slot, point, Mathf.Atan2(-point.x, -point.z) * Mathf.Rad2Deg);
                            if (point.z > -d * .25f)
                            {
                                float side = point.x < 0 ? -1 : 1;
                                EntryRoute(slot, new Vector3(side * w * .42f, .03f, -d * .4f), new Vector3(side * w * .42f, .03f, point.z));
                            }
                        }
                        break;
                    default:
                        GridSeats(seats, Vector3.up * .03f, new Vector2(w * .6f, d * .5f));
                        break;
                }
                if (rig == null && parent.Find("Ride anchor") == null) Anchor(parent, seats.GetChild(0).localPosition);
            }
            private void Platform(float y, float width, float depth, Transform owner = null)
                => Box("Platform", new Vector3(0, y, 0), new Vector3(width, .2f, depth), wood, owner);
            public void Build()
            {
                switch (definition.attraction.kind)
                {
                    case AttractionKind.PettingZoo: Petting(); break;
                    case AttractionKind.ViewingTower: Tower(); break;
                    case AttractionKind.BalloonRide: Balloon(); break;
                    case AttractionKind.PredatorFeedingGallery: Gallery(); break;
                    case AttractionKind.TricksterTheatre: Theatre(); break;
                    case AttractionKind.CabinetOfCuriosities: Cabinet(); break;
                    case AttractionKind.ExpeditionPavilion: Pavilion(); break;
                    case AttractionKind.ClockworkCarousel: Carousel(); break;
                    case AttractionKind.ViewingWindow: ViewingBay(); break;
                }
                PassengerAreas();
            }
            private void ViewingBay()
            {
                float top = Mathf.Max(1.8f, h), panelHeight = top - .4f;
                var settings = ViewingWindowDefinition.Current;
                void Pane(string name, Vector3 point, Vector3 size)
                {
                    var pane = Box(name, point, size, settings.glassColor);
                    pane.GetComponent<MeshRenderer>().sharedMaterial = ViewingGlassMaterial();
                }
                Box("Viewing bay floor", new Vector3(0, .015f, 0), new Vector3(w, .03f, d), wood);
                Box("Viewing bay roof", new Vector3(0, top, 0), new Vector3(w + .08f, .16f, d + .08f), body);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    Box("Viewing bay support", new Vector3(x * w * .48f, top * .5f, z * d * .46f), new Vector3(.13f, top, .13f), settings.frameColor);
                Pane("Viewing bay glass", new Vector3(0, top * .5f, d * .46f), new Vector3(w - .16f, panelHeight, .035f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Pane("Viewing bay side glass", new Vector3(side * w * .48f, top * .5f, 0), new Vector3(.035f, panelHeight, d * .92f));
                    Box("Viewing bay glazing rail", new Vector3(0, top * (.5f + side * .35f), d * .46f), new Vector3(w, .1f, .11f), brass);
                }
                float doorX = w * ((definition.DoorColumn + .5f) / definition.width - .5f);
                float doorWidth = w / definition.width * .72f;
                var entrance = Group("Viewing bay entrance", position: new Vector3(doorX, 0, -d * .46f));
                for (int side = -1; side <= 1; side += 2)
                    Box("Viewing bay door post", new Vector3(side * doorWidth * .5f, top * .4f, 0), new Vector3(.14f, top * .8f, .16f), brass, entrance);
                Box("Viewing bay door lintel", new Vector3(0, top * .8f, 0), new Vector3(doorWidth + .14f, .16f, .18f), brass, entrance);
                Box("Viewing bay welcome plate", new Vector3(0, top * .89f, -.07f), new Vector3(doorWidth * .8f, .2f, .05f), cream, entrance);
                Box("Viewing bay viewing rail", new Vector3(0, .78f, d * .3f), new Vector3(w * .87f, .075f, .075f), brass);
            }
            private void Petting()
            {
                float top = Mathf.Max(1.8f, h);
                Post(new Vector3(-w * .4f, 0, 0), top); Post(new Vector3(w * .4f, 0, 0), top);
                Box("Entrance arch", Vector3.up * top, new Vector3(w * .95f, .28f, .35f), body);
                for (int side = -1; side <= 1; side += 2)
                    Part("Arch finial", PrimitiveType.Sphere, new Vector3(side * w * .4f, top + .3f, 0), Vector3.one * .3f, brass);
                Box("Welcome plaque", new Vector3(0, top - .02f, -.19f), new Vector3(w * .48f, .17f, .06f), cream);
            }
            private void Tower()
            {
                float level = Mathf.Max(.5f, definition.attraction.rideHeight);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    Post(new Vector3(x * w * .35f, 0, z * d * .35f), level);
                Platform(level, w * .86f, d * .86f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Observation rail", new Vector3(side * w * .42f, level + .6f, 0), new Vector3(.12f, .13f, d * .88f), brass);
                    Box("Observation rail", new Vector3(0, level + .6f, side * d * .42f), new Vector3(w * .88f, .13f, .12f), brass);
                }
                for (int i = 0; i < 8; i++)
                    Box("Tower stair", new Vector3(-w * .22f, level * (i + .5f) / 8f, d * .34f - i * d * .085f),
                        new Vector3(w * .32f, .14f, d * .1f), cream);
                Part("Telescope", PrimitiveType.Cylinder, new Vector3(w * .17f, level + .7f, -d * .2f), new Vector3(.2f, .7f, .2f), brass,
                    rotation: new Vector3(65, 0, 0));
                Anchor(parent, new Vector3(0, level + .2f, 0));
            }
            private void Balloon()
            {
                var basket = BasketSize();
                Platform(.13f, w * .95f, d * .95f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Mooring post", new Vector3(side * w * .4f, .8f, d * .25f), new Vector3(.25f, 1.6f, .25f), brass);
                    Box("Station step", new Vector3(0, .2f + (side + 1) * .07f, -d * .44f), new Vector3(w * .45f, .3f, .45f), wood);
                }
                var rig = Group("Moving rig", position: new Vector3(0, .7f, 0));
                Box("Basket floor", Vector3.zero, new Vector3(basket.x, .2f, basket.y), wood, rig);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Basket wall", new Vector3(side * (basket.x * .5f - .05f), .32f, 0), new Vector3(.12f, .65f, basket.y), body, rig);
                    Box("Basket wall", new Vector3(0, .32f, side * (basket.y * .5f - .05f)), new Vector3(basket.x, .65f, .12f), body, rig);
                    Box("Suspension rope", new Vector3(side * (basket.x * .5f - .15f), 1.65f, 0), new Vector3(.045f, 2.5f, .045f), cream, rig);
                }
                Part("Balloon envelope", PrimitiveType.Sphere, new Vector3(0, 4.1f, 0), new Vector3(3.8f, 4.8f, 3.8f), body, rig);
                Part("Envelope crown", PrimitiveType.Sphere, new Vector3(0, 6.28f, 0), new Vector3(1.7f, .5f, 1.7f), brass, rig);
                Part("Balloon collar", PrimitiveType.Cylinder, new Vector3(0, 1.95f, 0), new Vector3(.9f, .25f, .9f), brass, rig);
                Anchor(rig, new Vector3(0, .15f, 0));
            }
            private void Gallery()
            {
                float roof = Mathf.Max(2.4f, h * .9f);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    Post(new Vector3(x * w * .42f, 0, z * d * .38f), roof);
                Box("Gallery canopy", Vector3.up * roof, new Vector3(w, .28f, d), body);
                Box("Protected observation rail", new Vector3(0, .95f, d * .43f), new Vector3(w * .92f, .14f, .12f), brass);
                for (int side = -1; side <= 1; side += 2)
                    Box("Gallery bench", new Vector3(side * w * .24f, .55f, 0), new Vector3(w * .35f, .16f, .6f), wood);
                Part("Feed demonstration drum", PrimitiveType.Cylinder, new Vector3(0, .55f, d * .22f), new Vector3(.7f, .55f, .7f), brass);
            }
            private void Theatre()
            {
                Platform(.35f, w * .94f, d * .86f);
                Box("Theatre backdrop", new Vector3(0, h * .47f, d * .37f), new Vector3(w * .82f, h * .78f, .2f), dark);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Theatre curtain", new Vector3(side * w * .29f, h * .49f, d * .3f), new Vector3(w * .21f, h * .8f, .18f), body);
                    Post(new Vector3(side * w * .43f, .35f, d * .36f), h * .82f);
                    Part("Trickster ornament", PrimitiveType.Sphere, new Vector3(side * w * .22f, h * .91f, d * .33f), Vector3.one * .45f, brass);
                }
                Box("Theatre proscenium", new Vector3(0, h * .93f, d * .32f), new Vector3(w * .95f, .25f, .4f), brass);
                Box("Stage step", new Vector3(0, .16f, -d * .46f), new Vector3(w * .55f, .25f, .5f), wood);
            }
            private void Cabinet()
            {
                Box("Curiosity cabinet", new Vector3(0, h * .46f, d * .08f), new Vector3(w * .75f, h * .88f, d * .5f), wood);
                Box("Display recess", new Vector3(0, h * .49f, -d * .19f), new Vector3(w * .65f, h * .7f, .1f), dark);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Cabinet frame", new Vector3(side * w * .33f, h * .5f, -d * .22f), new Vector3(.12f, h * .76f, .15f), brass);
                    Box("Cabinet frame", new Vector3(0, h * (.5f + side * .38f), -d * .22f), new Vector3(w * .7f, .12f, .15f), brass);
                }
                Box("Display shelf", new Vector3(0, h * .44f, -d * .23f), new Vector3(w * .65f, .12f, .65f), cream);
                Part("Mythical egg", PrimitiveType.Sphere, new Vector3(-w * .18f, h * .6f, -d * .24f), new Vector3(.55f, .85f, .55f), body);
                Part("Spirit orb", PrimitiveType.Sphere, new Vector3(w * .18f, h * .62f, -d * .24f), Vector3.one * .6f, new Color(.3f, .9f, .8f));
                Part("Antique horn", PrimitiveType.Cylinder, new Vector3(0, h * .66f, -d * .24f), new Vector3(.15f, .65f, .15f), brass,
                    rotation: new Vector3(10, 0, 25));
            }
            private void Pavilion()
            {
                float roof = h * .9f;
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    Post(new Vector3(x * w * .4f, 0, z * d * .4f), roof);
                Part("Expedition pavilion roof", PrimitiveType.Cylinder, new Vector3(0, roof, 0), new Vector3(w, .2f, d), body);
                Box("Map display table", new Vector3(0, .85f, 0), new Vector3(w * .55f, .16f, d * .46f), cream);
                Box("Map table pedestal", new Vector3(0, .45f, 0), new Vector3(.35f, .9f, .35f), brass);
                var globe = Part("Exhibition globe", PrimitiveType.Sphere, new Vector3(0, 1.6f, 0), Vector3.one * 1.15f, new Color(.2f, .48f, .7f));
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * .65f;
                    Part("Globe land", PrimitiveType.Sphere, new Vector3(Mathf.Sin(angle) * .46f, .15f, Mathf.Cos(angle) * .46f), new Vector3(.42f, .35f, .25f), body, globe);
                }
            }
            private void Carousel()
            {
                Part("Carousel foundation", PrimitiveType.Cylinder, new Vector3(0, .15f, 0), new Vector3(w * .92f, .15f, d * .92f), wood);
                var rig = Group("Moving rig", position: new Vector3(0, .35f, 0));
                Part("Carousel turntable", PrimitiveType.Cylinder, Vector3.zero, new Vector3(w * .86f, .12f, d * .86f), body, rig);
                Part("Carousel centre pole", PrimitiveType.Cylinder, new Vector3(0, h * .45f, 0), new Vector3(.18f, h * .45f, .18f), brass, rig);
                Part("Carousel canopy", PrimitiveType.Sphere, new Vector3(0, h * .85f, 0), new Vector3(w * .92f, .75f, d * .92f), cream, rig);
                for (int i = 0; i < PassengerCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / PassengerCount;
                    var horse = Group("Clockwork mythical mount", rig, new Vector3(Mathf.Sin(angle) * w * .29f, .72f, Mathf.Cos(angle) * d * .29f));
                    horse.localRotation = Quaternion.Euler(0, i * 360f / PassengerCount + 90, 0);
                    Part("Mount body", PrimitiveType.Sphere, Vector3.zero, new Vector3(.42f, .4f, .8f), body, horse);
                    Part("Mount head", PrimitiveType.Sphere, new Vector3(0, .3f, .4f), new Vector3(.32f, .38f, .38f), cream, horse);
                    Part("Mount horn", PrimitiveType.Cylinder, new Vector3(0, .65f, .5f), new Vector3(.06f, .18f, .06f), brass, horse);
                    for (int side = -1; side <= 1; side += 2) for (int end = -1; end <= 1; end += 2)
                        Box("Mount leg", new Vector3(side * .13f, -.35f, end * .25f), new Vector3(.08f, .4f, .08f), cream, horse);
                    Part("Mount support pole", PrimitiveType.Cylinder, new Vector3(0, .35f, 0), new Vector3(.06f, 1.1f, .06f), brass, horse);
                }
                Anchor(rig, Vector3.up * .2f);
            }
        }
    }
}
