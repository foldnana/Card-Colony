using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class SavedGamesUI : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab used to create a saved-game slot entry in the list.")]
        private SavedGameSlot slotPrefab;

        [SerializeField, Tooltip("Parent container that holds all generated saved-game slot entries.")]
        private RectTransform contentRect;

        [SerializeField, Tooltip("Button that deletes all saved games after user confirmation.")]
        private TextButton clearButton;

        [SerializeField, Tooltip("Button that closes the Saved Games UI panel.")]
        private TextButton closeButton;

        [SerializeField, Tooltip("Modal confirmation window used for delete actions.")]
        private ModalWindow modalWindow;

        private List<SavedGameSlot> slots = new();

        public void Open() => gameObject.SetActive(true);
        public void Close() => gameObject.SetActive(false);

        private void Start()
        {
            foreach (var savedGame in GameDirector.Instance.SavedGames.Values)
            {
                var slot = Instantiate(slotPrefab, contentRect);
                slot.Initialize(savedGame, modalWindow, this);
                slots.Add(slot);
            }

            clearButton.SetOnClick(() =>
                modalWindow.Show(
                    "删除全部存档",
                    "确定要删除所有存档吗？" +
                    "\n此操作永久生效，无法撤销。",
                    ClearSavedGames
                )
            );

            closeButton.SetOnClick(Close);
        }

        private void ClearSavedGames()
        {
            slots.RemoveAll(slot => slot == null);
            slots.ForEach(slot => slot.DeleteSavedGame());
            slots.Clear();
        }
    }
}
