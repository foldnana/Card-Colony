using System;

namespace CryingSnow.StackCraft
{
    public sealed class MarketSubmitGate
    {
        private readonly float cooldownSeconds;
        private bool hasSubmission;
        private float nextAllowedTime;

        public MarketSubmitGate(float cooldownSeconds)
        {
            this.cooldownSeconds = Math.Max(
                0f,
                cooldownSeconds);
        }

        public bool TryEnter(float currentTime)
        {
            if (hasSubmission && currentTime < nextAllowedTime)
                return false;

            hasSubmission = true;
            nextAllowedTime = currentTime + cooldownSeconds;
            return true;
        }

        public void Reset()
        {
            hasSubmission = false;
            nextAllowedTime = 0f;
        }
    }
}
