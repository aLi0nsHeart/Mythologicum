using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Persistent construction and service balance, shared by new and loaded parks.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Buildings/Building catalogue", fileName = "DefaultBuildings")]
    public sealed class BuildingCatalogDefinition : ScriptableObject
    {
        private static BuildingCatalogDefinition current;
        public static BuildingCatalogDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<BuildingCatalogDefinition>("Buildings/DefaultBuildings");
                if (current == null)
                {
                    current = CreateInstance<BuildingCatalogDefinition>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }

        [Tooltip("One entry per building kind. Costs are in park currency; time/rates use simulation seconds. Changes to default prices apply to newly placed buildings; placed buildings keep their player-set price.")]
        public BuildingDefinition[] buildings = BuildingDefinition.Defaults();

        /// <summary>Visual binding and runtime use must not modify the source asset.</summary>
        public BuildingDefinition[] CreateRuntimeCatalog()
        {
            var result = new List<BuildingDefinition>();
            var kinds = new HashSet<BuildingKind>();
            foreach (BuildingDefinition definition in buildings ?? Array.Empty<BuildingDefinition>())
            {
                if (definition == null || !kinds.Add(definition.kind)) continue;
                BuildingDefinition copy = definition.Copy();
                copy.Validate();
                result.Add(copy);
            }
            return result.ToArray();
        }

        private void OnValidate()
        {
            foreach (BuildingDefinition definition in buildings ?? Array.Empty<BuildingDefinition>()) definition?.Validate();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}
