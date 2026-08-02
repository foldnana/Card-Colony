using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class CombatSystemPrefabInstaller
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string SkillFolder =
            "Assets/StackCraft/Resources/Combat/Skills";
        private const string ItemFolder =
            "Assets/StackCraft/Resources/Combat/Items";
        private const string SkillPath = SkillFolder + "/Skill_PowerStrike.asset";
        private const string ItemPath = ItemFolder + "/CombatItem_Medicine.asset";
        private const string MedicinePath =
            "Assets/StackCraft/Resources/Cards/Consumables/Card_Medicine.asset";
        private const string PanelSpritePath =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/Frame/PanelFrame_01_Bg.png";
        private const string BorderSpritePath =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/Popup/Popup_01_Border.png";
        private const string BlueButtonPath =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/Button/Button_Rectangle_01_Convex_Blue.Png";
        private const string PurpleButtonPath =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/Button/Button_Rectangle_01_Convex_Purple.Png";
        private const string DarkButtonPath =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/Button/Button_Rectangle_01_Convex_Dark.Png";

        [MenuItem("Tools/StackCraft/Install Combat System UI And Content")]
        public static void Install()
        {
            CombatSkillDefinition skill = InstallPowerStrike();
            CombatItemDefinition item = InstallMedicine();
            AssignSkillToProtagonist(skill);
            AssignMedicineItem(item);
            AssetDatabase.SaveAssets();
            if (!CombatContentValidator.Validate(logResults: true))
                throw new System.InvalidOperationException(
                    "Combat content validation failed. UI installation was cancelled.");
            InstallUi();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Installed deterministic combat content and serialized HUD.");
        }

        private static CombatSkillDefinition InstallPowerStrike()
        {
            EnsureFolder("Assets/StackCraft/Resources", "Combat");
            EnsureFolder("Assets/StackCraft/Resources/Combat", "Skills");
            CombatSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<CombatSkillDefinition>(SkillPath);
            if (skill == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(SkillPath) != null)
                    AssetDatabase.DeleteAsset(SkillPath);
                skill = ScriptableObject.CreateInstance<CombatSkillDefinition>();
                AssetDatabase.CreateAsset(skill, SkillPath);
            }

            var serialized = new SerializedObject(skill);
            serialized.FindProperty("id").stringValue =
                "skill_protagonist_power_strike";
            serialized.FindProperty("displayName").stringValue = "奋力一击";
            serialized.FindProperty("description").stringValue =
                "消耗2点体力，对一个敌人造成150%伤害。";
            serialized.FindProperty("targetRule").enumValueIndex =
                (int)CombatTargetRule.SingleLivingEnemy;
            serialized.FindProperty("energyCost").intValue = 2;
            serialized.FindProperty("cooldownSeconds").floatValue = 4f;
            serialized.FindProperty("powerMultiplier").floatValue = 1.5f;
            serialized.FindProperty("flatPower").intValue = 0;
            serialized.FindProperty("canCritical").boolValue = true;
            serialized.FindProperty("retargetIfInvalid").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static CombatItemDefinition InstallMedicine()
        {
            EnsureFolder("Assets/StackCraft/Resources/Combat", "Items");
            CombatItemDefinition item =
                AssetDatabase.LoadAssetAtPath<CombatItemDefinition>(ItemPath);
            if (item == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ItemPath) != null)
                    AssetDatabase.DeleteAsset(ItemPath);
                item = ScriptableObject.CreateInstance<CombatItemDefinition>();
                AssetDatabase.CreateAsset(item, ItemPath);
            }

            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = "combat_item_medicine";
            serialized.FindProperty("targetRule").enumValueIndex =
                (int)CombatTargetRule.SingleLivingAlly;
            serialized.FindProperty("effectType").enumValueIndex =
                (int)CombatItemEffectType.Heal;
            serialized.FindProperty("magnitude").intValue = 3;
            serialized.FindProperty("consumesAction").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void AssignSkillToProtagonist(
            CombatSkillDefinition skill)
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:CardDefinition",
                         new[] { "Assets/StackCraft/Resources/Cards/Characters" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CardDefinition definition =
                    AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                if (definition == null)
                    continue;
                var serialized = new SerializedObject(definition);
                SerializedProperty skills = serialized.FindProperty("innateCombatSkills");
                bool isBaseProtagonist = path.EndsWith("Card_Villager.asset");
                skills.arraySize = isBaseProtagonist ? 1 : 0;
                if (isBaseProtagonist)
                    skills.GetArrayElementAtIndex(0).objectReferenceValue = skill;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }
        }

        private static void AssignMedicineItem(CombatItemDefinition item)
        {
            CardDefinition medicine =
                AssetDatabase.LoadAssetAtPath<CardDefinition>(MedicinePath);
            if (medicine == null)
                throw new System.InvalidOperationException("Card_Medicine asset is missing.");
            var serialized = new SerializedObject(medicine);
            serialized.FindProperty("combatItemDefinition").objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(medicine);
        }

        private static void InstallUi()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                TMP_Text template = root.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault();
                Transform oldLog = Find(root.transform, "CombatLogPanel");
                Transform oldHud = Find(root.transform, "CombatHudPanel");
                if (oldLog != null)
                    Object.DestroyImmediate(oldLog.gameObject);
                if (oldHud != null)
                    Object.DestroyImmediate(oldHud.gameObject);

                Transform mainPanel = Find(root.transform, "MainPanel") ?? root.transform;
                Toggle locationToggle = Find(root.transform, "LocationToggle")?
                    .GetComponent<Toggle>();
                TMP_Text toggleLabel = locationToggle != null
                    ? locationToggle.GetComponentInChildren<TMP_Text>(true)
                    : null;
                InfoPanel infoPanel = root.GetComponentInChildren<InfoPanel>(true);

                CombatLogPresenter log = CreateCombatLog(
                    root.transform,
                    template,
                    infoPanel);
                CreateCombatHud(
                    mainPanel,
                    template,
                    locationToggle,
                    toggleLabel);

                EditorUtility.SetDirty(log);
                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static CombatLogPresenter CreateCombatLog(
            Transform parent,
            TMP_Text template,
            InfoPanel infoPanel)
        {
            GameObject panelObject = CreateUiObject("CombatLogPanel", parent);
            RectTransform rect = (RectTransform)panelObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(18f, 106f);
            rect.sizeDelta = new Vector2(420f, 190f);
            Image background = panelObject.AddComponent<Image>();
            background.sprite = LoadSprite(PanelSpritePath);
            background.type = Image.Type.Sliced;
            background.color = new Color(0.025f, 0.06f, 0.11f, 0.97f);
            CanvasGroup group = panelObject.AddComponent<CanvasGroup>();

            TMP_Text title = CreateText(
                "Title",
                panelObject.transform,
                template,
                "战斗记录",
                24f,
                new Color(0.36f, 0.82f, 0.98f));
            SetRect(title.rectTransform, 18f, -12f, -18f, -46f);
            TMP_Text logText = CreateText(
                "LogText",
                panelObject.transform,
                template,
                string.Empty,
                18f,
                new Color(0.95f, 0.88f, 0.77f));
            SetRect(logText.rectTransform, 18f, -46f, -18f, -14f);
            logText.alignment = TextAlignmentOptions.BottomLeft;
            logText.enableWordWrapping = true;
            logText.lineSpacing = 4f;

            var coordinator = panelObject.AddComponent<HudMessageAreaCoordinator>();
            var coordinatorSerialized = new SerializedObject(coordinator);
            coordinatorSerialized.FindProperty("infoPanel").objectReferenceValue = infoPanel;
            coordinatorSerialized.FindProperty("combatLog").objectReferenceValue = group;
            coordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();

            var presenter = panelObject.AddComponent<CombatLogPresenter>();
            var view = panelObject.AddComponent<CombatLogView>();
            view.SetTextTarget(logText);
            EditorUtility.SetDirty(view);
            var presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("logText").objectReferenceValue = logText;
            presenterSerialized.FindProperty("view").objectReferenceValue = view;
            presenterSerialized.FindProperty("canvasGroup").objectReferenceValue = group;
            presenterSerialized.FindProperty("coordinator").objectReferenceValue = coordinator;
            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static void CreateCombatHud(
            Transform parent,
            TMP_Text template,
            Toggle locationToggle,
            TMP_Text toggleLabel)
        {
            GameObject panelObject = CreateUiObject("CombatHudPanel", parent);
            RectTransform rect = (RectTransform)panelObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            Image background = panelObject.AddComponent<Image>();
            background.sprite = LoadSprite(PanelSpritePath);
            background.type = Image.Type.Sliced;
            background.color = new Color(0.025f, 0.055f, 0.11f, 1f);
            CanvasGroup group = panelObject.AddComponent<CanvasGroup>();

            TMP_Text title = CreateText(
                "CombatTitle",
                panelObject.transform,
                template,
                "战斗指令",
                30f,
                new Color(0.37f, 0.83f, 0.98f));
            SetRect(title.rectTransform, 22f, -20f, -22f, -64f);
            TMP_Text actor = CreateText(
                "ActorStatus",
                panelObject.transform,
                template,
                "未选择角色",
                21f,
                new Color(0.96f, 0.87f, 0.71f));
            SetRect(actor.rectTransform, 22f, -74f, -22f, -174f);
            TMP_Text target = CreateText(
                "TargetStatus",
                panelObject.transform,
                template,
                "未选择敌人",
                19f,
                new Color(1f, 0.60f, 0.52f));
            SetRect(target.rectTransform, 22f, -178f, -22f, -224f);

            Button basic = CreateButton(
                "BasicAttackButton",
                panelObject.transform,
                template,
                "普通攻击",
                BlueButtonPath,
                new Vector2(22f, -236f),
                new Vector2(376f, 54f));
            var buttons = new Button[3];
            var labels = new TMP_Text[3];
            for (int index = 0; index < 3; index++)
            {
                buttons[index] = CreateButton(
                    $"SkillButton{index + 1}",
                    panelObject.transform,
                    template,
                    index == 0 ? "奋力一击  -2体力" : "技能",
                    index == 0 ? PurpleButtonPath : DarkButtonPath,
                    new Vector2(22f, -302f - index * 62f),
                    new Vector2(376f, 54f));
                labels[index] = buttons[index].GetComponentInChildren<TMP_Text>(true);
            }

            TMP_Text hint = CreateText(
                "CombatHint",
                panelObject.transform,
                template,
                "点击友方卡选择行动者，点击敌方卡选择目标。\n将角色拖出战斗框可尝试撤退。",
                17f,
                new Color(0.72f, 0.78f, 0.84f));
            SetRect(hint.rectTransform, 22f, -500f, -22f, -570f);

            var presenter = panelObject.AddComponent<CombatHudPresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("panel").objectReferenceValue = group;
            serialized.FindProperty("combatToggle").objectReferenceValue = locationToggle;
            serialized.FindProperty("combatToggleLabel").objectReferenceValue = toggleLabel;
            serialized.FindProperty("actorLabel").objectReferenceValue = actor;
            serialized.FindProperty("targetLabel").objectReferenceValue = target;
            serialized.FindProperty("basicAttackButton").objectReferenceValue = basic;
            SetObjectArray(serialized.FindProperty("skillButtons"), buttons);
            SetObjectArray(serialized.FindProperty("skillLabels"), labels);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray<T>(SerializedProperty property, T[] values)
            where T : Object
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            TMP_Text template,
            string text,
            string spritePath,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = buttonObject.AddComponent<Image>();
            image.sprite = LoadSprite(spritePath);
            image.type = Image.Type.Sliced;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            TMP_Text label = CreateText(
                "Label",
                buttonObject.transform,
                template,
                text,
                21f,
                Color.white);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_Text template,
            string text,
            float fontSize,
            Color color)
        {
            GameObject value = CreateUiObject(name, parent);
            var label = value.AddComponent<TextMeshProUGUI>();
            if (template != null)
            {
                label.font = template.font;
                label.fontSharedMaterial = template.fontSharedMaterial;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value;
        }

        private static void SetRect(
            RectTransform rect,
            float left,
            float top,
            float right,
            float bottom)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static Sprite LoadSprite(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
