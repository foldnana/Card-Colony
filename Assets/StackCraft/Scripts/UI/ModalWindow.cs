using UnityEngine;
using TMPro;

namespace CryingSnow.StackCraft
{
    public class ModalWindow : MonoBehaviour
    {
        [SerializeField, Tooltip("UI text element used to display the modal window title.")]
        private TextMeshProUGUI titleText;

        [SerializeField, Tooltip("UI text element used to display the dialog or confirmation message.")]
        private TextMeshProUGUI dialogText;

        [SerializeField, Tooltip("Button that closes the modal without executing the action.")]
        private TextButton declineButton;

        [SerializeField, Tooltip("Button that confirms the action and triggers the assigned callback.")]
        private TextButton acceptButton;

        private void Awake()
        {
            declineButton.SetOnClick(() => gameObject.SetActive(false));
        }

        /// <summary>
        /// Shows the modal window with the specified title and message, and assigns a callback to run when the Accept button is pressed.
        /// </summary>
        public void Show(string title, string dialog, System.Action onAccept)
        {
            gameObject.SetActive(true);
            titleText.text = title;
            dialogText.text = dialog;
            acceptButton.SetOnClick(() =>
            {
                onAccept?.Invoke();
                gameObject.SetActive(false);
            });
        }
    }
}
