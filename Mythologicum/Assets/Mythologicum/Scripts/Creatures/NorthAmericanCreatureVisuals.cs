using UnityEngine;

namespace Mythologicum
{
    /// <summary>Temporary bodies; species assets and simulation remain independent of appearance.</summary>
    public static class NorthAmericanCreatureVisuals
    {
        public static bool TryCreate(Transform parent, CreatureDefinition definition, Material material,
            out GameObject body, out MeshRenderer renderer)
        {
            body = null; renderer = null;
            if (definition.placeholderKind != CreaturePlaceholderKind.Wendigo
                && definition.placeholderKind != CreaturePlaceholderKind.Thunderbird
                && definition.placeholderKind != CreaturePlaceholderKind.Kalupelik) return false;
            body = new GameObject(definition.placeholderKind + " placeholder");
            body.transform.SetParent(parent, false); body.transform.localScale = definition.bodyScale;
            var builder = new Builder(body.transform, material, definition);
            switch (definition.placeholderKind)
            {
                case CreaturePlaceholderKind.Wendigo: renderer = builder.Wendigo(); break;
                case CreaturePlaceholderKind.Thunderbird: renderer = builder.Thunderbird(); break;
                case CreaturePlaceholderKind.Kalupelik: renderer = builder.Kalupelik(); break;
            }
            return true;
        }

        private sealed class Builder
        {
            private readonly Transform parent;
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private readonly Color coat, accent, detail;
            public Builder(Transform parent, Material material, CreatureDefinition definition)
            { this.parent = parent; this.material = material; coat = definition.color; accent = definition.placeholderAccentColor; detail = definition.placeholderDetailColor; }

            private MeshRenderer Part(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color,
                Transform owner = null, Vector3 rotation = default)
            {
                var item = GameObject.CreatePrimitive(primitive); item.name = name;
                item.transform.SetParent(owner != null ? owner : parent, false);
                item.transform.localPosition = position; item.transform.localScale = scale;
                item.transform.localRotation = Quaternion.Euler(rotation);
                item.GetComponent<Collider>().enabled = false;
                var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color); renderer.SetPropertyBlock(properties);
                return renderer;
            }
            private void Limb(string name, Vector3 from, Vector3 to, float width, Color color, Transform owner = null)
            {
                var piece = Part(name, PrimitiveType.Cylinder, (from + to) / 2,
                    new Vector3(width, Vector3.Distance(from, to) / 2, width), color, owner);
                piece.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            }

            public MeshRenderer Wendigo()
            {
                var body = Part("Gaunt humanoid torso", PrimitiveType.Sphere, new Vector3(0, .08f, -.02f), new Vector3(.48f, .88f, .3f), coat);
                Part("Wendigo narrow head", PrimitiveType.Sphere, new Vector3(0, .69f, .06f), new Vector3(.38f, .43f, .34f), coat);
                Limb("Long bare neck", new Vector3(0, .43f, 0), new Vector3(0, .6f, .04f), .14f, coat);
                Part("Hungry dark mouth", PrimitiveType.Sphere, new Vector3(0, .59f, .225f), new Vector3(.22f, .16f, .025f), detail);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Wendigo sunken eye", PrimitiveType.Sphere, new Vector3(side * .105f, .73f, .22f), new Vector3(.07f, .06f, .035f), detail);
                    Vector3 shoulder = new Vector3(side * .22f, .35f, -.02f), elbow = new Vector3(side * .4f, -.08f, .02f), hand = new Vector3(side * .52f, -.67f, .2f);
                    Limb("Wendigo upper arm", shoulder, elbow, .1f, coat); Limb("Wendigo long forearm", elbow, hand, .085f, coat);
                    Part("Wendigo grasping hand", PrimitiveType.Sphere, hand, new Vector3(.14f, .18f, .14f), coat);
                    for (int finger = 0; finger < 3; finger++)
                        Limb("Wendigo long finger", hand + new Vector3((finger - 1) * .04f, -.03f, .04f),
                            hand + new Vector3((finger - 1) * .045f, -.2f, .1f), .025f, accent);
                    Vector3 hip = new Vector3(side * .12f, -.28f, -.02f), knee = new Vector3(side * .16f, -.58f, .05f), ankle = new Vector3(side * .17f, -.91f, 0);
                    Limb("Wendigo thigh", hip, knee, .13f, coat); Limb("Wendigo shin", knee, ankle, .09f, coat);
                    Part("Wendigo bare foot", PrimitiveType.Cube, new Vector3(side * .17f, -.94f, .1f), new Vector3(.18f, .12f, .36f), accent);
                    for (int rib = 0; rib < 3; rib++)
                        Part("Visible rib", PrimitiveType.Cube, new Vector3(side * .105f, .25f - rib * .12f, .135f),
                            new Vector3(.17f, .025f, .025f), accent, rotation: new Vector3(0, 0, side * 8));
                }
                return body;
            }

            public MeshRenderer Thunderbird()
            {
                var body = Part("Thunderbird feathered breast", PrimitiveType.Sphere, new Vector3(0, -.03f, -.05f), new Vector3(.78f, .9f, 1.25f), coat);
                Part("Thunderbird head", PrimitiveType.Sphere, new Vector3(0, .42f, .51f), new Vector3(.59f, .59f, .57f), coat);
                Part("Thunderbird hooked beak", PrimitiveType.Sphere, new Vector3(0, .34f, .83f), new Vector3(.3f, .28f, .45f), accent,
                    rotation: new Vector3(28, 0, 0));
                Part("Crest feather", PrimitiveType.Cube, new Vector3(0, .8f, .37f), new Vector3(.18f, .44f, .13f), accent,
                    rotation: new Vector3(-24, 0, 0));
                for (int side = -1; side <= 1; side += 2)
                {
                    string sideName = side < 0 ? "Left" : "Right";
                    Part(sideName + " lightning eye", PrimitiveType.Sphere, new Vector3(side * .21f, .47f, .74f), new Vector3(.12f, .105f, .055f), detail);
                    var pivot = new GameObject(sideName + " wing").transform; pivot.SetParent(parent, false);
                    pivot.localPosition = new Vector3(side * .3f, .12f, -.04f);
                    Part("Broad flight wing", PrimitiveType.Sphere, new Vector3(side * .85f, 0, -.09f), new Vector3(1.9f, .2f, .9f), coat, pivot,
                        rotation: new Vector3(0, side * 12, 0));
                    for (int feather = 0; feather < 4; feather++)
                        Part("Lightning wing feather", PrimitiveType.Cube, new Vector3(side * (1.4f + feather * .13f), -.02f, -.2f - feather * .16f),
                            new Vector3(.8f, .12f, .2f), feather % 2 == 0 ? accent : coat, pivot, new Vector3(0, side * (20 + feather * 7), 0));
                    Limb("Thunderbird leg", new Vector3(side * .21f, -.3f, .04f), new Vector3(side * .23f, -.84f, .08f), .11f, accent);
                    Part("Thunderbird talon", PrimitiveType.Cube, new Vector3(side * .23f, -.94f, .24f), new Vector3(.22f, .12f, .52f), accent);
                }
                for (int feather = -1; feather <= 1; feather++)
                    Part("Thunderbird tail feather", PrimitiveType.Cube, new Vector3(feather * .19f, -.1f, -.9f), new Vector3(.22f, .14f, 1.0f),
                        feather == 0 ? accent : coat, rotation: new Vector3(0, feather * 12, 0));
                return body;
            }

            public MeshRenderer Kalupelik()
            {
                var body = Part("Kalupelik sea-green parka", PrimitiveType.Sphere, new Vector3(0, -.05f, 0), new Vector3(.82f, 1.1f, .62f), coat);
                Part("Kalupelik hood", PrimitiveType.Sphere, new Vector3(0, .56f, .01f), new Vector3(.76f, .72f, .65f), accent);
                Part("Kalupelik hidden face", PrimitiveType.Sphere, new Vector3(0, .55f, .3f), new Vector3(.46f, .48f, .16f), coat);
                Part("Amauti back pouch", PrimitiveType.Sphere, new Vector3(0, .01f, -.39f), new Vector3(.7f, .94f, .53f), accent);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Kalupelik pale eye", PrimitiveType.Sphere, new Vector3(side * .11f, .61f, .39f), new Vector3(.075f, .06f, .025f), detail);
                    Limb("Kalupelik arm", new Vector3(side * .31f, .22f, 0), new Vector3(side * .54f, -.42f, .19f), .16f, coat);
                    for (int finger = 0; finger < 3; finger++)
                        Limb("Kalupelik long swimming finger", new Vector3(side * .54f + (finger - 1) * .05f, -.41f, .2f),
                            new Vector3(side * .58f + (finger - 1) * .06f, -.65f, .37f), .035f, coat);
                    Limb("Kalupelik lower leg", new Vector3(side * .2f, -.43f, -.03f), new Vector3(side * .21f, -.86f, .04f), .2f, coat);
                    Part("Kalupelik webbed foot", PrimitiveType.Cube, new Vector3(side * .22f, -.94f, .17f), new Vector3(.32f, .12f, .5f), coat);
                    Part("Parka shoulder seam", PrimitiveType.Cube, new Vector3(side * .25f, .34f, .27f), new Vector3(.22f, .035f, .035f), accent);
                }
                return body;
            }
        }
    }
}
