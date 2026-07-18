namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Defines a component that can interact with a card stack
    /// being dropped onto it.
    /// </summary>
    public interface IOnStackable
    {
        /// <summary>
        /// Called when a stack is dropped on this card's stack.
        /// </summary>
        /// <param name="droppedStack">The stack being dropped.</param>
        /// <returns>True if the interaction was handled (and consumed the stack).</returns>
        bool OnStack(CardStack droppedStack);
    }
}
