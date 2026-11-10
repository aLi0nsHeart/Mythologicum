using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Refreshes an ID-bound label when the player changes language, including hidden panels.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string id;
        private object[] arguments = Array.Empty<object>();
        private Text target;
        private string lastValue;
        public string ID => id;
        public void Set(string key, object[] values)
        { id = key; arguments = values ?? Array.Empty<object>(); Apply(); }
        private void Awake() => target = GetComponent<Text>();
        private void OnEnable()
        {
            Loc.Changed += LanguageChanged;
            if (!IsUserInput() && !string.IsNullOrEmpty(id) && (lastValue == null || target == null || target.text == lastValue)) Apply();
        }
        private void OnDisable() => Loc.Changed -= LanguageChanged;
        private void LanguageChanged()
        {
            // A model-driven label may have replaced its initial placeholder since it was built.
            // Do not restore stale text over the model's newer content.
            if (IsUserInput() || (target != null && target.text != lastValue)) return;
            Apply();
        }
        private bool IsUserInput()
        {
            if (target == null) target = GetComponent<Text>();
            var input = target.GetComponentInParent<InputField>(true);
            return input != null && input.textComponent == target;
        }
        private void Apply()
        {
            if (target == null) target = GetComponent<Text>();
            if (string.IsNullOrEmpty(id)) return;
            lastValue = arguments.Length == 0 ? Loc.Text(id) : Loc.Format(id, arguments);
            target.text = lastValue;
        }
    }
}
