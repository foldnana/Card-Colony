using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CardColony.Tests
{
    public sealed class UltimateCleanHudUnityTests
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string ShapeRoot =
            "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/";
        private const string RoundedPanelFill =
            ShapeRoot + "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string ModernDarkButtonFill =
            ShapeRoot + "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string ModernDarkButtonOutline =
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
        public void UiRoot_RightSidebarPrefabUsesModernDarkRoundedTabs()
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
                ModernDarkButtonFill);
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
                AssertSlicedSprite(tabImage, ModernDarkButtonFill);
                Assert.That(tabImage.color.r, Is.LessThan(0.25f));
                Assert.That(tabImage.color.g, Is.LessThan(0.3f));
                Assert.That(tabImage.color.b, Is.LessThan(0.4f));
                Assert.That(tabImage.color.a, Is.EqualTo(1f));
                Image selection = FindDirectChild(
                    tab,
                    "FantasySelectionHighlight")?.GetComponent<Image>();
                AssertSlicedSprite(selection, ModernDarkButtonOutline);
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
    }
}
