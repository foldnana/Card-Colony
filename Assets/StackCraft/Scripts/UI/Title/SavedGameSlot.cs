using System.Text;
using UnityEngine;
using TMPro;

namespace CryingSnow.StackCraft
{
    public class SavedGameSlot : MonoBehaviour
    {
        [SerializeField, Tooltip("UI label displaying the slot number, scene name, progress, and last saved time.")]
        private TextMeshProUGUI labelText;

        [SerializeField, Tooltip("Button used to load the saved game stored in this slot.")]
        private TextButton loadButton;

        [SerializeField, Tooltip("Button used to delete this saved game after user confirmation.")]
        private TextButton deleteButton;

        private GameData data;

        public void Initialize(GameData data, ModalWindow modalWindow, SavedGamesUI parentUI)
        {
            this.data = data;

            var sb = new StringBuilder();
            sb.Append($"[存档 {data.SlotNumber:D3}] {ChineseLocalization.Text(data.CurrentScene)}");
            if (data.TryGetScene(out var sceneData))
            {
                sb.Append($" ({sceneData.QuestProgress}%)");
            }
            sb.Append($"\n最后保存：{data.LastSaved}");
            labelText.text = sb.ToString();

            loadButton.SetOnClick(() =>
            {
                GameDirector.Instance.LoadGame(data);
                parentUI.Close();
            });

            deleteButton.SetOnClick(() =>
                modalWindow.Show(
                    "删除存档",
                    $"确定要删除存档 {data.SlotNumber} 吗？" +
                    "\n此操作永久生效，无法撤销。",
                    DeleteSavedGame
                )
            );
        }

        public void DeleteSavedGame()
        {
            GameDirector.Instance?.DeleteGame(data);
            Destroy(gameObject);
        }
    }
}
