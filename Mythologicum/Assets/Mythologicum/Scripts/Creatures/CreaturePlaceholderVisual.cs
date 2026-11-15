using UnityEngine;

namespace Mythologicum
{
    // Serialized values are stable: existing species keep their capsule unless selected explicitly.
    public enum CreaturePlaceholderKind { Capsule, Wolpertinger, Unicorn, Waldschrat }

    /// <summary>Recognizable temporary bodies using shared materials and collider-free visual parts.</summary>
    public static class CreaturePlaceholderVisual
    {
        public static bool TryCreate(Transform parent, CreatureDefinition definition, Material material,
            out GameObject body, out MeshRenderer renderer)
        {
            body = null; renderer = null;
            if (definition.placeholderKind == CreaturePlaceholderKind.Capsule) return false;
            if (definition.placeholderKind != CreaturePlaceholderKind.Wolpertinger
                && definition.placeholderKind != CreaturePlaceholderKind.Unicorn
                && definition.placeholderKind != CreaturePlaceholderKind.Waldschrat) return false;
            body = new GameObject(definition.placeholderKind + " placeholder");
            body.transform.SetParent(parent, false);
            body.transform.localScale = definition.bodyScale;
            var builder = new Builder(body.transform, material, definition);
            switch (definition.placeholderKind)
            {
                case CreaturePlaceholderKind.Wolpertinger: renderer = builder.Rabbit(); break;
                case CreaturePlaceholderKind.Unicorn: renderer = builder.Horse(); break;
                case CreaturePlaceholderKind.Waldschrat: renderer = builder.ForestGiant(); break;
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
            {
                this.parent = parent; this.material = material;
                coat = definition.color; accent = definition.placeholderAccentColor; detail = definition.placeholderDetailColor;
            }
            private MeshRenderer Part(string name, PrimitiveType primitive, Vector3 point, Vector3 size, Color color,
                Vector3 rotation = default)
            {
                var part = GameObject.CreatePrimitive(primitive); part.name = name;
                part.transform.SetParent(parent, false); part.transform.localPosition = point;
                part.transform.localScale = size; part.transform.localRotation = Quaternion.Euler(rotation);
                part.GetComponent<Collider>().enabled = false;
                var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
                return renderer;
            }
            private void Branch(string name, Vector3 from, Vector3 to, float width, Color color)
            {
                var renderer = Part(name, PrimitiveType.Cylinder, (from + to) * .5f,
                    new Vector3(width, (to - from).magnitude * .5f, width), color);
                renderer.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            }
            public MeshRenderer Rabbit()
            {
                var torso = Part("Rabbit body", PrimitiveType.Sphere, new Vector3(0, -.34f, -.08f), new Vector3(.92f, 1f, 1f), coat);
                Part("Rabbit head", PrimitiveType.Sphere, new Vector3(0, .05f, .35f), new Vector3(.68f, .65f, .6f), coat);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(side < 0 ? "Left long ear" : "Right long ear", PrimitiveType.Capsule,
                        new Vector3(side * .19f, .65f, .28f), new Vector3(.16f, .5f, .14f), coat, new Vector3(0, 0, -side * 8));
                    Part("Ear inner", PrimitiveType.Capsule, new Vector3(side * .19f, .65f, .35f), new Vector3(.075f, .37f, .025f), accent,
                        new Vector3(0, 0, -side * 8));
                    Part("Front paw", PrimitiveType.Sphere, new Vector3(side * .25f, -.88f, .32f), new Vector3(.25f, .24f, .35f), coat);
                    Part("Hind paw", PrimitiveType.Sphere, new Vector3(side * .3f, -.85f, -.34f), new Vector3(.32f, .3f, .4f), coat);
                    Part("Eye", PrimitiveType.Sphere, new Vector3(side * .23f, .14f, .59f), Vector3.one * .09f, detail);
                    var root = new Vector3(side * .29f, .28f, .3f);
                    var tip = new Vector3(side * .55f, .75f, .19f);
                    Branch("Antler stem", root, tip, .055f, accent);
                    Branch("Antler tine", Vector3.Lerp(root, tip, .55f), new Vector3(side * .7f, .64f, .27f), .04f, accent);
                }
                Part("Rabbit nose", PrimitiveType.Sphere, new Vector3(0, -.005f, .66f), new Vector3(.13f, .1f, .1f), detail);
                Part("Fluffy tail", PrimitiveType.Sphere, new Vector3(0, -.2f, -.6f), Vector3.one * .35f, accent);
                return torso;
            }
            public MeshRenderer Horse()
            {
                var torso = Part("Horse body", PrimitiveType.Sphere, new Vector3(0, -.05f, -.08f), new Vector3(.78f, .65f, 1.12f), coat);
                for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                {
                    Part("Horse leg", PrimitiveType.Cylinder, new Vector3(side * .25f, -.65f, end * .35f), new Vector3(.12f, .35f, .12f), coat);
                    Part("Hoof", PrimitiveType.Cube, new Vector3(side * .25f, -.95f, end * .35f + .025f), new Vector3(.17f, .1f, .22f), detail);
                }
                Part("Horse neck", PrimitiveType.Capsule, new Vector3(0, .33f, .34f), new Vector3(.32f, .43f, .32f), coat, new Vector3(18, 0, 0));
                Part("Horse head", PrimitiveType.Sphere, new Vector3(0, .72f, .51f), new Vector3(.35f, .33f, .55f), coat);
                Part("Muzzle", PrimitiveType.Sphere, new Vector3(0, .65f, .74f), new Vector3(.29f, .22f, .22f), accent);
                Part("Mane", PrimitiveType.Capsule, new Vector3(0, .44f, .19f), new Vector3(.13f, .45f, .18f), accent, new Vector3(18, 0, 0));
                Part("Tail", PrimitiveType.Capsule, new Vector3(0, -.13f, -.78f), new Vector3(.16f, .37f, .16f), accent, new Vector3(-40, 0, 0));
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Horse ear", PrimitiveType.Capsule, new Vector3(side * .11f, .96f, .39f), new Vector3(.08f, .15f, .08f), coat,
                        new Vector3(0, 0, -side * 12));
                    Part("Eye", PrimitiveType.Sphere, new Vector3(side * .16f, .78f, .63f), Vector3.one * .065f, detail);
                }
                Branch("Unicorn horn base", new Vector3(0, .87f, .57f), new Vector3(0, 1.14f, .7f), .095f, accent);
                Branch("Unicorn horn tip", new Vector3(0, 1.14f, .7f), new Vector3(0, 1.36f, .81f), .045f, accent);
                return torso;
            }
            public MeshRenderer ForestGiant()
            {
                var torso = Part("Bark torso", PrimitiveType.Sphere, new Vector3(0, -.04f, 0), new Vector3(.88f, .98f, .61f), coat);
                for (int side = -1; side <= 1; side += 2)
                {
                    Branch("Root leg", new Vector3(side * .23f, -.95f, 0), new Vector3(side * .22f, -.37f, 0), .24f, coat);
                    Part("Root foot", PrimitiveType.Cube, new Vector3(side * .24f, -.94f, .07f), new Vector3(.33f, .12f, .42f), coat);
                    Branch("Branch arm", new Vector3(side * .31f, .22f, 0), new Vector3(side * .6f, -.38f, .03f), .2f, coat);
                    Part("Wooden hand", PrimitiveType.Sphere, new Vector3(side * .63f, -.44f, .06f), new Vector3(.28f, .32f, .23f), coat);
                    var stem = new Vector3(side * .17f, .65f, -.03f);
                    var tip = new Vector3(side * .42f, 1.13f, -.04f);
                    Branch("Crown branch", stem, tip, .075f, coat);
                    Branch("Crown twig", Vector3.Lerp(stem, tip, .6f), new Vector3(side * .6f, 1.04f, .06f), .05f, coat);
                    Part("Leaf crown", PrimitiveType.Sphere, new Vector3(side * .3f, 1.04f, -.04f), new Vector3(.55f, .43f, .48f), accent);
                    Part("Amber eye", PrimitiveType.Sphere, new Vector3(side * .115f, .59f, .265f), new Vector3(.075f, .1f, .045f), detail);
                }
                Part("Wooden head", PrimitiveType.Sphere, new Vector3(0, .55f, .015f), new Vector3(.53f, .5f, .5f), coat);
                Part("Leaf beard", PrimitiveType.Capsule, new Vector3(0, .32f, .27f), new Vector3(.26f, .19f, .12f), accent);
                return torso;
            }
        }
    }
}
