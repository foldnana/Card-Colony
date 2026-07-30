using UnityEngine;

namespace CryingSnow.StackCraft
{
    public static class LocationRandomRefreshPolicy
    {
        public static bool ShouldRefresh(
            bool randomizationEnabled,
            bool wasLoaded,
            LocationTransitionReason transitionReason,
            int worldDay,
            int lastRefreshDay)
        {
            if (!randomizationEnabled)
                return false;
            if (!wasLoaded)
                return true;
            if (transitionReason == LocationTransitionReason.ReturnToParent)
                return false;

            return transitionReason == LocationTransitionReason.WorldMapEntry &&
                Mathf.Max(1, worldDay) > lastRefreshDay;
        }
    }
}
