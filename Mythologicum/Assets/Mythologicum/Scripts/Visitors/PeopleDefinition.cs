using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/People catalog", fileName = "People")]
    public sealed class PeopleDefinition : ScriptableObject
    {
        [Tooltip("Mixed visitor pool retained for general visitors restored from existing parks.")]
        public GameObject[] civilians = new GameObject[0];
        [Header("Visitor age groups")]
        [Tooltip("Models used by the adult visitor types. An empty pool uses their configured placeholder body.")]
        public GameObject[] adultVisitors = new GameObject[0];
        [Tooltip("Models used only by Children. An empty pool uses the child placeholder, never an adult model.")]
        public GameObject[] childVisitors = new GameObject[0];
        public GameObject[] VisitorModels(bool children) => (children ? childVisitors : adultVisitors) ?? System.Array.Empty<GameObject>();
        [Header("Park staff")]
        public GameObject police;
        public GameObject doctor;
        public GameObject worker;
    }
}
