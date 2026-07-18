using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class MenuView : MonoBehaviour
    {
        [SerializeField, Tooltip("The parent transform for all list items.")]
        private RectTransform content;

        [SerializeField, Tooltip("Color for the headers (quest group or recipe category).")]
        protected Color headerColor = new Color(0.3f, 0.8f, 1.0f, 1.0f);

        protected const string SYMBOL_COLLAPSED = "\u25ba";
        protected const string SYMBOL_EXPANDED = "\u25bc";
        protected const string SYMBOL_BULLET = "\u2022";
        protected const string INDICATOR_NEW = " <color=red>\u25cf</color>";

        private CanvasGroup canvasGroup;

        private object infoRequesterKey;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            infoRequesterKey = this;
        }

        /// <summary>
        /// Toggles the visibility and interactivity of this menu.
        /// </summary>
        /// <param name="show">True to show, false to hide.</param>
        public void ToggleView(bool show)
        {
            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;

            if (show) AudioManager.Instance?.PlaySFX(AudioId.Click);
        }

        /// <summary>
        /// Handles the logic for showing or hiding the info panel based on hover state.
        /// </summary>
        /// <param name="show">True if the mouse is entering the item, false if exiting.</param>
        /// <param name="item">The data object (e.g., QuestInstance, RecipeDefinition) associated with the item.</param>
        protected void ToggleInfoPanel(bool show, object item)
        {
            if (show && item != null)
            {
                InfoPanel.Instance?.RequestInfoDisplay(
                    infoRequesterKey,
                    InfoPriority.Hover,
                    GetItemInfo(item) // Get the specific info from the derived class implementation
                );
            }
            else
            {
                InfoPanel.Instance?.ClearInfoRequest(infoRequesterKey);
            }
        }

        /// <summary>
        /// Creates and configures a standard <see cref="TextButton"/> for an item in the list.
        /// </summary>
        /// <param name="displayText">The text to display on the button.</param>
        /// <param name="itemData">The data object to associate with this button's hover event.</param>
        /// <param name="fontSize">The font size for the button text.</param>
        /// <returns>The newly created <see cref="TextButton"/> component.</returns>
        protected TextButton CreateItemButton(string displayText, object itemData, float fontSize = 30f)
        {
            GameObject go = new GameObject($"ItemButton ({displayText})");
            go.transform.SetParent(content, false);

            var itemBtn = go.AddComponent<TextButton>();

            string itemId = GetItemId(itemData);

            bool isNew = false;
            if (!string.IsNullOrEmpty(itemId) && GameDirector.Instance?.GameData != null)
            {
                isNew = !GameDirector.Instance.GameData.SeenItems.Contains(itemId);
            }

            string initialText = isNew ? displayText + INDICATOR_NEW : displayText;

            itemBtn.Setup(
                initialText,
                fontSize: fontSize,
                onHover: (enter) =>
                {
                    ToggleInfoPanel(enter, itemData);

                    if (enter && isNew)
                    {
                        if (GameDirector.Instance?.GameData != null)
                        {
                            GameDirector.Instance.GameData.SeenItems.Add(itemId);
                        }

                        isNew = false;

                        string currentText = itemBtn.GetText();
                        itemBtn.SetText(currentText.Replace(INDICATOR_NEW, ""));
                    }
                }
            );

            return itemBtn;
        }

        /// <summary>
        /// When implemented in a derived class, gets the formatted header and body text
        /// for a specific item to be displayed in the info panel.
        /// </summary>
        /// <param name="item">The data item (e.g., QuestInstance, RecipeDefinition).</param>
        /// <returns>A tuple containing the header and body text for the info panel.</returns>
        protected abstract (string header, string body) GetItemInfo(object item);

        /// <summary>
        /// Derived classes must implement this to extract a unique ID from the item data.
        /// </summary>
        /// <param name="item">The data object (e.g., RecipeDefinition, QuestInstance).</param>
        /// <returns>The unique string ID of the item, or null/empty if the item is just a header.</returns>
        protected abstract string GetItemId(object item);
    }
}
