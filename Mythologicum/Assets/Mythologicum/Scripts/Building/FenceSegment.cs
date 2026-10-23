using UnityEngine;

namespace Mythologicum
{
    public sealed class FenceSegment : MonoBehaviour
    {
        public FenceEdge Edge { get; private set; }
        public void Initialize(FenceEdge edge) => Edge = edge;
    }
}
