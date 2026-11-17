using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Common demolition entry point for constructed objects, independent of their type.</summary>
    public sealed class DemolitionTarget : MonoBehaviour
    {
        private Action onRemoved;
        private bool removed;
        private Func<bool> canRemove;
        public bool CanDemolish => !removed && (canRemove?.Invoke() ?? true);

        public void Initialize(Action onRemoved, Func<bool> canRemove = null) { this.onRemoved = onRemoved; this.canRemove = canRemove; }

        public bool Demolish()
        {
            if (!CanDemolish) return false;
            removed = true;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            onRemoved?.Invoke();
            Destroy(gameObject);
            return true;
        }
    }
}
