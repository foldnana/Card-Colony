using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    public class ProgressUI : MonoBehaviour
    {
        [SerializeField, Tooltip("The Image component (filled Image) that visualizes the normalized progress of the current crafting task.")]
        private Image progressFill;

        [SerializeField, Tooltip("The local offset added to the target card stack's position to correctly place the progress bar UI in 3D space.")]
        private Vector3 displayOffset = new Vector3(0f, 0f, 0.55f);

        public Vector3 DisplayOffset => displayOffset;

        public void UpdateUI(CraftingTask task)
        {
            transform.position = task.TargetStack.TargetPosition + displayOffset;
            float normalizedProgress = task.Progress / task.Recipe.CraftingDuration;
            progressFill.fillAmount = normalizedProgress;
        }
    }
}
