using System;
using CardColony.Gameplay;
using CardColony.Inventory;

namespace CardColony.Presentation
{
    public sealed class BackpackInteraction
    {
        private readonly PlayableLoopSession session;

        public ItemCardStack HeldCard => session.HeldCard;

        public BackpackInteraction(PlayableLoopSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool TryTake(string cardInstanceId, int quantity)
        {
            return session.TryTakeCard(cardInstanceId, quantity);
        }

        public bool TrySplitOne(string cardInstanceId)
        {
            return session.TrySplitOne(cardInstanceId);
        }

        public bool TryPutBack()
        {
            return session.TryPutHeldCardBack();
        }
    }
}
