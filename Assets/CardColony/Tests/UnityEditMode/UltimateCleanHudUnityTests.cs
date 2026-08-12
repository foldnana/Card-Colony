using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CardColony.Tests
{
    public sealed class UltimateCleanHudUnityTests
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string DialoguePanelPath =
            "Assets/StackCraft/Prefabs/UI/DialoguePanel.prefab";
        private const string ShapeRoot =
            "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/";
        private const string RoundedPanelFill =
            ShapeRoot + "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string LightButtonFill =
            ShapeRoot + "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string LightButtonOutline =
            ShapeRoot +
            "Semi Rounded/Semi Rounded - Outline - 6px - 300ppu.png";

        [Test]
        public void UiRoot_TopStatusPrefabsFormOneAlignedBar()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform day = FindDescendant(root.transform, "DayTimeUI");
            Transform stats = FindDescendant(root.transform, "CardStatsUI");
            AssertSlicedSprite(day.GetComponent<Image>(), RoundedPanelFill);
            AssertSlicedSprite(stats.GetComponent<Image>(), RoundedPanelFill);

            RectTransform dayRect = (RectTransform)day;
            RectTransform statsRect = (RectTransform)stats;
            Assert.That(dayRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(statsRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(dayRect.anchoredPosition.y,
                Is.EqualTo(statsRect.anchoredPosition.y).Within(0.01f));
            Assert.That(dayRect.sizeDelta.y,
                Is.EqualTo(statsRect.sizeDelta.y).Within(0.01f));
            Assert.That(dayRect.sizeDelta.y, Is.InRange(58f, 68f));
            Assert.That(
                statsRect.anchoredPosition.x,
                Is.EqualTo(dayRect.anchoredPosition.x + dayRect.sizeDelta.x)
                    .Within(2f));
        }

        [Test]
        public void UiRoot_RightSidebarPrefabUsesLightRoundedTabs()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform sidebar = FindDescendant(root.transform, "MenuPanel");
            Assert.That(sidebar, Is.Not.Null);
            AssertSlicedSprite(
                sidebar.GetComponent<Image>(),
                RoundedPanelFill);
            RectTransform sidebarRect = (RectTransform)sidebar;
            Assert.That(sidebarRect.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(sidebarRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(sidebarRect.sizeDelta.x, Is.InRange(370f, 390f));

            Transform header = FindDirectChild(sidebar, "Header");
            Assert.That(header, Is.Not.Null);
            AssertSlicedSprite(
                header.GetComponent<Image>(),
                LightButtonFill);
            Assert.That(((RectTransform)header).sizeDelta.y, Is.InRange(58f, 68f));

            foreach (string tabName in new[]
                     {
                         "LocationToggle",
                         "QuestsToggle",
                         "RecipesToggle",
                         "BackpackToggle"
                     })
            {
                Transform tab = FindDescendant(sidebar, tabName);
                Assert.That(tab, Is.Not.Null, tabName);
                Image tabImage = tab.GetComponent<Image>();
                AssertSlicedSprite(tabImage, LightButtonFill);
                Assert.That(ColorLuminance(tabImage.color),
                    Is.InRange(0.62f, 0.92f));
                Assert.That(tabImage.color.a, Is.EqualTo(1f));
                Image selection = FindDirectChild(
                    tab,
                    "FantasySelectionHighlight")?.GetComponent<Image>();
                AssertSlicedSprite(selection, LightButtonOutline);
                Assert.That(selection.raycastTarget, Is.False);
            }

            foreach (string pageName in new[]
                     {
                         "LocationView",
                         "QuestsView",
                         "RecipesView",
                         "BackpackTablePanel"
                     })
            {
                Transform page = FindDescendant(root.transform, pageName);
                Assert.That(page, Is.Not.Null, pageName);
                RectTransform pageRect = (RectTransform)page;
                Assert.That(pageRect.anchorMin.x, Is.EqualTo(0f));
                Assert.That(pageRect.anchorMax.x, Is.EqualTo(1f));
                Assert.That(pageRect.anchorMin.y, Is.EqualTo(0f));
                Assert.That(pageRect.anchorMax.y, Is.EqualTo(1f));
                Assert.That(pageRect.offsetMin.x, Is.InRange(12f, 18f));
                Assert.That(pageRect.offsetMax.x, Is.InRange(-18f, -12f));
                Assert.That(pageRect.offsetMax.y, Is.InRange(-76f, -68f));
            }
        }

        [Test]
        public void UiRoot_ExtendedInterfacesUseLightSurfaces()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            foreach (string panelName in new[]
                     {
                         "BackpackTablePanel",
                         "BackpackSelectedDetails",
                         "InfoPanel",
                         "NpcTradePanel",
                         "NpcTradeScrollView",
                         "WorldMapPartyStatusPanel",
                         "QuestsView",
                         "RecipesView",
                         "CombatHudPanel",
                         "CombatLogPanel",
                         "PublicMarketModal",
                         "PublicMarketBackpackPanel",
                         "PublicMarketMarketPanel",
                         "PublicMarketTransactionPanel"
                     })
            {
                Transform panel = FindDescendant(root.transform, panelName);
                Assert.That(panel, Is.Not.Null, panelName);
                AssertLightSurface(panel.GetComponent<Image>(), panelName);
            }

            foreach (string buttonName in new[]
                     {
                         "PublicMarketCloseButton",
                         "PublicMarketDecreaseButton",
                         "PublicMarketIncreaseButton",
                         "PublicMarketMaximumButton",
                         "PublicMarketConfirmButton",
                         "SkillButton1",
                         "SkillButton2",
                         "SkillButton3",
                         "RetreatButton"
                     })
            {
                Transform button = FindDescendant(root.transform, buttonName);
                Assert.That(button, Is.Not.Null, buttonName);
                AssertLightSurface(button.GetComponent<Image>(), buttonName);
                AssertReadableOnLight(button, buttonName);
            }

            foreach (string borderName in new[]
                     {
                         "PublicMarketBackpackPanelFantasyBorder",
                         "PublicMarketMarketPanelFantasyBorder",
                         "PublicMarketTransactionPanelFantasyBorder"
                     })
            {
                Transform border = FindDescendant(root.transform, borderName);
                Assert.That(border, Is.Not.Null, borderName);
                AssertSlicedSprite(
                    border.GetComponent<Image>(),
                    LightButtonOutline);
            }

            Transform action = FindDescendant(root.transform, "ActionButton");
            Assert.That(action, Is.Not.Null);
            LayoutElement actionLayout = action.GetComponent<LayoutElement>();
            Assert.That(actionLayout, Is.Not.Null);
            Assert.That(actionLayout.minWidth, Is.GreaterThanOrEqualTo(120f));
            Assert.That(actionLayout.preferredWidth, Is.GreaterThanOrEqualTo(120f));
        }

        [Test]
        public void UiRoot_WorldMapPartyPanelIsCompactFourMemberRoster()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform panel = FindDescendant(
                root.transform,
                "WorldMapPartyStatusPanel");
            Assert.That(panel, Is.Not.Null);

            RectTransform panelRect = (RectTransform)panel;
            Assert.That(panelRect.anchorMin, Is.EqualTo(new Vector2(0f, 0.5f)));
            Assert.That(panelRect.anchorMax, Is.EqualTo(new Vector2(0f, 0.5f)));
            Assert.That(panelRect.anchoredPosition.y, Is.InRange(44f, 52f));
            Assert.That(panelRect.sizeDelta.x, Is.InRange(265f, 275f));
            Assert.That(panelRect.sizeDelta.y, Is.InRange(350f, 365f));
            Assert.That(FindDescendant(panel, "PartyRosterCollapseButton"), Is.Not.Null);

            for (int index = 1; index <= 4; index++)
            {
                Transform slot = FindDescendant(
                    panel,
                    $"PartyMemberSlot{index}");
                Assert.That(slot, Is.Not.Null, $"missing party slot {index}");
                Assert.That(slot.GetComponent<Button>(), Is.Not.Null);
                Assert.That(FindDescendant(slot, "Portrait"), Is.Not.Null);
                Assert.That(FindDescendant(slot, "HealthFill"), Is.Not.Null);
                Assert.That(FindDescendant(slot, "SelectionOutline"), Is.Not.Null);
                Assert.That(((RectTransform)slot).sizeDelta.y,
                    Is.InRange(67f, 71f));
            }
        }

        [Test]
        public void PartySelectionService_TracksPersistentMemberIdentity()
        {
            System.Type serviceType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "CryingSnow.StackCraft.PartySelectionService"))
                .FirstOrDefault(type => type != null);
            Assert.That(serviceType, Is.Not.Null);
            System.Reflection.MethodInfo select = serviceType.GetMethod("Select");
            System.Reflection.PropertyInfo selected = serviceType.GetProperty(
                "SelectedPersistentId");
            try
            {
                select.Invoke(null, new object[] { "member-a" });
                Assert.That(
                    selected.GetValue(null),
                    Is.EqualTo("member-a"));

                select.Invoke(null, new object[] { "member-b" });
                Assert.That(
                    selected.GetValue(null),
                    Is.EqualTo("member-b"));
            }
            finally
            {
                select.Invoke(null, new object[] { null });
            }
        }

        [Test]
        public void CombatHud_SuppressesLocationPageAndKeepsActionsInsideSidebar()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform combat = FindDescendant(root.transform, "CombatHudPanel");
            Transform location = FindDescendant(root.transform, "LocationView");
            Transform sidebar = FindDescendant(root.transform, "MenuPanel");
            Assert.That(combat, Is.Not.Null);
            Assert.That(location, Is.Not.Null);
            Assert.That(combat.parent, Is.EqualTo(sidebar),
                "战斗页必须与地点页平级，否则隐藏地点页时战斗页也会消失。");

            System.Type presenterType = FindType(
                "CryingSnow.StackCraft.CombatHudPresenter");
            System.Type locationViewType = FindType(
                "CryingSnow.StackCraft.WorldMapLocationView");
            Component presenter = combat.GetComponent(presenterType);
            Assert.That(presenter, Is.Not.Null);
            var serialized = new SerializedObject(presenter);
            Assert.That(
                serialized.FindProperty("locationView").objectReferenceValue,
                Is.EqualTo(location.GetComponent(locationViewType)));

            foreach (string buttonName in new[]
                     {
                         "SkillButton1",
                         "SkillButton2",
                         "SkillButton3",
                         "RetreatButton"
                     })
            {
                RectTransform button = (RectTransform)FindDescendant(
                    combat,
                    buttonName);
                Assert.That(button.anchorMin.x, Is.EqualTo(0f));
                Assert.That(button.anchorMax.x, Is.EqualTo(1f));
                Assert.That(button.offsetMin.x, Is.InRange(16f, 28f));
                Assert.That(button.offsetMax.x, Is.InRange(-28f, -16f));
            }

            const float minimumSidebarHeight = 514f;
            RectTransform log = (RectTransform)FindDescendant(
                combat,
                "CombatLogPanel");
            Assert.That(log, Is.Not.Null);
            float logTopFromPageTop = minimumSidebarHeight -
                log.anchoredPosition.y - log.sizeDelta.y;
            float lowestActionBottom = new[]
                {
                    "SkillButton1",
                    "SkillButton2",
                    "SkillButton3",
                    "RetreatButton"
                }
                .Select(buttonName => (RectTransform)FindDescendant(
                    combat,
                    buttonName))
                .Max(button => -button.anchoredPosition.y +
                               button.sizeDelta.y);
            Assert.That(lowestActionBottom,
                Is.LessThan(logTopFromPageTop),
                "At the 600px project height, combat actions must not overlap the log.");
            Assert.That(
                FindDescendant(combat, "CombatHint").gameObject.activeSelf,
                Is.False,
                "The long hint must stay hidden in the compact combat layout.");
        }

        [Test]
        public void DialoguePanel_UsesLightRoundedSurfaces()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(
                DialoguePanelPath);
            Assert.That(root, Is.Not.Null);

            foreach (string surfaceName in new[]
                     {
                         "DialoguePanel",
                         "SpeakerHeader",
                         "ReplyButton",
                         "GoodbyeButton"
                     })
            {
                Transform surface = FindDescendant(
                    root.transform,
                    surfaceName);
                Assert.That(surface, Is.Not.Null, surfaceName);
                AssertLightSurface(
                    surface.GetComponent<Image>(),
                    surfaceName,
                    surfaceName == "SpeakerHeader" ? 0.56f : 0.62f);
            }
            AssertReadableOnLight(root.transform, "DialoguePanel");
        }

        [Test]
        public void LegacyUiInstallersFinishWithTheLightSkin()
        {
            foreach (string path in new[]
                     {
                         "Assets/StackCraft/Scripts/Editor/" +
                         "PublicMarketFantasySkinInstaller.cs",
                         "Assets/StackCraft/Scripts/Editor/" +
                         "BackpackDrawerPrefabInstaller.cs",
                         "Assets/StackCraft/Scripts/Editor/" +
                         "WorldMapPartyStatusUiPrefabInstaller.cs"
                     })
            {
                Assert.That(
                    File.ReadAllText(path),
                    Does.Contain(
                        "CommonFantasyHudSkinInstaller.ApplyToPrefabContents(root)"),
                    path);
            }
        }

        private static void AssertLightSurface(
            Image image,
            string name,
            float minimumLuminance = 0.62f)
        {
            AssertSlicedSprite(image, LightButtonFill);
            Assert.That(ColorLuminance(image.color),
                Is.InRange(minimumLuminance, 0.96f),
                $"{name} 应保持可读的中等明度 Light 表面，而不是被旧阈值推回泛白配色。");
            Assert.That(image.color.a, Is.GreaterThan(0.85f), name);
        }

        private static void AssertReadableOnLight(Transform root, string name)
        {
            foreach (TMP_Text text in
                     root.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(
                    ColorLuminance(text.color),
                    Is.LessThan(0.52f),
                    $"{name}/{text.name}");
            }
        }

        private static float ColorLuminance(Color color)
        {
            return 0.2126f * color.r +
                   0.7152f * color.g +
                   0.0722f * color.b;
        }

        private static void AssertSlicedSprite(
            Image image,
            string expectedPath)
        {
            Assert.That(image, Is.Not.Null);
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(image.sprite),
                Is.EqualTo(expectedPath));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
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
                Transform found = FindDescendant(root.GetChild(i), name);
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

        private static System.Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in
                     System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            Assert.Fail($"Type not found: {fullName}");
            return null;
        }
    }
}
