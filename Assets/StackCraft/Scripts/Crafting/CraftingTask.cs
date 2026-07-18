namespace CryingSnow.StackCraft
{
    public class CraftingTask
    {
        public RecipeDefinition Recipe { get; private set; }
        public CardStack TargetStack { get; private set; }
        public float Progress { get; private set; }
        public bool IsCanceled { get; private set; }
        public bool IsPaused { get; private set; }

        public bool IsComplete => Progress >= Recipe.CraftingDuration;

        public CraftingTask(RecipeDefinition recipe, CardStack targetStack)
        {
            Recipe = recipe;
            TargetStack = targetStack;
            Progress = 0f;
        }

        public void UpdateProgress(float deltaTime)
        {
            // Only update progress if not paused, canceled, or complete.
            if (!IsComplete && !IsCanceled && !IsPaused)
            {
                Progress += deltaTime;
            }
        }

        public void SetProgress(float value)
        {
            Progress = value;
        }

        public void Cancel()
        {
            IsCanceled = true;
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public void Complete()
        {
            Progress = Recipe.CraftingDuration;
        }
    }
}
