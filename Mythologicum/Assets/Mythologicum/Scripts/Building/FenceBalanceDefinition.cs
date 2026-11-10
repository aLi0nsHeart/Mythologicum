using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class FenceTypeRules
    {
        [Tooltip("Full health of one fence segment. Changes apply to newly built segments and the repair limit.")]
        [Min(1f)] public float maximumHealth = 120f;
        [Tooltip("Allows a creature with a climbing ability to cross this fence. Closed gates remain unclimbable.")]
        public bool allowsClimbing;
        [Tooltip("Stops a spirit's phasing ability, including when this fence contains a staff gate.")]
        public bool blocksPhasing;
        [Tooltip("Creature attacks cannot damage this fence, including its gates. The player may still demolish it.")]
        public bool resistsCreatureDamage;

        public void Validate()
        {
            maximumHealth = float.IsNaN(maximumHealth) || float.IsInfinity(maximumHealth) ? 1f : Math.Max(1f, maximumHealth);
        }
    }

    /// <summary>Plain rules can be injected into the model without accessing Unity resources.</summary>
    [Serializable]
    public sealed class FenceRules
    {
        public FenceTypeRules wooden = new FenceTypeRules { maximumHealth = 120f, allowsClimbing = true };
        public FenceTypeRules spiked = new FenceTypeRules { maximumHealth = 220f };
        public FenceTypeRules heavy = new FenceTypeRules { maximumHealth = 800f, resistsCreatureDamage = true };
        public FenceTypeRules sanctified = new FenceTypeRules { maximumHealth = 300f, blocksPhasing = true };

        public FenceTypeRules For(FenceKind kind) => kind == FenceKind.Heavy ? heavy : kind == FenceKind.Sanctified ? sanctified
            : kind == FenceKind.Spiked ? spiked : wooden;

        public void Validate()
        {
            if (wooden == null) wooden = new FenceTypeRules { maximumHealth = 120f, allowsClimbing = true };
            if (spiked == null) spiked = new FenceTypeRules { maximumHealth = 220f };
            if (heavy == null) heavy = new FenceTypeRules { maximumHealth = 800f, resistsCreatureDamage = true };
            if (sanctified == null) sanctified = new FenceTypeRules { maximumHealth = 300f, blocksPhasing = true };
            wooden.Validate(); spiked.Validate(); heavy.Validate(); sanctified.Validate();
        }
    }

    [CreateAssetMenu(menuName = "Mythologicum/Buildings/Fence balance", fileName = "DefaultFences")]
    public sealed class FenceBalanceDefinition : ScriptableObject
    {
        private static FenceBalanceDefinition current;
        public static FenceBalanceDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<FenceBalanceDefinition>("Fences/DefaultFences");
                if (current == null)
                {
                    current = CreateInstance<FenceBalanceDefinition>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }

        public FenceRules rules = new FenceRules();
        private void OnValidate() { if (rules == null) rules = new FenceRules(); rules.Validate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}
