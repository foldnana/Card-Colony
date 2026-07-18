using UnityEngine;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Defines a component that can be "clicked" for a special action,
    /// rather than just picked up.
    /// </summary>
    public interface IClickable
    {
        /// <summary>
        /// Called by CardController when a click is registered.
        /// </summary>
        /// <param name="clickPosition">The world position where the drag started.</param>
        /// <returns>True if the click was handled, false otherwise.</returns>
        bool OnClick(Vector3 clickPosition);
    }
}
