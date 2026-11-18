using UnityEngine;

namespace FourCorners
{
    public sealed class ArenaLayout : MonoBehaviour
    {
        public Transform PlayerSpawn;
        public Transform TruckSpawn;
        public Transform[] Loop;
        public Transform[] Shortcut;
        public Transform[] Ramps = System.Array.Empty<Transform>();
        public BreakableFence Fence;
    }
}
