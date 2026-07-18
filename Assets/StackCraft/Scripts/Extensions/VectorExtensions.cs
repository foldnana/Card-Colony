using UnityEngine;

namespace CryingSnow.StackCraft
{
    public static class VectorExtensions
    {
        /// <summary>
        /// Returns a new Vector3 with the Y component set to 0.
        /// Useful for snapping positions to the ground plane.
        /// </summary>
        public static Vector3 Flatten(this Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
