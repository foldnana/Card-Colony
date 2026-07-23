using UnityEngine;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Marks cards created by a location's per-entry random spawn rules.
    /// The marker is persisted through <see cref="CardData"/> so returning to
    /// a wilderness location can replace only its generated contents.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocationRandomSpawnMarker : MonoBehaviour
    {
    }
}
