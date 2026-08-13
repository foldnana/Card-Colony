using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CardColony.Tests
{
    public sealed class BackpackDrawerUnityTests
    {
        [Test]
        public void BackpackPrefab_UsesSidebarTabAndInsetCommonMenuPage()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(prefab, Is.Not.Null);
            Transform root = FindDescendant(prefab.transform, "BackpackRoot");
            Transform menuPanel = FindDescendant(prefab.transform, "MenuPanel");
            Transform header = FindDescendant(menuPanel, "Header");
            Transform backpackToggle = FindDescendant(header, "BackpackToggle");
            RectTransform drawer = (RectTransform)FindDescendant(
                prefab.transform,
                "BackpackTablePanel");
            GridLayoutGroup grid = FindDescendant(drawer, "BackpackSlots")
                .GetComponent<GridLayoutGroup>();

            Assert.That(FindDescendant(root, "BackpackSidebarPageV3"), Is.Not.Null,
                "背包侧栏页面必须序列化进 UIRoot.prefab，而不是运行时临时生成。");
            Assert.That(backpackToggle, Is.Not.Null,
                "右侧信息栏顶部必须有背包页签。");
            Assert.That(backpackToggle.parent, Is.EqualTo(header));
            Assert.That(backpackToggle.GetComponent<Toggle>(), Is.Not.Null);
            Assert.That(drawer.parent, Is.EqualTo(menuPanel),
                "背包内容应是右侧信息栏的完整页面。");
            Assert.That(drawer.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(drawer.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(drawer.offsetMin, Is.EqualTo(new Vector2(14f, 14f)));
            Assert.That(drawer.offsetMax, Is.EqualTo(new Vector2(-14f, -72f)));
            Assert.That(FindDescendant(prefab.transform, "BackpackButton"), Is.Null,
                "旧的左下角背包入口必须移除。");
            Assert.That(grid.constraint, Is.EqualTo(
                GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(3));
            Assert.That(grid.cellSize.x, Is.InRange(96f, 116f),
                "两列物品格应填满侧栏宽度，不能继续缩在左上角。");
            Assert.That(grid.cellSize.y, Is.InRange(88f, 112f));
            TMP_Text pickupHint = FindDescendant(drawer, "BackpackPickupHint")
                ?.GetComponent<TMP_Text>();
            Assert.That(pickupHint, Is.Not.Null);
            Assert.That(pickupHint.text, Does.Contain("拖"));
            Assert.That(pickupHint.text, Does.Contain("背包"));
            Assert.That(pickupHint.fontSize, Is.GreaterThanOrEqualTo(17f));
            Assert.That(
                ((RectTransform)FindDescendant(drawer, "BackpackSelectedDetails"))
                    .sizeDelta.y,
                Is.GreaterThanOrEqualTo(140f));

            Transform dragLayer = FindDescendant(prefab.transform, "BackpackDragLayer");
            Canvas dragCanvas = dragLayer.GetComponent<Canvas>();
            Assert.That(dragCanvas, Is.Not.Null,
                "拖进背包时需要专用 UI 拖拽层覆盖右侧面板。");
            Assert.That(dragCanvas.overrideSorting, Is.True);
            Assert.That(dragCanvas.sortingOrder, Is.GreaterThanOrEqualTo(100));
            Transform worldPreview = FindDescendant(
                dragLayer,
                "BackpackWorldCardDragPreview");
            Assert.That(worldPreview, Is.Not.Null,
                "三维卡牌进入屏幕空间 UI 时需要显示前景卡牌预览。");
            Assert.That(worldPreview.gameObject.activeSelf, Is.False);
            Assert.That(FindDescendant(worldPreview, "Art")
                    ?.GetComponent<RawImage>(),
                Is.Not.Null);
        }

        [Test]
        public void BackpackPrefab_BackpackTabMatchesTheOtherSidebarTabs()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform backpackToggle = FindDescendant(
                prefab.transform,
                "BackpackToggle");
            Transform locationToggle = FindDescendant(
                prefab.transform,
                "LocationToggle");
            Toggle backpack = backpackToggle.GetComponent<Toggle>();
            Toggle location = locationToggle.GetComponent<Toggle>();

            Assert.That(backpack.group, Is.SameAs(location.group));
            Assert.That(
                backpackToggle.GetComponent<Image>().sprite,
                Is.EqualTo(locationToggle.GetComponent<Image>().sprite));
            Assert.That(
                backpackToggle.GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("背包"));
        }

        [Test]
        public void BackpackPrefab_UsesExpandableEquipmentStripAndCompactThreeColumnGrid()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(prefab, Is.Not.Null);

            Transform drawer = FindDescendant(
                prefab.transform,
                "BackpackTablePanel");
            Transform characterHeader = FindDescendant(
                drawer,
                "BackpackCharacterHeader");
            Transform equipmentViewport = FindDescendant(
                drawer,
                "BackpackEquipmentViewport");
            Transform equipmentSlots = FindDescendant(
                drawer,
                "BackpackEquipmentSlots");
            Transform itemViewport = FindDescendant(
                drawer,
                "BackpackScrollViewport");
            GridLayoutGroup itemGrid = FindDescendant(
                drawer,
                "BackpackSlots").GetComponent<GridLayoutGroup>();

            Assert.That(characterHeader, Is.Not.Null,
                "背包顶部需要显示当前换装人物。 ");
            Assert.That(
                FindDescendant(characterHeader, "BackpackCharacterName")
                    ?.GetComponent<TMP_Text>(),
                Is.Not.Null);
            Assert.That(equipmentViewport, Is.Not.Null,
                "当前装备必须拥有独立的横向滚动视口。 ");
            ScrollRect equipmentScroll = equipmentViewport
                .GetComponent<ScrollRect>();
            Assert.That(equipmentScroll, Is.Not.Null);
            Assert.That(equipmentScroll.horizontal, Is.True);
            Assert.That(equipmentScroll.vertical, Is.False);
            Assert.That(equipmentScroll.content, Is.EqualTo(equipmentSlots));
            Assert.That(
                equipmentSlots.GetComponent<HorizontalLayoutGroup>(),
                Is.Not.Null,
                "装备槽必须由布局组件动态排列，不能写死三个位置。 ");
            Assert.That(equipmentSlots.childCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(
                FindDescendant(equipmentSlots, "EquipmentSlotTemplate"),
                Is.Not.Null,
                "需要保留可复用模板，以后增加槽位不必重做界面。 ");

            Assert.That(itemGrid.constraint, Is.EqualTo(
                GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(itemGrid.constraintCount, Is.EqualTo(3));
            Assert.That(itemGrid.cellSize.x, Is.InRange(96f, 116f));
            Assert.That(itemGrid.cellSize.y, Is.InRange(88f, 112f));
            Assert.That(
                ((RectTransform)itemViewport).sizeDelta.y,
                Is.LessThanOrEqualTo(-250f),
                "缩小格子后要保留足够的纵向浏览空间。 ");
        }

        [Test]
        public void BackpackEquipmentTransfer_SwapsOldEquipmentBackIntoBackpack()
        {
            Type transferType = FindType(
                "CryingSnow.StackCraft.BackpackEquipmentTransfer");
            Assert.That(transferType, Is.Not.Null,
                "需要独立的背包换装事务，防止旧装备掉到地图或丢失。 ");
            Assert.That(
                transferType.GetMethod(
                    "TryEquip",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                transferType.GetMethod(
                    "TryUnequip",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null);
        }

        [Test]
        public void BackpackEquipmentTransfer_ActuallySwapsAndUnequipsWithoutLosingCards()
        {
            Type transferType = FindType(
                "CryingSnow.StackCraft.BackpackEquipmentTransfer");
            Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type entryType = FindType("CryingSnow.StackCraft.BackpackEntryData");
            Type slotType = FindType("CryingSnow.StackCraft.EquipmentSlot");
            Assert.That(transferType, Is.Not.Null);
            Assert.That(backpackType, Is.Not.Null);
            Assert.That(cardDataType, Is.Not.Null);
            Assert.That(entryType, Is.Not.Null);
            Assert.That(slotType, Is.Not.Null);

            object backpack = Activator.CreateInstance(backpackType);
            object member = Activator.CreateInstance(cardDataType);
            object oldEquipment = Activator.CreateInstance(cardDataType);
            object incomingEquipment = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id")?.SetValue(oldEquipment, "old_weapon");
            cardDataType.GetField("Id")?.SetValue(incomingEquipment, "new_weapon");
            object equippedItems = cardDataType.GetField("EquippedItems")
                ?.GetValue(member);
            equippedItems?.GetType().GetMethod("Add")
                ?.Invoke(equippedItems, new[] { oldEquipment });

            object[] addArguments = { incomingEquipment, null };
            Assert.That(
                backpackType.GetMethod("TryAdd")?.Invoke(backpack, addArguments),
                Is.True);
            object incomingEntry = addArguments[1];
            string entryId = (string)entryType.GetField("InstanceId")
                ?.GetValue(incomingEntry);
            object weaponSlot = Enum.GetValues(slotType).GetValue(0);
            Type nullableSlotType = typeof(Nullable<>).MakeGenericType(slotType);
            Type resolverType = typeof(Func<,>).MakeGenericType(
                typeof(string),
                nullableSlotType);
            MethodInfo resolverFactory = GetType().GetMethod(
                nameof(CreateEquipmentSlotResolver),
                BindingFlags.Static | BindingFlags.NonPublic);
            Delegate resolver = (Delegate)resolverFactory
                ?.MakeGenericMethod(slotType)
                .Invoke(null, new[] { weaponSlot });

            MethodInfo tryEquip = transferType.GetMethod("TryEquip");
            Assert.That(tryEquip?.Invoke(
                null,
                new[] { backpack, member, entryId, resolver }), Is.True);
            Assert.That((int)backpackType.GetProperty("Count")?.GetValue(backpack),
                Is.EqualTo(1));
            object firstEquipped = equippedItems.GetType().GetProperty("Item")
                ?.GetValue(equippedItems, new object[] { 0 });
            Assert.That(cardDataType.GetField("Id")?.GetValue(firstEquipped),
                Is.EqualTo("new_weapon"));

            MethodInfo tryUnequip = transferType.GetMethod("TryUnequip");
            Assert.That(tryUnequip?.Invoke(
                null,
                new[] { backpack, member, weaponSlot, resolver }), Is.True);
            Assert.That((int)backpackType.GetProperty("Count")?.GetValue(backpack),
                Is.EqualTo(2));
            Assert.That((int)equippedItems.GetType().GetProperty("Count")
                ?.GetValue(equippedItems), Is.Zero);
        }

        private static Delegate CreateEquipmentSlotResolver<TSlot>(object slot)
            where TSlot : struct, Enum
        {
            TSlot typedSlot = (TSlot)slot;
            Func<string, TSlot?> resolver = id =>
                id == "old_weapon" || id == "new_weapon"
                    ? typedSlot
                    : null;
            return resolver;
        }

        [Test]
        public void BackpackView_CloseReturnsToTheLocationTab()
        {
            GameObject root = new GameObject("Sidebar Toggle Test Root");
            root.SetActive(false);
            try
            {
                ToggleGroup group = root.AddComponent<ToggleGroup>();
                group.allowSwitchOff = false;
                Toggle location = new GameObject("LocationToggle", typeof(Toggle))
                    .GetComponent<Toggle>();
                location.transform.SetParent(root.transform, false);
                location.group = group;
                Toggle backpack = new GameObject("BackpackToggle", typeof(Toggle))
                    .GetComponent<Toggle>();
                backpack.transform.SetParent(root.transform, false);
                backpack.group = group;
                var panel = new GameObject(
                    "BackpackTablePanel",
                    typeof(RectTransform));
                panel.transform.SetParent(root.transform, false);
                var viewObject = new GameObject("BackpackRoot");
                viewObject.transform.SetParent(root.transform, false);
                Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
                Component view = viewObject.AddComponent(viewType);
                viewType.GetField("tabToggle",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(view, backpack);
                FieldInfo fallbackField = viewType.GetField(
                    "fallbackToggle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(fallbackField, Is.Not.Null,
                    "关闭背包时需要选回地点页，不能让 ToggleGroup 与页面显示状态不一致。");
                fallbackField.SetValue(view, location);
                viewType.GetField("tablePanel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(view, panel.GetComponent<RectTransform>());

                backpack.SetIsOnWithoutNotify(true);
                root.SetActive(true);
                viewType.GetMethod("Close").Invoke(view, null);

                Assert.That(location.isOn, Is.True);
                Assert.That(backpack.isOn, Is.False);
                Assert.That(panel.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BackpackItemView_RendersASelectableIconWithQuantityAndDetails()
        {
            LightweightBackpackUi ui = LightweightBackpackUi.Create();
            GameObject itemObject = null;
            try
            {
                Type itemType = FindType("CryingSnow.StackCraft.BackpackItemView");
                Type entryType = FindType("CryingSnow.StackCraft.BackpackEntryData");
                Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
                Type definitionType = FindType("CryingSnow.StackCraft.CardDefinition");
                UnityEngine.Object definition = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
                Assert.That(definition, Is.Not.Null);

                ui.Drawer.sizeDelta = new Vector2(320f, 520f);
                ui.Root.SetActive(true);
                ui.View.GetType().GetMethod("Open").Invoke(ui.View, null);

                object entry = Activator.CreateInstance(entryType);
                object cardData = Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(
                    cardData,
                    definitionType.GetProperty("Id").GetValue(definition));
                cardDataType.GetField("CurrentNutrition").SetValue(
                    cardData,
                    definitionType.GetProperty("Nutrition").GetValue(definition));
                entryType.GetField("InstanceId").SetValue(entry, "apple-test");
                entryType.GetField("Card").SetValue(entry, cardData);

                itemObject = new GameObject(
                    "BackpackItem_Apple",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                itemObject.transform.SetParent(ui.SlotsRoot, false);
                MonoBehaviour item = itemObject.AddComponent(itemType) as MonoBehaviour;
                MethodInfo bind = itemType.GetMethod(
                    "Bind",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        entryType,
                        definitionType,
                        ui.View.GetType(),
                        typeof(TMP_FontAsset),
                        typeof(int),
                        typeof(IReadOnlyList<string>)
                    },
                    null);
                Assert.That(bind, Is.Not.Null,
                    "图标背包需要接收视觉堆叠数量。");
                bind.Invoke(item, new[]
                {
                    entry,
                    definition,
                    ui.View,
                    ui.CapacityLabel.font,
                    (object)3,
                    new[] { "apple-test", "apple-test-2", "apple-test-3" }
                });

                var groupedEntryIds = (IReadOnlyList<string>)itemType
                    .GetProperty("EntryIds")
                    ?.GetValue(item);
                Assert.That(groupedEntryIds, Is.Not.Null,
                    "×N 图标必须保留整组背包条目，拖出时才能恢复完整卡堆。");
                Assert.That(groupedEntryIds.Count, Is.EqualTo(3));

                RectTransform itemRect = (RectTransform)item.transform;
                Assert.That(
                    Mathf.Abs(itemRect.sizeDelta.x - itemRect.sizeDelta.y),
                    Is.LessThanOrEqualTo(12f));
                Assert.That(itemRect.sizeDelta.x, Is.InRange(94f, 104f),
                    "物品图标应与放大的背包格匹配。");
                Transform art = FindDescendant(item.transform, "Art");
                Assert.That(art, Is.Not.Null);
                Assert.That(((RectTransform)art).sizeDelta.x,
                    Is.GreaterThanOrEqualTo(60f));
                Transform quantity = FindDescendant(item.transform, "QuantityBadge");
                Assert.That(quantity, Is.Not.Null);
                Assert.That(
                    quantity.GetComponentInChildren<TMP_Text>().text,
                    Is.EqualTo("×3"));
                Assert.That(FindDescendant(item.transform, "Header"), Is.Null,
                    "背包格内不应继续绘制完整卡牌标题栏。");
                Assert.That(FindDescendant(item.transform, "Title"), Is.Null,
                    "背包格内不应继续绘制完整卡牌标题。");

                MethodInfo setDragPresentation = itemType.GetMethod(
                    "SetWorldDragPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setDragPresentation, Is.Not.Null,
                    "物品拖出背包 UI 后需要切换成竖向卡牌预览。");
                setDragPresentation.Invoke(item, new object[] { true });
                Assert.That(itemRect.sizeDelta.y,
                    Is.GreaterThan(itemRect.sizeDelta.x + 24f));
                Transform dragHeader = FindDescendant(item.transform, "DragHeader");
                Assert.That(dragHeader, Is.Not.Null);
                Assert.That(dragHeader.gameObject.activeSelf, Is.True);
                Assert.That(FindDescendant(dragHeader, "DragTitle")
                        .GetComponent<TMP_Text>().text,
                    Does.Contain("苹果"));
                Assert.That(((RectTransform)art).anchoredPosition.y,
                    Is.LessThan(0f));

                setDragPresentation.Invoke(item, new object[] { false });
                Assert.That(
                    Mathf.Abs(itemRect.sizeDelta.x - itemRect.sizeDelta.y),
                    Is.LessThanOrEqualTo(8f));
                Assert.That(dragHeader.gameObject.activeSelf, Is.False);

                itemType.GetMethod("OnPointerClick")
                    .Invoke(item, new object[] { null });
                Assert.That(ui.SelectedName.text, Does.Contain("苹果"));
                Assert.That(ui.SelectedDescription.text, Is.Not.Empty);
            }
            finally
            {
                if (itemObject != null)
                    UnityEngine.Object.DestroyImmediate(itemObject);
                ui.Dispose();
            }
        }

        [Test]
        public void BackpackItemDrag_CrossingDrawerBoundarySwitchesCardPresentation()
        {
            LightweightBackpackUi ui = LightweightBackpackUi.Create();
            GameObject itemObject = null;
            try
            {
                Type itemType = FindType("CryingSnow.StackCraft.BackpackItemView");
                Type entryType = FindType("CryingSnow.StackCraft.BackpackEntryData");
                Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
                Type definitionType = FindType("CryingSnow.StackCraft.CardDefinition");
                UnityEngine.Object definition = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
                Assert.That(definition, Is.Not.Null);

                object entry = Activator.CreateInstance(entryType);
                object cardData = Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(
                    cardData,
                    definitionType.GetProperty("Id").GetValue(definition));
                entryType.GetField("InstanceId").SetValue(entry, "drag-apple");
                entryType.GetField("Card").SetValue(entry, cardData);

                itemObject = new GameObject(
                    "BackpackItem_DragApple",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                itemObject.transform.SetParent(ui.SlotsRoot, false);
                MonoBehaviour item = itemObject.AddComponent(itemType) as MonoBehaviour;
                MethodInfo bind = itemType.GetMethod(
                    "Bind",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        entryType,
                        definitionType,
                        ui.View.GetType(),
                        typeof(TMP_FontAsset),
                        typeof(int),
                        typeof(IReadOnlyList<string>)
                    },
                    null);
                bind.Invoke(item, new[]
                {
                    entry,
                    definition,
                    ui.View,
                    ui.CapacityLabel.font,
                    (object)3,
                    new[] { "drag-apple", "drag-apple-2", "drag-apple-3" }
                });

                Vector2 inside = ui.Drawer.TransformPoint(ui.Drawer.rect.center);
                Vector2 outside = ui.Drawer.TransformPoint(
                    new Vector2(ui.Drawer.rect.xMin - 80f, ui.Drawer.rect.center.y));
                MethodInfo beginDrag = ui.View.GetType().GetMethod(
                    "BeginItemDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo updateDrag = ui.View.GetType().GetMethod(
                    "UpdateItemDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                beginDrag.Invoke(ui.View, new object[] { item, inside });
                RectTransform itemRect = (RectTransform)item.transform;
                Transform dragLayer = FindDescendant(ui.Root.transform, "BackpackDragLayer");
                Transform dragHeader = FindDescendant(item.transform, "DragHeader");
                Transform art = FindDescendant(item.transform, "Art");
                Transform quantity = FindDescendant(item.transform, "QuantityBadge");
                Assert.That(item.transform.parent, Is.EqualTo(dragLayer));
                Assert.That(item.transform.GetSiblingIndex(),
                    Is.EqualTo(dragLayer.childCount - 1));
                Assert.That(itemRect.sizeDelta, Is.EqualTo(new Vector2(98f, 90f)));
                Assert.That(dragHeader.gameObject.activeSelf, Is.False);

                updateDrag.Invoke(ui.View, new object[] { item, outside });
                Assert.That(itemRect.sizeDelta, Is.EqualTo(new Vector2(120f, 156f)));
                Assert.That(dragHeader.gameObject.activeSelf, Is.True);
                Assert.That(dragHeader.GetComponent<Image>().color,
                    Is.EqualTo(new Color(0.96f, 0.40f, 0.16f, 1f)));
                Assert.That(FindDescendant(dragHeader, "DragTitle")
                        .GetComponent<TMP_Text>().text,
                    Does.Contain("苹果"));
                Assert.That(art.GetComponent<RawImage>().texture,
                    Is.EqualTo(definitionType.GetProperty("ArtTexture")
                        .GetValue(definition)));
                Assert.That(quantity.gameObject.activeSelf, Is.True);
                Assert.That(quantity.GetComponentInChildren<TMP_Text>().text,
                    Is.EqualTo("×3"));

                updateDrag.Invoke(ui.View, new object[] { item, inside });
                Assert.That(itemRect.sizeDelta, Is.EqualTo(new Vector2(98f, 90f)));
                Assert.That(dragHeader.gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (itemObject != null)
                    UnityEngine.Object.DestroyImmediate(itemObject);
                ui.Dispose();
            }
        }

        [Test]
        public void BackpackItemView_SingleItemDoesNotRenderBlackQuantityBadge()
        {
            GameObject itemObject = null;
            try
            {
                Type itemType = FindType("CryingSnow.StackCraft.BackpackItemView");
                Type entryType = FindType("CryingSnow.StackCraft.BackpackEntryData");
                Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
                Type definitionType = FindType("CryingSnow.StackCraft.CardDefinition");
                Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
                object entry = Activator.CreateInstance(entryType);
                object card = Activator.CreateInstance(cardDataType);
                entryType.GetField("InstanceId").SetValue(entry, "single-item");
                entryType.GetField("Card").SetValue(entry, card);

                itemObject = new GameObject(
                    "SingleBackpackItem",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                MonoBehaviour item = itemObject.AddComponent(itemType) as MonoBehaviour;
                MethodInfo bind = itemType.GetMethod(
                    "Bind",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        entryType,
                        definitionType,
                        viewType,
                        typeof(TMP_FontAsset),
                        typeof(int),
                        typeof(IReadOnlyList<string>)
                    },
                    null);
                bind.Invoke(item, new object[]
                {
                    entry,
                    null,
                    null,
                    null,
                    1,
                    new[] { "single-item" }
                });

                Assert.That(FindDescendant(item.transform, "QuantityBadge"), Is.Null,
                    "单件物品右下角不能残留黑色数量块。");
            }
            finally
            {
                if (itemObject != null)
                    UnityEngine.Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void BackpackView_ShowsWorldCardPreviewAboveTheOpenSidebar()
        {
            GameObject uiInstance = null;
            GameObject cardObject = null;
            FieldInfo backpackInstanceField = null;
            object previousBackpackInstance = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
                uiInstance = UnityEngine.Object.Instantiate(prefab);
                Transform backpackRoot = FindDescendant(uiInstance.transform, "BackpackRoot");
                MonoBehaviour view = backpackRoot.GetComponents<MonoBehaviour>()
                    .Single(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.BackpackView");
                backpackInstanceField = view.GetType().GetField(
                    "<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);
                previousBackpackInstance = backpackInstanceField?.GetValue(null);
                backpackInstanceField?.SetValue(null, view);
                view.GetType().GetMethod("ToggleView")
                    .Invoke(view, new object[] { true });
                Canvas.ForceUpdateCanvases();

                Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
                UnityEngine.Object definition = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
                cardObject = new GameObject(
                    "DraggedWorldCard",
                    typeof(MeshRenderer),
                    typeof(BoxCollider));
                Component card = cardObject.AddComponent(cardType);
                Component controller = cardObject.AddComponent(FindType(
                    "CryingSnow.StackCraft.CardController"));
                cardType.GetMethod("SetDefinition")
                    .Invoke(card, new[] { definition });

                RectTransform drawer = (RectTransform)FindDescendant(
                    uiInstance.transform,
                    "BackpackTablePanel");
                Vector3 drawerCenter = drawer.TransformPoint(drawer.rect.center);
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                    null,
                    drawerCenter);
                MethodInfo updatePreview = view.GetType().GetMethod(
                    "UpdateWorldCardDragPreview",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(updatePreview, Is.Not.Null);
                Assert.That(
                    updatePreview.Invoke(
                        view,
                        new object[] { card, screenPoint, Vector2.zero }),
                    Is.True);

                Transform preview = FindDescendant(
                    uiInstance.transform,
                    "BackpackWorldCardDragPreview");
                Assert.That(preview.gameObject.activeSelf, Is.True);
                Assert.That(preview.parent.name, Is.EqualTo("BackpackDragLayer"));
                Assert.That(preview.GetComponentInParent<Canvas>().sortingOrder,
                    Is.GreaterThanOrEqualTo(100));
                Assert.That(FindDescendant(preview, "Art")
                        .GetComponent<RawImage>().texture,
                    Is.EqualTo(definition.GetType().GetProperty("ArtTexture")
                        .GetValue(definition)));

                Assert.That(
                    updatePreview.Invoke(
                        view,
                        new object[] { card, new Vector2(-100f, -100f), Vector2.zero }),
                    Is.False);
                Assert.That(preview.gameObject.activeSelf, Is.False,
                    "指针离开背包侧栏后必须隐藏前景预览。");

                updatePreview.Invoke(
                    view,
                    new object[] { card, screenPoint, Vector2.zero });
                view.GetType().GetMethod("ToggleView")
                    .Invoke(view, new object[] { false });
                Assert.That(preview.gameObject.activeSelf, Is.False,
                    "关闭背包页时必须隐藏前景预览。");

                view.GetType().GetMethod("ToggleView")
                    .Invoke(view, new object[] { true });
                Canvas.ForceUpdateCanvases();
                updatePreview.Invoke(
                    view,
                    new object[] { card, screenPoint, Vector2.zero });
                controller.GetType().GetMethod(
                        "OnDisable",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(preview.gameObject.activeSelf, Is.False);
            }
            finally
            {
                backpackInstanceField?.SetValue(null, previousBackpackInstance);
                if (cardObject != null)
                    UnityEngine.Object.DestroyImmediate(cardObject);
                if (uiInstance != null)
                    UnityEngine.Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void BackpackView_FinalizesTakenCardUsingItsFullBoardFootprint()
        {
            LightweightBackpackUi ui = LightweightBackpackUi.Create();
            GameObject boardObject = null;
            GameObject cardObject = null;
            ScriptableObject settings = null;
            Mesh boardMesh = null;
            Type boardType = FindType("CryingSnow.StackCraft.Board");
            FieldInfo boardInstance = boardType.GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                boardInstance.SetValue(null, null);
                boardObject = new GameObject(
                    "Backpack Drawer Board",
                    typeof(SkinnedMeshRenderer));
                boardMesh = new Mesh
                {
                    vertices = new[]
                    {
                        new Vector3(-5f, 0f, -5f),
                        new Vector3(-5f, 0f, 5f),
                        new Vector3(5f, 0f, 5f),
                        new Vector3(5f, 0f, -5f)
                    },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 }
                };
                boardObject.GetComponent<SkinnedMeshRenderer>().sharedMesh =
                    boardMesh;
                Component board = boardObject.AddComponent(boardType);
                boardInstance.SetValue(null, board);
                boardType.GetMethod("SetWorldBoundsOverride").Invoke(
                    board,
                    new object[]
                    {
                        new Bounds(Vector3.zero, new Vector3(10f, 0.1f, 10f))
                    });

                Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
                Type stackType = FindType("CryingSnow.StackCraft.CardStack");
                Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
                cardObject = new GameObject(
                    "Taken Card",
                    typeof(MeshRenderer),
                    typeof(BoxCollider));
                Component card = cardObject.AddComponent(cardType);
                settings = ScriptableObject.CreateInstance(settingsType);
                cardType.GetProperty("Settings").SetValue(card, settings);
                cardType.GetProperty("Size").SetValue(card, new Vector2(2f, 2f));
                object stack = Activator.CreateInstance(
                    stackType,
                    card,
                    Vector3.zero);

                MethodInfo finalize = ui.View.GetType().GetMethod(
                    "FinalizeTakenCardPlacement",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(finalize, Is.Not.Null,
                    "从背包生成卡牌后需要按实际卡堆尺寸修正落点。");
                finalize.Invoke(
                    null,
                    new object[] { card, new Vector3(4.8f, 0f, 4.8f) });

                Vector3 target = (Vector3)stackType.GetProperty("TargetPosition")
                    .GetValue(stack);
                Assert.That(target.x, Is.EqualTo(4f).Within(0.001f));
                Assert.That(target.z, Is.EqualTo(2.5f).Within(0.001f));
                Assert.That(
                    (bool)boardType.GetMethod("IsPointValid")
                        .Invoke(board, new[] { (object)target, stack }),
                    Is.True);
            }
            finally
            {
                boardInstance?.SetValue(null, null);
                if (cardObject != null)
                    UnityEngine.Object.DestroyImmediate(cardObject);
                if (settings != null)
                    UnityEngine.Object.DestroyImmediate(settings);
                if (boardObject != null)
                    UnityEngine.Object.DestroyImmediate(boardObject);
                if (boardMesh != null)
                    UnityEngine.Object.DestroyImmediate(boardMesh);
                ui.Dispose();
            }
        }

        [Test]
        public void BackpackData_MoveEntryToOccupiedSlot_SwapsBothEntries()
        {
            Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object backpack = Activator.CreateInstance(backpackType);
            object firstCard = Activator.CreateInstance(cardDataType);
            object secondCard = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(firstCard, "first-item");
            cardDataType.GetField("Id").SetValue(secondCard, "second-item");
            object[] firstAdd = { firstCard, null };
            object[] secondAdd = { secondCard, null };
            backpackType.GetMethod("TryAdd").Invoke(backpack, firstAdd);
            backpackType.GetMethod("TryAdd").Invoke(backpack, secondAdd);
            object firstEntry = firstAdd[1];
            object secondEntry = secondAdd[1];
            string firstId = (string)firstEntry.GetType()
                .GetField("InstanceId").GetValue(firstEntry);
            MethodInfo move = backpackType.GetMethod(
                "TryMoveEntry",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(move, Is.Not.Null,
                "背包数据需要提供可持久化的槽位移动操作。");
            Assert.That(move.Invoke(backpack, new object[] { firstId, 1 }),
                Is.True);
            Assert.That(firstEntry.GetType().GetField("SlotIndex")
                    .GetValue(firstEntry),
                Is.EqualTo(1));
            Assert.That(secondEntry.GetType().GetField("SlotIndex")
                    .GetValue(secondEntry),
                Is.EqualTo(0));
        }

        [Test]
        public void BackpackVisualGrouping_MovedQuantityIconKeepsItsRepresentative()
        {
            Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type groupingType = FindType("CryingSnow.StackCraft.BackpackVisualGrouping");
            object backpack = Activator.CreateInstance(backpackType);
            for (int index = 0; index < 3; index++)
            {
                object card = Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(card, "grouped-currency");
                object[] addArguments = { card, null };
                backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments);
            }

            MethodInfo build = groupingType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public);
            var isCurrency = new Func<string, bool>(_ => true);
            object groupsBefore = build.Invoke(
                null,
                new object[] { backpack, isCurrency });
            object groupBefore = ((System.Collections.IEnumerable)groupsBefore)
                .Cast<object>().Single();
            object representativeBefore = groupBefore.GetType()
                .GetProperty("Representative").GetValue(groupBefore);
            string representativeId = (string)representativeBefore.GetType()
                .GetField("InstanceId").GetValue(representativeBefore);

            Assert.That(
                backpackType.GetMethod("TryMoveEntry")
                    .Invoke(backpack, new object[] { representativeId, 6 }),
                Is.True);

            object groupsAfter = build.Invoke(
                null,
                new object[] { backpack, isCurrency });
            object groupAfter = ((System.Collections.IEnumerable)groupsAfter)
                .Cast<object>().Single();
            object representativeAfter = groupAfter.GetType()
                .GetProperty("Representative").GetValue(groupAfter);
            Assert.That(
                representativeAfter.GetType().GetField("InstanceId")
                    .GetValue(representativeAfter),
                Is.EqualTo(representativeId),
                "移动 ×N 图标后必须继续用同一条目定位，不能跳回旧格子。");
            Assert.That(
                representativeAfter.GetType().GetField("SlotIndex")
                    .GetValue(representativeAfter),
                Is.EqualTo(6));
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing type {fullName}");
            return type;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDescendant(root.GetChild(index), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private sealed class LightweightBackpackUi : IDisposable
        {
            public GameObject Root { get; private set; }
            public MonoBehaviour View { get; private set; }
            public Button OpenButton { get; private set; }
            public Image OpenButtonImage { get; private set; }
            public RectTransform Drawer { get; private set; }
            public GridLayoutGroup Grid { get; private set; }
            public RectTransform SlotsRoot { get; private set; }
            public TMP_Text CapacityLabel { get; private set; }
            public TMP_Text SelectedName { get; private set; }
            public TMP_Text SelectedDescription { get; private set; }

            public static LightweightBackpackUi Create()
            {
                var result = new LightweightBackpackUi();
                result.Root = new GameObject(
                    "BackpackRoot",
                    typeof(RectTransform));
                result.Root.SetActive(false);
                result.View = result.Root.AddComponent(
                    FindType("CryingSnow.StackCraft.BackpackView")) as MonoBehaviour;

                result.OpenButton = CreateButton("BackpackButton", result.Root.transform);
                result.OpenButtonImage = result.OpenButton.GetComponent<Image>();
                TMP_Text openLabel = CreateText("Label", result.OpenButton.transform);

                GameObject drawerObject = CreateUiObject(
                    "BackpackTablePanel",
                    result.Root.transform,
                    typeof(Image));
                result.Drawer = (RectTransform)drawerObject.transform;
                result.CapacityLabel = CreateText(
                    "BackpackCapacityText",
                    result.Drawer);
                Button close = CreateButton("BackpackCloseButton", result.Drawer);
                Button arrange = CreateButton("BackpackArrangeButton", result.Drawer);

                GameObject background = CreateUiObject(
                    "BackpackBackground",
                    result.Drawer,
                    typeof(Image));
                background.transform.SetAsFirstSibling();

                GameObject viewport = CreateUiObject(
                    "BackpackScrollViewport",
                    result.Drawer,
                    typeof(Image),
                    typeof(RectMask2D),
                    typeof(ScrollRect));
                GameObject slots = CreateUiObject(
                    "BackpackSlots",
                    viewport.transform,
                    typeof(GridLayoutGroup));
                result.SlotsRoot = (RectTransform)slots.transform;
                result.Grid = slots.GetComponent<GridLayoutGroup>();
                viewport.GetComponent<ScrollRect>().content = result.SlotsRoot;
                viewport.GetComponent<ScrollRect>().viewport =
                    (RectTransform)viewport.transform;

                GameObject dragLayer = CreateUiObject(
                    "BackpackDragLayer",
                    result.Root.transform);

                SetField(result.View, "openButton", result.OpenButton);
                SetField(result.View, "openButtonLabel", openLabel);
                SetField(result.View, "tablePanel", result.Drawer);
                SetField(result.View, "capacityLabel", result.CapacityLabel);
                SetField(result.View, "closeButton", close);
                SetField(result.View, "arrangeButton", arrange);
                SetField(result.View, "slotsRoot", result.SlotsRoot);
                SetField(result.View, "dragLayer", (RectTransform)dragLayer.transform);

                result.SelectedName = CreateText(
                    "BackpackSelectedName",
                    result.Drawer);
                TMP_Text selectedType = CreateText(
                    "BackpackSelectedType",
                    result.Drawer);
                result.SelectedDescription = CreateText(
                    "BackpackSelectedDescription",
                    result.Drawer);
                SetField(result.View, "selectedNameLabel", result.SelectedName);
                SetField(result.View, "selectedTypeLabel", selectedType);
                SetField(
                    result.View,
                    "selectedDescriptionLabel",
                    result.SelectedDescription);
                return result;
            }

            public void Dispose()
            {
                if (Root != null)
                    UnityEngine.Object.DestroyImmediate(Root);
            }

            private static GameObject CreateUiObject(
                string name,
                Transform parent,
                params Type[] extraTypes)
            {
                Type[] types = new[]
                    {
                        typeof(RectTransform),
                        typeof(CanvasRenderer)
                    }
                    .Concat(extraTypes)
                    .Distinct()
                    .ToArray();
                var child = new GameObject(name, types);
                child.transform.SetParent(parent, false);
                return child;
            }

            private static Button CreateButton(string name, Transform parent)
            {
                GameObject child = CreateUiObject(
                    name,
                    parent,
                    typeof(Image),
                    typeof(Button));
                return child.GetComponent<Button>();
            }

            private static TMP_Text CreateText(string name, Transform parent)
            {
                GameObject child = CreateUiObject(
                    name,
                    parent,
                    typeof(TextMeshProUGUI));
                return child.GetComponent<TMP_Text>();
            }

            private static void SetField(object target, string name, object value)
            {
                FieldInfo field = target.GetType().GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"Missing field {name}");
                field.SetValue(target, value);
            }
        }
    }
}
