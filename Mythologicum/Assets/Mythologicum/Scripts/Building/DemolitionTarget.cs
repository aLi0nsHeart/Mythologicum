using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Common demolition entry point for constructed objects, independent of their type.</summary>
    public sealed class DemolitionTarget : MonoBehaviour
    {
        private Action onRemoved;
        private bool removed;

        public void Initialize(Action onRemoved) => this.onRemoved = onRemoved;

        public bool Demolish()
        {
            if (removed) return false;
            removed = true;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            onRemoved?.Invoke();
            Destroy(gameObject);
            return true;
        }
    }
}
