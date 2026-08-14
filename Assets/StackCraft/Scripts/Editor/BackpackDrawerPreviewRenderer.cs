#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class BackpackDrawerPreviewRenderer
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";

        [MenuItem("Tools/StackCraft/Render Backpack Sidebar Preview")]
        public static void Render()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            GameObject instance = Object.Instantiate(prefab);
            var cameraObject = new GameObject("BackpackPreviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            RenderTexture target = null;
            Texture2D frame = null;
            Texture2D crop = null;

            try
            {
                Transform drawer = Find(instance.transform, "BackpackTablePanel");
                Canvas canvas = drawer.GetComponentInParent<Canvas>(true);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;

                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.11f, 0.14f, 0.17f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;

                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;

                Transform menu = Find(instance.transform, "MenuPanel");
                Transform publicMarket = Find(instance.transform, "PublicMarketModal");
                if (publicMarket != null)
                    publicMarket.gameObject.SetActive(false);
                SetLayerRecursively(instance.transform, 31);
                canvas.gameObject.layer = 30;
                SetLayerRecursively(menu, 30);
                camera.cullingMask = 1 << 30;
                menu.gameObject.SetActive(true);
                foreach (Transform child in menu)
                {
                    child.gameObject.SetActive(
                        child.name == "Header" ||
                        child == drawer ||
                        child.name.Contains("Fold"));
                }
                drawer.gameObject.SetActive(true);
                Transform marker = Find(instance.transform, "BackpackSidebarPageV6");
                if (marker != null)
                    marker.gameObject.SetActive(true);
                foreach (Button button in drawer.GetComponentsInChildren<Button>(true))
                {
                    if (button.targetGraphic != null)
                        button.targetGraphic.color = button.colors.normalColor;
                }

                PopulateItemPreview(instance);

                // Rebuild refreshes the selected-character panel, so apply the
                // preview portrait after the sample backpack has been populated.
                CardDefinition villager = AssetDatabase.LoadAssetAtPath<CardDefinition>(
                    "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
                Transform portrait = Find(drawer, "BackpackCharacterPortraitFrame");
                if (villager != null && portrait != null)
                {
                    RawImage image = portrait.GetComponentInChildren<RawImage>(true);
                    if (image != null)
                    {
                        image.texture = villager.ArtTexture;
                        image.color = Color.white;
                        image.enabled = image.texture != null;
                    }
                }

                SetText(drawer, "BackpackWeightText", "重量");
                SetText(drawer, "BackpackWeightValueText", "8/20");
                SetText(drawer, "BackpackCharacterName", "旅行者");
                SetText(drawer, "BackpackCharacterHealth", "生命15/15");
                SetText(drawer, "BackpackSelectedName", "短剑");
                SetText(drawer, "BackpackSelectedType", "武器 · 攻击 +3");
                SetText(drawer, "BackpackSelectedDescription", "比当前木棍多 2 点攻击。拖到场地取出，或点击按钮替换装备。");
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                frame = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                frame.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
                frame.Apply();

                const int cropWidth = 420;
                crop = new Texture2D(cropWidth, 1080, TextureFormat.RGBA32, false);
                crop.SetPixels(frame.GetPixels(1920 - cropWidth, 0, cropWidth, 1080));
                crop.Apply();

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string outputPath = Path.Combine(
                    projectRoot,
                    "Library",
                    "BackpackLayoutPreview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                string fullOutputPath = Path.Combine(
                    projectRoot,
                    "Library",
                    "BackpackLayoutPreviewFull.png");
                File.WriteAllBytes(fullOutputPath, frame.EncodeToPNG());
                File.WriteAllBytes(outputPath, crop.EncodeToPNG());
                Debug.Log($"Backpack preview rendered to: {outputPath}");
            }
            finally
            {
                RenderTexture.active = null;
                if (target != null)
                    Object.DestroyImmediate(target);
                if (frame != null)
                    Object.DestroyImmediate(frame);
                if (crop != null)
                    Object.DestroyImmediate(crop);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(instance);
            }
        }

        private static void PopulateItemPreview(GameObject instance)
        {
            BackpackView owner = instance.GetComponentInChildren<BackpackView>(true);
            string[] paths =
            {
                "Assets/StackCraft/Resources/Cards/Equipments/Card_Sword.asset",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Berry.asset",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Stone.asset",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Medicine.asset",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Wood.asset",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Rope.asset",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Turnip.asset"
            };
            var backpack = new BackpackData
            {
                SlotCapacity = 9,
                MaximumCarryWeight = 20f
            };
            for (int index = 0; index < paths.Length; index++)
            {
                CardDefinition definition = AssetDatabase.LoadAssetAtPath<CardDefinition>(paths[index]);
                if (definition == null)
                    continue;

                backpack.Entries.Add(new BackpackEntryData
                {
                    InstanceId = $"preview-{index}",
                    Card = new CardData { Id = definition.Id },
                    SlotIndex = index
                });
            }
            owner.Rebuild(backpack);
        }

        private static void SetText(Transform root, string name, string value)
        {
            Transform target = Find(root, name);
            if (target != null && target.TryGetComponent(out TMP_Text text))
            {
                text.text = value;
                text.ForceMeshUpdate();
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
#endif
