using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class PublicMarketFantasySkinInstaller
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string SpriteRoot =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/";
        private const string PanelSpritePath =
            SpriteRoot + "Frame/PanelFrame_01_Bg.png";
        private const string BorderSpritePath =
            SpriteRoot + "Popup/Popup_01_Border.png";
        private const string RowSpritePath =
            SpriteRoot + "Frame/Listframe_01~02_Bg.png";
        private const string ButtonSpriteRoot =
            SpriteRoot +
            "Button/";
        private const string RedButtonSpritePath =
            ButtonSpriteRoot +
            "Button_Rectangle_01_Convex_Red.Png";
        private const string BlueButtonSpritePath =
            ButtonSpriteRoot +
            "Button_Rectangle_01_Convex_Blue.Png";
        private const string YellowButtonSpritePath =
            ButtonSpriteRoot +
            "Button_Rectangle_01_Convex_Yellow.Png";
        private const string GreenButtonSpritePath =
            ButtonSpriteRoot +
            "Button_Rectangle_01_Convex_Green.Png";
        private const string BrownButtonSpritePath =
            ButtonSpriteRoot +
            "Button_Rectangle_01_Convex_Brown.Png";

        private static readonly Color ModalColor =
            new(0.012f, 0.020f, 0.038f, 0.99f);
        private static readonly Color HeaderColor =
            new(0.025f, 0.13f, 0.23f, 0.99f);
        private static readonly Color BackpackColor =
            new(0.030f, 0.11f, 0.24f, 0.99f);
        private static readonly Color TransactionColor =
            new(0.25f, 0.12f, 0.035f, 0.99f);
        private static readonly Color MarketColor =
            new(0.025f, 0.20f, 0.11f, 0.99f);
        private static readonly Color Cyan =
            new(0.38f, 0.82f, 0.97f, 1f);
        private static readonly Color Gold =
            new(0.98f, 0.75f, 0.30f, 1f);
        private static readonly Color WarmIvory =
            new(0.95f, 0.86f, 0.70f, 1f);
        private static readonly Color SoftSilver =
            new(0.72f, 0.78f, 0.82f, 1f);
        private static readonly Color StockBlue =
            new(0.50f, 0.86f, 1f, 1f);
        private static readonly Color TrendGreen =
            new(0.57f, 0.88f, 0.66f, 1f);

        [MenuItem("Tools/StackCraft/Apply Public Market Fantasy Skin")]
        public static void Apply()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                ApplyToPrefabContents(root);
                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    "Applied the fantasy RPG skin to the public market.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static void ApplyToPrefabContents(GameObject root)
        {
            Transform modal = FindDescendant(
                root?.transform,
                "PublicMarketModal");
            if (modal == null)
                throw new MissingReferenceException(
                    "UIRoot does not contain PublicMarketModal.");

            Sprite panelSprite = LoadRequiredSprite(PanelSpritePath);
            Sprite borderSprite = LoadRequiredSprite(BorderSpritePath);
            Sprite rowSprite = LoadRequiredSprite(RowSpritePath);
            Sprite redButtonSprite =
                LoadRequiredSprite(RedButtonSpritePath);
            Sprite blueButtonSprite =
                LoadRequiredSprite(BlueButtonSpritePath);
            Sprite yellowButtonSprite =
                LoadRequiredSprite(YellowButtonSpritePath);
            Sprite greenButtonSprite =
                LoadRequiredSprite(GreenButtonSpritePath);
            Sprite brownButtonSprite =
                LoadRequiredSprite(BrownButtonSpritePath);

            StyleImage(
                modal.GetComponent<Image>(),
                panelSprite,
                ModalColor);

            Image outerFrame = EnsureOverlay(
                modal,
                "PublicMarketFantasyFrame",
                borderSprite,
                new Color(0.42f, 0.66f, 0.74f, 0.92f),
                new Vector2(0.008f, 0.012f),
                new Vector2(0.992f, 0.992f));
            outerFrame.transform.SetAsFirstSibling();
            EnsureShadow(
                outerFrame.gameObject,
                new Color(0f, 0f, 0.02f, 0.78f),
                new Vector2(0f, -6f));

            Image headerBand = EnsureOverlay(
                modal,
                "PublicMarketFantasyHeader",
                panelSprite,
                HeaderColor,
                new Vector2(0.022f, 0.895f),
                new Vector2(0.978f, 0.985f));
            headerBand.transform.SetSiblingIndex(1);
            EnsureShadow(
                headerBand.gameObject,
                new Color(0f, 0.01f, 0.025f, 0.66f),
                new Vector2(0f, -5f));
            RemoveDirectChild(
                headerBand.transform,
                "PublicMarketFantasyHeaderHighlight");

            StylePanel(
                modal,
                "PublicMarketBackpackPanel",
                panelSprite,
                borderSprite,
                BackpackColor,
                new Color(0.22f, 0.62f, 0.96f, 0.88f));
            StylePanel(
                modal,
                "PublicMarketTransactionPanel",
                panelSprite,
                borderSprite,
                TransactionColor,
                new Color(1f, 0.62f, 0.20f, 0.90f));
            StylePanel(
                modal,
                "PublicMarketMarketPanel",
                panelSprite,
                borderSprite,
                MarketColor,
                new Color(0.25f, 0.90f, 0.55f, 0.88f));

            StyleScrollView(
                modal,
                "PublicMarketBackpackPanelScrollView",
                rowSprite,
                new Color(0.025f, 0.075f, 0.15f, 0.90f));
            StyleScrollView(
                modal,
                "PublicMarketMarketPanelScrollView",
                rowSprite,
                new Color(0.02f, 0.11f, 0.065f, 0.90f));

            StyleRow(
                modal,
                "PublicMarketBackpackRowTemplate",
                brownButtonSprite,
                blueButtonSprite,
                brownButtonSprite);
            StyleRow(
                modal,
                "PublicMarketMarketRowTemplate",
                blueButtonSprite,
                blueButtonSprite,
                brownButtonSprite);

            StyleButton(
                modal,
                "PublicMarketCloseButton",
                redButtonSprite);
            StyleButton(
                modal,
                "PublicMarketDecreaseButton",
                blueButtonSprite);
            StyleButton(
                modal,
                "PublicMarketIncreaseButton",
                blueButtonSprite);
            StyleButton(
                modal,
                "PublicMarketMaximumButton",
                yellowButtonSprite);
            StyleButton(
                modal,
                "PublicMarketConfirmButton",
                greenButtonSprite);

            StyleText(
                modal,
                "PublicMarketTitle",
                Cyan,
                36f);
            StyleText(
                modal,
                "PublicMarketSummary",
                new Color(0.90f, 0.92f, 0.90f, 1f),
                20f);
            StyleText(
                modal,
                "PublicMarketBackpackPanelTitle",
                Cyan,
                27f);
            StyleText(
                modal,
                "PublicMarketBackpackPanelSubtitle",
                WarmIvory,
                17f);
            StyleText(
                modal,
                "PublicMarketTransactionTitle",
                Gold,
                28f);
            StyleText(
                modal,
                "PublicMarketTransactionGuide",
                SoftSilver,
                17f);
            StyleText(
                modal,
                "PublicMarketMarketPanelTitle",
                WarmIvory,
                27f);
            StyleText(
                modal,
                "PublicMarketMarketPanelSubtitle",
                StockBlue,
                17f);
            StyleText(
                modal,
                "PublicMarketQuantityLabel",
                Gold,
                20f);
        }

        private static void StylePanel(
            Transform modal,
            string panelName,
            Sprite panelSprite,
            Sprite borderSprite,
            Color panelColor,
            Color borderColor)
        {
            Transform panel = RequireDescendant(modal, panelName);
            StyleImage(
                panel.GetComponent<Image>(),
                panelSprite,
                panelColor);
            EnsureShadow(
                panel.gameObject,
                new Color(0f, 0.005f, 0.015f, 0.72f),
                new Vector2(8f, -10f));
            Image border = EnsureOverlay(
                panel,
                $"{panelName}FantasyBorder",
                borderSprite,
                borderColor,
                new Vector2(0.008f, 0.006f),
                new Vector2(0.992f, 0.994f));
            border.transform.SetAsFirstSibling();
            RemoveDirectChild(
                panel,
                $"{panelName}TopHighlight");
            RemoveDirectChild(
                panel,
                $"{panelName}BottomShade");
        }

        private static void StyleScrollView(
            Transform modal,
            string name,
            Sprite sprite,
            Color color)
        {
            Transform scroll = RequireDescendant(modal, name);
            StyleImage(scroll.GetComponent<Image>(), sprite, color);
        }

        private static void StyleRow(
            Transform modal,
            string name,
            Sprite defaultSprite,
            Sprite buyButtonSprite,
            Sprite sellButtonSprite)
        {
            Transform row = RequireDescendant(modal, name);
            StyleImage(
                row.GetComponent<Image>(),
                defaultSprite,
                Color.white);
            EnsureShadow(
                row.gameObject,
                new Color(0f, 0.005f, 0.015f, 0.56f),
                new Vector2(2f, -4f));
            RemoveDirectChild(row, "FantasyRowBorder");

            Button button = row.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                button.colors = CreateButtonColors(
                    Color.white,
                    new Color(0.96f, 0.96f, 0.96f, 1f));
            }

            MarketCommodityListItem item =
                row.GetComponent<MarketCommodityListItem>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("buyButtonSprite")
                .objectReferenceValue = buyButtonSprite;
            serialized.FindProperty("sellButtonSprite")
                .objectReferenceValue = sellButtonSprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            StyleRowText(row, "Name", WarmIvory);
            StyleRowText(row, "Price", Gold);
            StyleRowText(row, "Quantity", StockBlue);
            StyleRowText(row, "Details", SoftSilver);
            StyleRowText(row, "Trend", TrendGreen);
        }

        private static void StyleButton(
            Transform modal,
            string name,
            Sprite sprite)
        {
            Transform buttonTransform =
                RequireDescendant(modal, name);
            Image image = buttonTransform.GetComponent<Image>();
            StyleImage(image, sprite, Color.white);
            EnsureShadow(
                buttonTransform.gameObject,
                new Color(0.005f, 0.008f, 0.015f, 0.70f),
                new Vector2(0f, -5f));

            Button button = buttonTransform.GetComponent<Button>();
            if (button == null)
                return;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors(
                Color.white,
                new Color(0.96f, 0.96f, 0.96f, 1f));
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Automatic
            };
        }

        private static Shadow EnsureShadow(
            GameObject target,
            Color color,
            Vector2 distance)
        {
            Shadow shadow = null;
            Shadow[] shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i].GetType() == typeof(Shadow))
                {
                    shadow = shadows[i];
                    break;
                }
            }

            if (shadow == null)
                shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        private static ColorBlock CreateButtonColors(
            Color normalMultiplier,
            Color highlightedMultiplier)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normalMultiplier;
            colors.highlightedColor = highlightedMultiplier;
            colors.pressedColor =
                new Color(0.74f, 0.78f, 0.80f, 1f);
            colors.selectedColor = highlightedMultiplier;
            colors.disabledColor =
                new Color(0.36f, 0.38f, 0.40f, 0.62f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static void StyleRowText(
            Transform row,
            string name,
            Color color)
        {
            TMP_Text label =
                RequireDescendant(row, name).GetComponent<TMP_Text>();
            label.color = color;
        }

        private static void StyleText(
            Transform modal,
            string name,
            Color color,
            float size)
        {
            TMP_Text label =
                RequireDescendant(modal, name).GetComponent<TMP_Text>();
            label.color = color;
            label.fontSize = size;
        }

        private static Image EnsureOverlay(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Transform existing = FindDirectChild(parent, name);
            GameObject overlay;
            if (existing == null)
            {
                overlay = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                overlay.layer = LayerMask.NameToLayer("UI");
                overlay.transform.SetParent(parent, false);
            }
            else
            {
                overlay = existing.gameObject;
            }

            RectTransform rect =
                (RectTransform)overlay.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = overlay.GetComponent<Image>();
            StyleImage(image, sprite, color);
            image.raycastTarget = false;
            return image;
        }

        private static void StyleImage(
            Image image,
            Sprite sprite,
            Color color)
        {
            if (image == null)
                throw new MissingComponentException(
                    "Fantasy market skin requires an Image component.");
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static Sprite LoadRequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new MissingReferenceException(
                    $"Fantasy RPG UI sprite is missing: {path}");
            return sprite;
        }

        private static Transform RequireDescendant(
            Transform root,
            string name)
        {
            Transform result = FindDescendant(root, name);
            if (result == null)
                throw new MissingReferenceException(
                    $"Public market UI element is missing: {name}");
            return result;
        }

        private static Transform FindDescendant(
            Transform root,
            string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(
                    root.GetChild(i),
                    name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Transform FindDirectChild(
            Transform root,
            string name)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                    return child;
            }
            return null;
        }

        private static void RemoveDirectChild(
            Transform root,
            string name)
        {
            Transform child = FindDirectChild(root, name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
