using UnityEngine;

namespace Mythologicum
{
    // Serialized values are stable: existing species keep their capsule unless selected explicitly.
    public enum CreaturePlaceholderKind
    {
        Capsule = 0, Wolpertinger = 1, Unicorn = 2, Waldschrat = 3, Kappa = 4, Nokk = 5,
        Tatzelwurm = 6, AllghoiKhorkhoi = 7, MokeleMbembe = 8, NinkiNanka = 9, Abada = 10,
        NandiBear = 11, Dingonek = 12, Marozi = 13, Grootslang = 14, Inkanyamba = 15,
        Aigamuxa = 16, Manticore = 17, Wendigo = 18, Thunderbird = 19, Kalupelik = 20
    }

    /// <summary>Recognizable temporary bodies using shared materials and collider-free visual parts.</summary>
    public static class CreaturePlaceholderVisual
    {
        public static bool TryCreate(Transform parent, CreatureDefinition definition, Material material,
            out GameObject body, out MeshRenderer renderer)
        {
            body = null; renderer = null;
            if (NorthAmericanCreatureVisuals.TryCreate(parent, definition, material, out body, out renderer)) return true;
            if (definition.placeholderKind < CreaturePlaceholderKind.Wolpertinger
                || definition.placeholderKind > CreaturePlaceholderKind.Manticore) return false;
            body = new GameObject(definition.placeholderKind + " placeholder");
            body.transform.SetParent(parent, false);
            body.transform.localScale = definition.bodyScale;
            var builder = new Builder(body.transform, material, definition);
            switch (definition.placeholderKind)
            {
                case CreaturePlaceholderKind.Wolpertinger: renderer = builder.Rabbit(); break;
                case CreaturePlaceholderKind.Unicorn: renderer = builder.Horse(); break;
                case CreaturePlaceholderKind.Waldschrat: renderer = builder.ForestGiant(); break;
                case CreaturePlaceholderKind.Kappa: renderer = builder.RiverTurtle(); break;
                case CreaturePlaceholderKind.Nokk: renderer = builder.Horse(false); break;
                case CreaturePlaceholderKind.Tatzelwurm: renderer = builder.CatSerpent(); break;
                case CreaturePlaceholderKind.AllghoiKhorkhoi: renderer = builder.DesertWorm(); break;
                case CreaturePlaceholderKind.MokeleMbembe: renderer = builder.Sauropod(); break;
                case CreaturePlaceholderKind.NinkiNanka: renderer = builder.SwampDragon(); break;
                case CreaturePlaceholderKind.Abada: renderer = builder.TwoHornedHorse(); break;
                case CreaturePlaceholderKind.NandiBear: renderer = builder.HighShoulderedBear(); break;
                case CreaturePlaceholderKind.Dingonek: renderer = builder.SaberCrocodile(); break;
                case CreaturePlaceholderKind.Marozi: renderer = builder.SpottedLion(); break;
                case CreaturePlaceholderKind.Grootslang: renderer = builder.ElephantSerpent(); break;
                case CreaturePlaceholderKind.Inkanyamba: renderer = builder.HorseSerpent(); break;
                case CreaturePlaceholderKind.Aigamuxa: renderer = builder.FootEyedGiant(); break;
                case CreaturePlaceholderKind.Manticore: renderer = builder.Manticore(); break;
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
            // Feet rest at normalized y = -1. World movement supplies bodyScale and animates this root.
            private void FourLegs(string name, float spread, float front, float rear, float hipY, float width, Color footColor)
            {
                for (int side = -1; side <= 1; side += 2)
                for (int end = 0; end < 2; end++)
                {
                    float z = end == 0 ? front : rear;
                    string limb = name + (end == 0 ? " front" : " hind");
                    Branch(limb + " leg", new Vector3(side * spread, hipY, z),
                        new Vector3(side * spread, -.88f, z), width, coat);
                    Part(limb + " foot", PrimitiveType.Sphere, new Vector3(side * spread, -.91f, z + .035f),
                        new Vector3(width * 1.45f, .18f, width * 1.8f), footColor);
                }
            }
            private void Eyes(string name, Vector3 point, float spread, float size)
            {
                for (int side = -1; side <= 1; side += 2)
                    Part(name + " eye", PrimitiveType.Sphere, point + Vector3.right * (side * spread),
                        Vector3.one * size, detail);
            }
            private void Tail(string name, Vector3[] points, float width, Color color)
            {
                for (int segment = 0; segment < points.Length - 1; segment++)
                    Branch(name + " segment " + segment, points[segment], points[segment + 1],
                        width * (1f - segment / (float)points.Length), color);
            }
            public MeshRenderer Sauropod()
            {
                var torso = Part("Mokele-Mbembe body", PrimitiveType.Sphere, new Vector3(0, -.28f, -.18f),
                    new Vector3(.98f, .78f, 1.4f), coat);
                FourLegs("Mokele-Mbembe", .31f, .27f, -.62f, -.37f, .23f, coat);
                Branch("Mokele-Mbembe lower neck", new Vector3(0, -.02f, .39f), new Vector3(0, .62f, .62f), .32f, coat);
                Branch("Mokele-Mbembe upper neck", new Vector3(0, .62f, .62f), new Vector3(0, 1.08f, .86f), .24f, coat);
                Part("Mokele-Mbembe head", PrimitiveType.Sphere, new Vector3(0, 1.12f, .96f), new Vector3(.36f, .3f, .5f), coat);
                Part("Mokele-Mbembe muzzle", PrimitiveType.Sphere, new Vector3(0, 1.06f, 1.17f), new Vector3(.3f, .19f, .28f), accent);
                Eyes("Mokele-Mbembe", new Vector3(0, 1.18f, 1.06f), .15f, .06f);
                Tail("Mokele-Mbembe tail", new[] { new Vector3(0, -.3f, -.76f), new Vector3(.08f, -.4f, -1.13f),
                    new Vector3(.24f, -.54f, -1.48f), new Vector3(.39f, -.67f, -1.76f) }, .25f, coat);
                return torso;
            }
            public MeshRenderer SwampDragon()
            {
                var torso = Part("Ninki Nanka body", PrimitiveType.Sphere, new Vector3(0, -.46f, -.2f),
                    new Vector3(.9f, .6f, 1.35f), coat);
                FourLegs("Ninki Nanka", .37f, .24f, -.64f, -.5f, .19f, accent);
                Branch("Ninki Nanka lower neck", new Vector3(0, -.38f, .33f), new Vector3(0, .16f, .62f), .32f, coat);
                Branch("Ninki Nanka upper neck", new Vector3(0, .16f, .62f), new Vector3(0, .58f, .86f), .24f, coat);
                Part("Ninki Nanka head", PrimitiveType.Sphere, new Vector3(0, .61f, 1.02f), new Vector3(.48f, .34f, .6f), coat);
                Part("Ninki Nanka crocodile snout", PrimitiveType.Sphere, new Vector3(0, .54f, 1.37f),
                    new Vector3(.4f, .2f, .62f), coat);
                Part("Ninki Nanka jaw", PrimitiveType.Sphere, new Vector3(0, .45f, 1.34f), new Vector3(.36f, .09f, .61f), accent);
                Eyes("Ninki Nanka", new Vector3(0, .72f, 1.16f), .2f, .08f);
                for (int side = -1; side <= 1; side += 2)
                    Branch("Ninki Nanka horn", new Vector3(side * .17f, .75f, .86f),
                        new Vector3(side * .27f, 1.04f, .66f), .075f, accent);
                for (int ridge = 0; ridge < 3; ridge++)
                    Part("Ninki Nanka dorsal ridge", PrimitiveType.Cube, new Vector3(0, -.12f, .12f - ridge * .3f),
                        new Vector3(.11f, .18f, .18f), accent, new Vector3(0, 0, 45));
                Tail("Ninki Nanka tail", new[] { new Vector3(0, -.44f, -.76f), new Vector3(.12f, -.54f, -1.12f),
                    new Vector3(.28f, -.67f, -1.43f), new Vector3(.44f, -.76f, -1.69f) }, .29f, coat);
                return torso;
            }
            public MeshRenderer TwoHornedHorse()
            {
                var torso = Horse(false);
                Branch("Abada forehead horn", new Vector3(0, .9f, .49f), new Vector3(0, 1.17f, .58f), .085f, accent);
                Branch("Abada nose horn", new Vector3(0, .78f, .75f), new Vector3(0, 1.02f, .87f), .065f, accent);
                return torso;
            }
            public MeshRenderer HighShoulderedBear()
            {
                var torso = Part("Nandi Bear body", PrimitiveType.Sphere, new Vector3(0, -.29f, -.19f),
                    new Vector3(.98f, .77f, 1.15f), coat);
                Part("Nandi Bear shoulder hump", PrimitiveType.Sphere, new Vector3(0, .02f, .25f),
                    new Vector3(.94f, .68f, .77f), coat);
                FourLegs("Nandi Bear", .32f, .39f, -.58f, -.33f, .25f, coat);
                Part("Nandi Bear head", PrimitiveType.Sphere, new Vector3(0, .12f, .73f), new Vector3(.67f, .57f, .61f), coat);
                Part("Nandi Bear muzzle", PrimitiveType.Sphere, new Vector3(0, -.01f, 1.01f), new Vector3(.42f, .28f, .34f), accent);
                Part("Nandi Bear nose", PrimitiveType.Sphere, new Vector3(0, .02f, 1.16f), new Vector3(.2f, .13f, .09f), detail);
                Eyes("Nandi Bear", new Vector3(0, .23f, .97f), .2f, .075f);
                for (int side = -1; side <= 1; side += 2)
                    Part("Nandi Bear round ear", PrimitiveType.Sphere, new Vector3(side * .23f, .43f, .63f),
                        new Vector3(.21f, .24f, .15f), coat);
                Part("Nandi Bear tail", PrimitiveType.Sphere, new Vector3(0, -.28f, -.79f), Vector3.one * .19f, coat);
                return torso;
            }
            public MeshRenderer SaberCrocodile()
            {
                var torso = Part("Dingonek body", PrimitiveType.Sphere, new Vector3(0, -.42f, -.17f),
                    new Vector3(.98f, .65f, 1.4f), coat);
                FourLegs("Dingonek", .39f, .31f, -.65f, -.49f, .2f, coat);
                Part("Dingonek walrus head", PrimitiveType.Sphere, new Vector3(0, -.12f, .76f),
                    new Vector3(.72f, .58f, .66f), coat);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Dingonek whisker muzzle", PrimitiveType.Sphere, new Vector3(side * .16f, -.24f, 1.04f),
                        new Vector3(.34f, .24f, .27f), accent);
                    Branch("Dingonek saber tusk", new Vector3(side * .23f, -.25f, 1.1f),
                        new Vector3(side * .26f, -.79f, 1.19f), .085f, accent);
                }
                Branch("Dingonek head horn", new Vector3(0, .12f, .83f), new Vector3(0, .39f, .91f), .095f, accent);
                Eyes("Dingonek", new Vector3(0, -.025f, .99f), .25f, .08f);
                for (int ridge = 0; ridge < 3; ridge++)
                    Part("Dingonek back scute", PrimitiveType.Sphere, new Vector3(0, -.08f, .15f - ridge * .32f),
                        new Vector3(.27f, .16f, .24f), accent);
                Tail("Dingonek crocodile tail", new[] { new Vector3(0, -.4f, -.76f), new Vector3(-.08f, -.52f, -1.13f),
                    new Vector3(-.22f, -.67f, -1.46f), new Vector3(-.41f, -.77f, -1.72f) }, .31f, coat);
                return torso;
            }
            private MeshRenderer LionBody(string name)
            {
                var torso = Part(name + " body", PrimitiveType.Sphere, new Vector3(0, -.29f, -.17f),
                    new Vector3(.79f, .72f, 1.22f), coat);
                FourLegs(name, .27f, .3f, -.62f, -.3f, .18f, coat);
                return torso;
            }
            public MeshRenderer SpottedLion()
            {
                var torso = LionBody("Marozi");
                Part("Marozi short mane", PrimitiveType.Sphere, new Vector3(0, -.02f, .4f), new Vector3(.72f, .69f, .49f), accent);
                Part("Marozi lion head", PrimitiveType.Sphere, new Vector3(0, .08f, .69f), new Vector3(.57f, .5f, .49f), coat);
                Part("Marozi muzzle", PrimitiveType.Sphere, new Vector3(0, -.04f, .93f), new Vector3(.41f, .23f, .23f), accent);
                Part("Marozi nose", PrimitiveType.Sphere, new Vector3(0, .015f, 1.035f), new Vector3(.15f, .1f, .07f), detail);
                Eyes("Marozi", new Vector3(0, .17f, .88f), .17f, .07f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Marozi round ear", PrimitiveType.Sphere, new Vector3(side * .22f, .34f, .63f),
                        new Vector3(.18f, .21f, .12f), coat);
                    for (int spot = 0; spot < 3; spot++)
                        Part("Marozi flank spot", PrimitiveType.Sphere, new Vector3(side * .377f, -.2f - (spot % 2) * .12f, .03f - spot * .27f),
                            new Vector3(.045f, .14f, .15f), detail);
                }
                Tail("Marozi tail", new[] { new Vector3(0, -.27f, -.73f), new Vector3(.16f, -.37f, -1.03f),
                    new Vector3(.36f, -.23f, -1.23f) }, .085f, coat);
                Part("Marozi tail tuft", PrimitiveType.Sphere, new Vector3(.36f, -.23f, -1.23f), new Vector3(.18f, .22f, .18f), accent);
                return torso;
            }
            public MeshRenderer ElephantSerpent()
            {
                var torso = Part("Grootslang body", PrimitiveType.Sphere, new Vector3(0, -.3f, -.04f),
                    new Vector3(1.02f, .87f, 1.13f), coat);
                FourLegs("Grootslang", .33f, .31f, -.46f, -.27f, .25f, coat);
                Part("Grootslang elephant head", PrimitiveType.Sphere, new Vector3(0, .15f, .66f),
                    new Vector3(.69f, .69f, .66f), coat);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Grootslang elephant ear", PrimitiveType.Sphere, new Vector3(side * .41f, .13f, .59f),
                        new Vector3(.46f, .59f, .15f), coat, new Vector3(0, side * -18, 0));
                    Branch("Grootslang tusk base", new Vector3(side * .25f, -.04f, .87f),
                        new Vector3(side * .29f, -.22f, 1.19f), .095f, accent);
                    Branch("Grootslang tusk tip", new Vector3(side * .29f, -.22f, 1.19f),
                        new Vector3(side * .3f, -.06f, 1.36f), .055f, accent);
                }
                Eyes("Grootslang", new Vector3(0, .24f, .91f), .22f, .075f);
                Tail("Grootslang trunk", new[] { new Vector3(0, .12f, .95f), new Vector3(0, -.26f, 1.1f),
                    new Vector3(0, -.63f, 1.12f), new Vector3(0, -.72f, 1.3f) }, .21f, coat);
                Tail("Grootslang serpent tail", new[] { new Vector3(0, -.3f, -.53f), new Vector3(.14f, -.44f, -.91f),
                    new Vector3(.35f, -.62f, -1.25f), new Vector3(.32f, -.74f, -1.6f),
                    new Vector3(.06f, -.79f, -1.86f), new Vector3(-.22f, -.78f, -2f) }, .42f, coat);
                return torso;
            }
            public MeshRenderer HorseSerpent()
            {
                var torso = Part("Inkanyamba serpent body", PrimitiveType.Sphere, new Vector3(0, -.62f, .03f),
                    new Vector3(.69f, .64f, .96f), coat);
                Tail("Inkanyamba serpent tail", new[] { new Vector3(0, -.65f, -.2f), new Vector3(.19f, -.72f, -.6f),
                    new Vector3(.4f, -.79f, -.93f), new Vector3(.34f, -.82f, -1.29f),
                    new Vector3(.06f, -.83f, -1.56f), new Vector3(-.24f, -.83f, -1.7f) }, .46f, coat);
                Branch("Inkanyamba lower neck", new Vector3(0, -.57f, .36f), new Vector3(0, -.03f, .62f), .35f, coat);
                Branch("Inkanyamba upper neck", new Vector3(0, -.03f, .62f), new Vector3(0, .46f, .8f), .27f, coat);
                Part("Inkanyamba horse head", PrimitiveType.Sphere, new Vector3(0, .57f, .96f), new Vector3(.4f, .43f, .61f), coat);
                Part("Inkanyamba horse muzzle", PrimitiveType.Sphere, new Vector3(0, .44f, 1.2f),
                    new Vector3(.34f, .27f, .34f), accent);
                Part("Inkanyamba mane", PrimitiveType.Capsule, new Vector3(0, .26f, .59f),
                    new Vector3(.16f, .4f, .2f), accent, new Vector3(18, 0, 0));
                for (int side = -1; side <= 1; side += 2)
                    Part("Inkanyamba horse ear", PrimitiveType.Capsule, new Vector3(side * .13f, .85f, .82f),
                        new Vector3(.09f, .16f, .09f), coat, new Vector3(0, 0, -side * 12));
                Eyes("Inkanyamba", new Vector3(0, .66f, 1.1f), .18f, .07f);
                return torso;
            }
            public MeshRenderer FootEyedGiant()
            {
                var torso = Part("Aigamuxa lanky torso", PrimitiveType.Sphere, new Vector3(0, .05f, 0),
                    new Vector3(.54f, .72f, .39f), coat);
                Part("Aigamuxa eyeless head", PrimitiveType.Sphere, new Vector3(0, .67f, .015f),
                    new Vector3(.38f, .45f, .37f), coat);
                Part("Aigamuxa mouth", PrimitiveType.Cube, new Vector3(0, .57f, .199f), new Vector3(.14f, .035f, .025f), detail);
                for (int side = -1; side <= 1; side += 2)
                {
                    Branch("Aigamuxa thigh", new Vector3(side * .15f, -.22f, 0), new Vector3(side * .22f, -.57f, -.08f), .15f, coat);
                    Branch("Aigamuxa shin", new Vector3(side * .22f, -.57f, -.08f), new Vector3(side * .28f, -.88f, .01f), .12f, coat);
                    Part("Aigamuxa long foot", PrimitiveType.Sphere, new Vector3(side * .28f, -.91f, .19f),
                        new Vector3(.27f, .18f, .57f), coat);
                    // Eyes sit on the upturned front of each sole so they remain visible above the ground.
                    Part("Aigamuxa foot eye", PrimitiveType.Sphere, new Vector3(side * .28f, -.92f, .455f),
                        new Vector3(.13f, .12f, .06f), accent);
                    Part("Aigamuxa foot pupil", PrimitiveType.Sphere, new Vector3(side * .28f, -.92f, .486f),
                        new Vector3(.065f, .075f, .025f), detail);
                    Branch("Aigamuxa upper arm", new Vector3(side * .22f, .27f, 0), new Vector3(side * .43f, -.06f, .015f), .12f, coat);
                    Branch("Aigamuxa forearm", new Vector3(side * .43f, -.06f, .015f), new Vector3(side * .55f, -.45f, .07f), .095f, coat);
                    Part("Aigamuxa hand", PrimitiveType.Sphere, new Vector3(side * .56f, -.51f, .075f),
                        new Vector3(.16f, .22f, .15f), coat);
                }
                return torso;
            }
            public MeshRenderer Manticore()
            {
                var torso = LionBody("Manticore");
                Part("Manticore mane", PrimitiveType.Sphere, new Vector3(0, .04f, .48f),
                    new Vector3(.85f, .86f, .63f), accent);
                Part("Manticore human face", PrimitiveType.Sphere, new Vector3(0, .08f, .79f),
                    new Vector3(.53f, .65f, .3f), coat);
                Part("Manticore human nose", PrimitiveType.Sphere, new Vector3(0, .09f, .965f),
                    new Vector3(.11f, .21f, .14f), accent);
                Part("Manticore mouth", PrimitiveType.Cube, new Vector3(0, -.1f, .938f), new Vector3(.24f, .045f, .035f), detail);
                Eyes("Manticore", new Vector3(0, .22f, .925f), .15f, .08f);
                for (int side = -1; side <= 1; side += 2)
                    Part("Manticore brow", PrimitiveType.Cube, new Vector3(side * .15f, .29f, .923f),
                        new Vector3(.18f, .05f, .04f), accent, new Vector3(0, 0, side * 12));
                Tail("Manticore scorpion tail", new[] { new Vector3(0, -.25f, -.71f), new Vector3(.12f, -.05f, -1.05f),
                    new Vector3(.22f, .37f, -1.18f), new Vector3(.2f, .76f, -.97f),
                    new Vector3(.13f, .85f, -.62f) }, .23f, coat);
                Part("Manticore venom bulb", PrimitiveType.Sphere, new Vector3(.13f, .83f, -.61f), Vector3.one * .24f, accent);
                Branch("Manticore scorpion stinger", new Vector3(.13f, .81f, -.56f), new Vector3(.1f, .56f, -.36f), .065f, detail);
                return torso;
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
            public MeshRenderer Horse(bool horn = true)
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
                if (horn)
                {
                    Branch("Unicorn horn base", new Vector3(0, .87f, .57f), new Vector3(0, 1.14f, .7f), .095f, accent);
                    Branch("Unicorn horn tip", new Vector3(0, 1.14f, .7f), new Vector3(0, 1.36f, .81f), .045f, accent);
                }
                return torso;
            }
            public MeshRenderer CatSerpent()
            {
                var torso = Part("Serpent torso", PrimitiveType.Sphere, new Vector3(0, -.44f, .18f),
                    new Vector3(.85f, .72f, .78f), coat);
                for (int segment = 0; segment < 4; segment++)
                {
                    float taper = 1f - segment * .18f;
                    Part("Serpent tail segment " + segment, PrimitiveType.Sphere,
                        new Vector3(segment * .07f, -.5f, -.2f - segment * .25f),
                        new Vector3(.75f * taper, .65f * taper, .58f), coat);
                }
                Part("Cat head", PrimitiveType.Sphere, new Vector3(0, -.12f, .62f), new Vector3(.76f, .65f, .58f), coat);
                Part("Cat muzzle", PrimitiveType.Sphere, new Vector3(0, -.26f, .9f), new Vector3(.48f, .27f, .23f), accent);
                Part("Cat nose", PrimitiveType.Sphere, new Vector3(0, -.18f, 1.015f), new Vector3(.13f, .09f, .06f), detail);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Cat ear", PrimitiveType.Sphere, new Vector3(side * .25f, .18f, .55f),
                        new Vector3(.2f, .42f, .17f), coat, new Vector3(0, 0, -side * 20));
                    Part("Ear inner", PrimitiveType.Sphere, new Vector3(side * .25f, .2f, .625f),
                        new Vector3(.09f, .23f, .035f), accent, new Vector3(0, 0, -side * 20));
                    Part("Cat eye", PrimitiveType.Sphere, new Vector3(side * .24f, -.035f, .85f),
                        new Vector3(.12f, .12f, .045f), accent);
                    Part("Slit pupil", PrimitiveType.Sphere, new Vector3(side * .24f, -.035f, .875f),
                        new Vector3(.035f, .09f, .025f), detail);
                    Branch("Front leg", new Vector3(side * .28f, -.37f, .34f),
                        new Vector3(side * .39f, -.84f, .4f), .2f, coat);
                    Part("Clawed forepaw", PrimitiveType.Sphere, new Vector3(side * .39f, -.91f, .49f),
                        new Vector3(.31f, .18f, .34f), accent);
                    for (int claw = 0; claw < 3; claw++)
                        Part("Foreclaw", PrimitiveType.Sphere, new Vector3(side * .39f + (claw - 1) * .08f, -.92f, .655f),
                            new Vector3(.045f, .07f, .12f), detail);
                }
                return torso;
            }
            public MeshRenderer DesertWorm()
            {
                MeshRenderer torso = null;
                for (int segment = 0; segment < 8; segment++)
                {
                    float taper = segment < 5 ? 1f : 1f - (segment - 4) * .17f;
                    float z = .65f - segment * .23f;
                    var ring = Part("Worm segment " + segment, PrimitiveType.Sphere,
                        new Vector3(Mathf.Sin(segment * .6f) * .065f, -1f + .46f * taper, z),
                        new Vector3(.88f * taper, .92f * taper, .4f), segment % 2 == 0 ? coat : accent);
                    if (segment == 0) torso = ring;
                }
                Part("Mouth rim", PrimitiveType.Sphere, new Vector3(0, -.54f, .855f), new Vector3(.73f, .78f, .16f), accent);
                Part("Dark circular mouth", PrimitiveType.Sphere, new Vector3(0, -.54f, .935f),
                    new Vector3(.51f, .55f, .035f), detail);
                for (int tooth = 0; tooth < 8; tooth++)
                {
                    float angle = tooth * Mathf.PI * .25f;
                    Part("Mouth tooth", PrimitiveType.Sphere,
                        new Vector3(Mathf.Sin(angle) * .225f, -.54f + Mathf.Cos(angle) * .24f, .962f),
                        new Vector3(.065f, .12f, .045f), accent, new Vector3(0, 0, -tooth * 45));
                }
                return torso;
            }
            public MeshRenderer RiverTurtle()
            {
                var torso = Part("River body", PrimitiveType.Sphere, new Vector3(0, -.12f, .02f), new Vector3(.8f, .9f, .62f), coat);
                Part("Turtle shell", PrimitiveType.Sphere, new Vector3(0, -.03f, -.26f), new Vector3(.94f, 1.03f, .48f), accent);
                Part("Head", PrimitiveType.Sphere, new Vector3(0, .57f, .16f), new Vector3(.67f, .58f, .58f), coat);
                Part("Water dish rim", PrimitiveType.Cylinder, new Vector3(0, .86f, .14f), new Vector3(.48f, .04f, .42f), accent);
                Part("Water dish", PrimitiveType.Cylinder, new Vector3(0, .905f, .14f), new Vector3(.35f, .008f, .3f), detail);
                Part("Beak", PrimitiveType.Sphere, new Vector3(0, .46f, .5f), new Vector3(.38f, .19f, .32f), accent);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Eye", PrimitiveType.Sphere, new Vector3(side * .22f, .63f, .39f), new Vector3(.105f, .1f, .07f), detail);
                    Branch("Arm", new Vector3(side * .33f, .13f, .06f), new Vector3(side * .6f, -.33f, .18f), .19f, coat);
                    Part("Webbed hand", PrimitiveType.Sphere, new Vector3(side * .61f, -.37f, .25f), new Vector3(.3f, .1f, .3f), coat);
                    Branch("Leg", new Vector3(side * .22f, -.41f, .01f), new Vector3(side * .28f, -.85f, .13f), .22f, coat);
                    Part("Webbed foot", PrimitiveType.Sphere, new Vector3(side * .29f, -.9f, .23f), new Vector3(.33f, .14f, .5f), coat);
                }
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
