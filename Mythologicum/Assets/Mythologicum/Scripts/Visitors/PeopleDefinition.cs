using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/People catalog", fileName = "People")]
    public sealed class PeopleDefinition : ScriptableObject
    {
        [Tooltip("Only these models can be selected for ordinary park visitors.")]
        public GameObject[] civilians = new GameObject[0];
        [Header("Reserved for future park staff")]
        public GameObject police;
        public GameObject doctor;
        public GameObject worker;
    }
}