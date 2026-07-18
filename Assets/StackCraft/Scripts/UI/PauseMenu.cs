using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PauseMenu : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField, Tooltip("The button used to close the pause menu and resume the game.")]
        private TextButton continueButton;

        [SerializeField, Tooltip("The button used to open the Game Options menu.")]
        private TextButton optionsButton;

        [SerializeField, Tooltip("The button used to return to the main Title Screen.")]
        private TextButton titleButton;

        [Header("Screens")]
        [SerializeField, Tooltip("Reference to the Game Options UI panel that is opened when the 'Options' button is clicked.")]
        private GameOptionsUI gameOptionsUI;

        private CanvasGroup canvasGroup;
        private bool isActive = false;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            continueButton.SetOnClick(ToggleActiveState);
            optionsButton.SetOnClick(gameOptionsUI.Open);
            titleButton.SetOnClick(GameDirector.Instance.BackToTitle);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !DayCycleManager.Instance.IsEndingCycle)
            {
                ToggleActiveState();
            }
        }

        private void ToggleActiveState()
        {
            isActive = !isActive;
            canvasGroup.alpha = isActive ? 1f : 0f;
            canvasGroup.blocksRaycasts = isActive;

            TimeManager.Instance.SetExternalPause(isActive);
        }
    }
}
