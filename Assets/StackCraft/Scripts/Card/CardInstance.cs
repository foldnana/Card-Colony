using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(MeshRenderer), typeof(BoxCollider))]
    public class CardInstance : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Fields & Properties
        [Header("Identification")]
        [SerializeField, Tooltip("The TextMeshPro component used to display the card's name.")]
        private TextMeshPro titleText;

        [Header("Stat Displays")]
        [SerializeField, Tooltip("The TextMeshPro component for displaying the card's sell price.")]
        private TextMeshPro priceText;

        [SerializeField, Tooltip("The TextMeshPro component for displaying the card's nutrition value.")]
        private TextMeshPro nutritionText;

        [SerializeField, Tooltip("The TextMeshPro component for displaying the card's current health.")]
        private TextMeshPro healthText;

        public CardDefinition Definition { get; protected set; }
        public string PersistentId { get; private set; }
        public CardSettings Settings { get; private set; }
        public CardStack Stack { get; set; }
        public Vector2 Size { get; private set; }

        public CombatStats Stats { get; private set; }
        public int UsesLeft { get; private set; }
        public int CurrentHealth { get; private set; }
        public int CurrentNutrition { get; private set; }
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int CurrentEnergy { get; private set; } = 4;
        public int MaxEnergy { get; private set; } = 4;
        public bool IsDowned { get; private set; }

        public CardDefinition BaseDefinition => EquipperComponent?.OriginalDefinition ?? Definition;
        public CardStack OriginalCraftingStack { get; set; }

        public CardCombatant Combatant { get; private set; }
        public CardEquipper EquipperComponent { get; private set; }
        public CardEquipment EquipmentComponent { get; private set; }

        public bool IsBeingDragged { get; set; }

        private Camera _mainCam;
        private MeshRenderer _renderer;
        private BoxCollider _col;

        private Tween _moveTween;
        private Tween _combatTween;
        private Tween _hurtTween;
        private Tween _levelUpTween;

        private Highlight _highlight;
        private Color _defaultCardColor = Color.white;
        private TextMeshPro _worldQuestMarker;

        private bool _isHovered;

        private Vector3 _dampVelocity;
        private Vector3 _dampedTargetPos;
        private bool _isFollowingDamped;
        #endregion

        #region Lifecycle & Initialization
        /// <summary>
        /// Fully initializes the card: setting definitions, generating stats, applying visuals,
        /// creating and registering its stack, and attempting to merge with any nearby compatible stack.
        /// </summary>
        /// <param name="definition">The data definition for this card.</param>
        /// <param name="settings">Runtime movement and behavior settings.</param>
        /// <param name="stackToIgnore">A specific stack to ignore when searching for nearby merge candidates.</param>
        /// <param name="registerWithManager">Whether this card belongs to the active world board.</param>
        public void Initialize(
            CardDefinition definition,
            CardSettings settings = null,
            CardStack stackToIgnore = null,
            bool registerWithManager = true)
        {
            _mainCam = Camera.main;
            _renderer = GetComponent<MeshRenderer>();
            _col = GetComponent<BoxCollider>();
            if (_renderer.material.HasProperty("_Color"))
                _defaultCardColor = _renderer.material.GetColor("_Color");

            Combatant = GetComponent<CardCombatant>();
            EquipperComponent = GetComponent<CardEquipper>();
            EquipmentComponent = GetComponent<CardEquipment>();

            gameObject.name = $"{(definition is PackDefinition ? "Pack" : "Card")}_{definition.DisplayName}";

            Definition = definition;
            PersistentId = System.Guid.NewGuid().ToString("N");
            Settings = settings;
            Size = new Vector2(_col.size.x, _col.size.z) + settings.Margin;

            Stats = definition.CreateCombatStats();
            ApplyProgressionModifiers();

            UsesLeft = (definition is PackDefinition packDefinition)
                ? packDefinition.Slots.Count
                : definition.Uses;

            CurrentHealth = Stats.MaxHealth.Value;
            CurrentNutrition = definition.Nutrition;

            UpdateProgressionTitle();

            UpdateStatDisplays();

            ApplyVisualTextures(_renderer.material, Definition);
            RefreshWorldQuestMarker();
            if (WorldQuestRuntime.Instance != null)
            {
                WorldQuestRuntime.Instance.OnQuestChanged +=
                    HandleWorldQuestChanged;
            }

            Stack = new CardStack(this, transform.position);
            if (registerWithManager)
            {
                CardManager.Instance?.RegisterStack(Stack);
                TryAttachToNearbyStack(settings.SpawnAttachRadius, stackToIgnore);
                CardManager.Instance?.ResolveOverlaps();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            InfoPanel.Instance?.RegisterHover(GetInfo());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            InfoPanel.Instance?.UnregisterHover();
        }

        private void OnDisable()
        {
            _isHovered = false;
            KillTweens();

            InfoPanel.Instance?.UnregisterHover();
        }

        private void OnDestroy()
        {
            if (WorldQuestRuntime.Instance != null)
            {
                WorldQuestRuntime.Instance.OnQuestChanged -=
                    HandleWorldQuestChanged;
            }
        }

        private void Update()
        {
            if (_isHovered && Stack != null && Stack.IsCrafting)
            {
                InfoPanel.Instance?.RegisterHover(GetInfo());
            }

            if (_isFollowingDamped)
            {
                transform.position = ExponentialMove(
                    transform.position,
                    _dampedTargetPos,
                    Settings.SwaySharpness,
                    Time.unscaledDeltaTime
                );

                if (Vector3.SqrMagnitude(transform.position - _dampedTargetPos) < 0.0001f)
                {
                    transform.position = _dampedTargetPos;
                    _isFollowingDamped = false;
                }
            }
        }
        #endregion

        #region Information & Visuals
        private void HandleWorldQuestChanged(WorldQuestViewModel quest)
        {
            RefreshWorldQuestMarker();
        }

        private void RefreshWorldQuestMarker()
        {
            string marker = string.Empty;
            Color markerColor = Color.white;
            bool visible = Definition != null &&
                WorldQuestRuntime.Instance != null &&
                WorldQuestRuntime.Instance.TryGetNpcMarker(
                    Definition.Id,
                    out marker,
                    out markerColor);
            if (!visible)
            {
                if (_worldQuestMarker != null)
                    _worldQuestMarker.gameObject.SetActive(false);
                return;
            }

            if (_worldQuestMarker == null && titleText != null)
            {
                _worldQuestMarker = Instantiate(
                    titleText,
                    titleText.transform.parent);
                _worldQuestMarker.name = "WorldQuestMarker";
                _worldQuestMarker.fontSize =
                    Mathf.Max(titleText.fontSize * 1.4f, 5f);
                _worldQuestMarker.fontStyle = FontStyles.Bold;
                _worldQuestMarker.alignment =
                    TextAlignmentOptions.Center;
                _worldQuestMarker.transform.localPosition =
                    titleText.transform.localPosition +
                    new Vector3(Size.x * 0.35f, 0.004f, 0f);
            }

            if (_worldQuestMarker != null)
            {
                _worldQuestMarker.text = marker;
                _worldQuestMarker.color = markerColor;
                _worldQuestMarker.gameObject.SetActive(true);
            }
        }

        private (string, string) GetInfo()
        {
            (string header, string body) info = ("", "");

            if (Stack == null) return info;

            if (Stack.IsCrafting)
            {
                var task = CraftingManager.Instance?.GetCraftingTask(Stack);

                info.header = task.Recipe.DisplayName;
                info.body = $"剩余时间：{task.Recipe.CraftingDuration - task.Progress:F1} 秒";
            }
            else if (Stack.Cards.Count > 1)
            {
                info.header = "卡牌堆";

                var grouped = Stack.Cards
                    .GroupBy(c => c.Definition)
                    .Select(g => new { g.Key.DisplayName, Count = g.Count() })
                    .ToList();

                for (int i = 0; i < grouped.Count; i++)
                {
                    var item = grouped[i];
                    info.body += $"{item.DisplayName} x{item.Count}";
                    info.body += i < grouped.Count - 1 ? ", " : ".";
                }
            }
            else if (Stack.TopCard != null)
            {
                info.header = Stack.TopCard.Definition.DisplayName;
                info.body = Stack.TopCard.Definition.Description;

                if (Stack.TopCard.Definition.Category is CardCategory.Character)
                {
                    info.body += $"\n生命（{CurrentHealth}/{Stack.TopCard.Stats.MaxHealth.Value}）";

                    if (ProtagonistRules.IsProtagonist(Stack.TopCard))
                    {
                        int required = CharacterProgressionService
                            .GetExperienceRequiredForNextLevel(Stack.TopCard.Level);
                        info.body +=
                            $"\n等级：{Stack.TopCard.Level}" +
                            $"\n经验：{Stack.TopCard.Experience}/{required}" +
                            $"\n体力：{Stack.TopCard.CurrentEnergy}/{Stack.TopCard.MaxEnergy}";
                        if (Stack.TopCard.IsDowned)
                            info.body += "\n状态：倒地";
                    }

                    if (Stack.TopCard.Definition.CombatType != CombatType.None)
                    {
                        info.body += $"\n{ChineseLocalization.CombatTypeName(Stack.TopCard.Definition.CombatType)}";
                        info.body += $"\n{Stack.TopCard.Stats.GetFormattedStats()}";
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Updates the visible TextMeshPro displays (price, nutrition, and health)
        /// to reflect the card's current stat values.
        /// </summary>
        public void UpdateStatDisplays()
        {
            if (priceText != null) priceText.text = Definition.SellPrice.ToString();
            if (nutritionText != null) nutritionText.text = CurrentNutrition.ToString();
            if (healthText != null) healthText.text = CurrentHealth.ToString();
        }

        /// <summary>
        /// Allows external components to safely update the price text.
        /// </summary>
        public void UpdatePriceText(string text)
        {
            if (priceText != null)
            {
                priceText.text = text;
            }
        }

        public void UpdateTitleText(string text)
        {
            if (titleText != null)
                titleText.text = text;
        }

        /// <summary>
        /// Controls the visual highlighting state of the card.
        /// Creates the necessary <see cref="Highlight"/> component if it does not already exist.
        /// </summary>
        /// <param name="value">If true, the card is highlighted; otherwise, the highlight is hidden.</param>
        public void SetHighlighted(bool value)
        {
            EnsureHighlight();
            _highlight.ResetColor();
            _highlight.SetActive(value);
        }

        /// <summary>
        /// Controls the card highlight and overrides its outline color for this card only.
        /// </summary>
        public void SetHighlighted(bool value, Color outlineColor)
        {
            EnsureHighlight();
            _highlight.SetColor(outlineColor);
            _highlight.SetActive(value);
        }

        private void EnsureHighlight()
        {
            if (_highlight != null)
                return;

            var mesh = GetComponent<MeshFilter>().sharedMesh;
            _highlight = new Highlight(transform, mesh, Settings.OutlineMaterial);
        }

        /// <summary>
        /// Instantiates the <see cref="PuffParticle"/> visual effect at the card's position.
        /// This effect typically plays when the card performs an action or is destroyed.
        /// </summary>
        public void PlayPuffParticle()
        {
            Instantiate(Settings.PuffParticle, transform.position, Quaternion.identity);
        }
        #endregion

        #region World Interactions
        /// <summary>
        /// Executes the process of consuming this card as food for a character.
        /// </summary>
        /// <param name="character">The <see cref="CardInstance"/> performing the consumption.</param>
        /// <param name="amountNeeded">The total amount of nutrition the character requires.</param>
        /// <param name="onConsumed">Action invoked with the actual amount of nutrition consumed.</param>
        /// <returns>An IEnumerator for use in a coroutine, handling animations and delays.</returns>
        public IEnumerator Consume(CardInstance character, int amountNeeded, System.Action<int> onConsumed)
        {
            CardStack oldStack = null;

            if (Stack != null && Stack.Cards.Count > 1)
            {
                if (Stack.IsCrafting)
                {
                    CraftingManager.Instance.StopCraftingTask(Stack);
                }

                oldStack = Stack;
                Vector3 logicalPosition = new Vector3(
                    transform.position.x,
                    oldStack.TargetPosition.y,
                    transform.position.z);
                Stack.RemoveCard(this);
                Stack = new CardStack(this, logicalPosition);
                CardManager.Instance.RegisterStack(Stack);
            }

            yield return transform.DOMoveY(Settings.DragHeight, 0.1f)
                .SetUpdate(true)
                .WaitForCompletion();

            Vector3 target = new Vector3(
                character.transform.position.x,
                Settings.DragHeight,
                character.transform.position.z
            );

            AudioManager.Instance?.PlaySFX(AudioId.CardSwipe);

            yield return transform.DOMove(target, 0.2f)
                .SetUpdate(true)
                .WaitForCompletion();

            AudioManager.Instance?.PlaySFX(AudioId.Eat);

            // If this card came from a stack, reapply the stack’s layout so remaining cards shift correctly.
            if (oldStack != null) oldStack.SetTargetPosition(oldStack.TargetPosition);

            yield return new WaitForSecondsRealtime(0.25f);

            int amountToEat = Mathf.Min(CurrentNutrition, amountNeeded);
            CurrentNutrition -= amountToEat;
            UpdateStatDisplays();

            onConsumed?.Invoke(amountToEat);

            if (CurrentNutrition <= 0)
            {
                Kill();
            }
            else if (Stack != null)
            {
                Stack.SetTargetPosition(Stack.TargetPosition);
                yield return new WaitForSecondsRealtime(0.25f);
                CardManager.Instance.ResolveOverlaps();
            }
        }

        /// <summary>
        /// Searches for nearby <see cref="CardStack"/> candidates within the specified radius 
        /// and attempts to merge the current card's stack into the best compatible candidate.
        /// </summary>
        /// <remarks>
        /// This process involves complex validation, including physical stacking rules, 
        /// crafting ingredient compatibility checks, and stack management logic (merging, 
        /// unregistering empty stacks, and recipe checking).
        /// </remarks>
        /// <param name="radius">The sphere radius used to search for nearby stacks.</param>
        /// <param name="stackToIgnore">A specific stack to exclude from being a merge candidate.</param>
        /// <returns>The <see cref="CardStack"/> the card successfully attached to, or null if no compatible stack was found.</returns>
        public CardStack TryAttachToNearbyStack(float radius, CardStack stackToIgnore = null)
        {
            if (stackToIgnore == CardStack.RefuseAll)
            {
                return null;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            CardStack bestCandidateStack = null;
            float bestSqrDist = float.MaxValue;

            HashSet<CardStack> checkedStacks = new HashSet<CardStack>();

            foreach (var hit in hits)
            {
                var otherCard = hit.GetComponent<CardInstance>();
                if (!IsWorldStackCandidate(otherCard)) continue;

                var candidateStack = otherCard.Stack;

                // 1. Basic Self/Null Checks
                if (candidateStack == null ||
                    candidateStack == Stack ||
                    candidateStack == stackToIgnore)
                    continue;

                // 2. Crafting Safety Check
                // If the candidate is crafting (and isn't the one we just unplugged from),
                // we need to verify if we are allowed to "feed" it.
                if (candidateStack.IsCrafting && candidateStack != OriginalCraftingStack)
                {
                    // Ask CraftingManager if this specific card is allowed to join the active task
                    if (!CraftingManager.Instance.CanJoinActiveCraft(candidateStack, Definition))
                    {
                        continue; // It's crafting and we aren't a valid ingredient. Block the merge.
                    }
                }

                // 3. Physical Stacking Check (Can a Wood card physically sit on a Sawmill?)
                bool protagonistInvolved =
                    ProtagonistRules.IsProtagonist(this) ||
                    candidateStack.Cards.Any(
                        ProtagonistRules.IsProtagonist);
                if (protagonistInvolved &&
                    Definition.Category == CardCategory.Character &&
                    candidateStack.BottomCard.Definition.Category ==
                        CardCategory.Character)
                {
                    continue;
                }

                if (!CanStack(Definition, candidateStack.BottomCard.Definition))
                    continue;

                // Skip if we already checked this stack
                if (!checkedStacks.Add(candidateStack)) continue;

                // Distance: compare against the closest card in the stack
                float sqrDist = float.MaxValue;
                foreach (var card in candidateStack.Cards)
                {
                    float d = (card.transform.position - transform.position).sqrMagnitude;
                    if (d < sqrDist) sqrDist = d;
                }

                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestCandidateStack = candidateStack;
                }
            }

            if (bestCandidateStack != null)
            {
                var droppedStack = Stack;

                // If we are merging back into the original crafting stack, RESUME it.
                if (bestCandidateStack == OriginalCraftingStack)
                {
                    CraftingManager.Instance.ResumeCraftingTask(OriginalCraftingStack);
                }
                // If the stack we're dropping was crafting, stop it since it's being merged.
                else if (droppedStack.IsCrafting)
                {
                    CraftingManager.Instance.StopCraftingTask(droppedStack);
                }

                bestCandidateStack.MergeWith(droppedStack);

                // Only unregister the stack if it has been fully absorbed or emptied.
                // If the interaction (like a Chest) left cards behind, we must keep the stack registered.
                if (droppedStack.Cards.Count == 0)
                {
                    CardManager.Instance.UnregisterStack(droppedStack);
                    bestCandidateStack.SetTargetPosition(bestCandidateStack.TargetPosition);
                }
                else
                {
                    // The stack still exists (e.g., partial deposit or full chest).
                    // We let the interaction logic (ChestLogic) or CardManager handle the overlap resolution,
                    // but we ensure the stack remains "alive" in the system.
                    CardManager.Instance.ResolveOverlaps();
                }

                // SAFE CHECK: Only look for new recipes if the stack is IDLE.
                // This prevents resetting the timer on active Workstations.
                if (!bestCandidateStack.IsCrafting)
                {
                    CraftingManager.Instance.CheckForRecipe(bestCandidateStack);
                }

                return bestCandidateStack;
            }

            return null;
        }

        private static bool IsWorldStackCandidate(CardInstance card)
        {
            if (card == null)
                return false;

            foreach (ICardStackRegistrationPolicy policy in
                     card.GetComponents<ICardStackRegistrationPolicy>())
            {
                if (!policy.RegisterSplitStacksWithWorld)
                    return false;
            }

            return true;
        }
        #endregion

        #region State Management
        /// <summary>
        /// Decrements the <see cref="UsesLeft"/> counter for the card.
        /// </summary>
        public void Use() => UsesLeft--;

        /// <summary>
        /// Increases the card's <see cref="CurrentHealth"/> by the specified amount and updates the stat displays.
        /// </summary>
        /// <param name="healAmount">The amount of health to restore.</param>
        public void Heal(int healAmount)
        {
            CurrentHealth += healAmount;
            UpdateStatDisplays();
        }

        /// <summary>
        /// Reduces the card's <see cref="CurrentHealth"/> by the specified damage amount,
        /// ensuring health does not drop below zero, and triggers visual effects (flash and shake).
        /// </summary>
        /// <param name="damage">The amount of damage to inflict.</param>
        public void TakeDamage(int damage)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            UpdateStatDisplays();

            _hurtTween?.Kill();

            var flashTween = _renderer.material
                .DOFloat(1f, "_FlashAmount", 0.1f)
                .SetDelay(0.05f)
                .SetLoops(2, LoopType.Yoyo);

            var shakeTween = transform
                .DOPunchRotation(new Vector3(0, 15, 0), 0.25f, vibrato: 25);

            _hurtTween = DOTween.Sequence()
                .Join(flashTween)
                .Join(shakeTween)
                .SetUpdate(true);
        }

        /// <summary>
        /// Destroys the card instance, notifying the <see cref="CardManager"/>, cleaning up combat status and equipment, 
        /// spawning loot if applicable, playing the puff particle effect, and finally destroying the card/stack.
        /// </summary>
        public void Kill()
        {
            if (ProtagonistRules.ShouldEnterDownedState(this))
            {
                EnterDownedState();
                return;
            }

            CardManager.Instance?.NotifyCardKilled(this);

            KillTweens();

            EquipperComponent?.UnequipAll();

            if (Combatant != null && Combatant.IsInCombat && Combatant.CurrentCombatTask != null)
            {
                Combatant.CurrentCombatTask.RemoveCombatant(this);
            }

            if (Definition.Category is CardCategory.Mob or CardCategory.Character)
            {
                CardManager.Instance?.CreateCardInstance(
                    Definition.GetRandomLoot(),
                    transform.position
                );
            }

            PlayPuffParticle();

            if (Stack != null) Stack.DestroyCard(this);
            else GameObject.Destroy(gameObject);
        }

        public void EnterDownedState()
        {
            if (IsDowned)
                return;

            IsDowned = true;
            CurrentHealth = 0;
            IsBeingDragged = false;
            KillTweens();

            if (Combatant != null)
            {
                CombatTask task = Combatant.CurrentCombatTask;
                if (Combatant.IsInCombat && task != null)
                    task.RemoveCombatant(this);
                Combatant.LeaveCombat();
            }

            if (Stack == null)
                CardManager.Instance?.ReturnCardToBoard(this);

            UpdateProgressionTitle();
            UpdateStatDisplays();
            GameDirector.Instance?.SyncProtagonistState(this);
            CardManager.Instance?.NotifyStatsChanged();
            ApplyDownedVisual();
        }

        public bool Revive(int restoredHealth, int restoredEnergy = 1)
        {
            if (!IsDowned)
                return false;

            IsDowned = false;
            CurrentHealth = Mathf.Clamp(
                Mathf.Max(1, restoredHealth),
                1,
                Stats.MaxHealth.Value);
            CurrentEnergy = Mathf.Clamp(
                Mathf.Max(0, restoredEnergy),
                0,
                MaxEnergy);
            UpdateProgressionTitle();
            UpdateStatDisplays();
            GameDirector.Instance?.SyncProtagonistState(this);
            CardManager.Instance?.NotifyStatsChanged();
            ApplyDownedVisual();
            return true;
        }

        public void ConfirmProtagonistDeath()
        {
            if (!IsDowned || !ProtagonistRules.IsProtagonist(this))
                return;

            GameDirector.Instance?.ConfirmProtagonistDeath("战斗重伤");
        }

        /// <summary>
        /// Toggles the visibility and collision state of the card and all its display components.
        /// </summary>
        /// <param name="value">If true<, the card is visible and collidable; otherwise, it is hidden.</param>
        public void SetVisible(bool value)
        {
            _renderer.enabled = value;
            _col.enabled = value;

            if (titleText != null) titleText.enabled = value;
            if (priceText != null) priceText.enabled = value;
            if (nutritionText != null) nutritionText.enabled = value;
            if (healthText != null) healthText.enabled = value;
        }

        /// <summary>
        /// Force-sets the card's definition, updates its combat stats, name, and art texture.
        /// Used primarily by <see cref="CardEquipper"/> when a card's class or type changes.
        /// </summary>
        /// <param name="newDefinition">The new definition to assign to the card.</param>
        public void SetDefinition(CardDefinition newDefinition)
        {
            Definition = newDefinition;
            Stats = Definition.CreateCombatStats();
            ApplyProgressionModifiers();
            UpdateProgressionTitle();
            if (_renderer != null)
            {
                ApplyVisualTextures(_renderer.material, Definition);
                ApplyDownedVisual();
            }
        }

        public static void ApplyVisualTextures(Material material, CardDefinition definition)
        {
            if (material == null || definition == null)
                return;

            if (definition.ArtTexture != null)
                material.SetTexture("_OverlayTex", definition.ArtTexture);

            if (definition.BaseTextureOverride != null)
                material.SetTexture("_BaseTex", definition.BaseTextureOverride);
        }

        /// <summary>
        /// Overwrites the card's current dynamic stats (UsesLeft, CurrentHealth, CurrentNutrition) 
        /// with values loaded from saved data and updates the displays.
        /// </summary>
        /// <param name="cardData">The data object containing the saved stat values.</param>
        public void RestoreSavedStats(CardData cardData)
        {
            if (cardData == null)
                return;

            if (!string.IsNullOrWhiteSpace(cardData.PersistentId))
                PersistentId = cardData.PersistentId;
            UsesLeft = cardData.UsesLeft;
            Level = Mathf.Max(1, cardData.Level);
            Experience = Mathf.Max(0, cardData.Experience);
            MaxEnergy = cardData.MaxEnergy > 0 ? cardData.MaxEnergy : 4;
            CurrentEnergy = Mathf.Clamp(cardData.CurrentEnergy, 0, MaxEnergy);
            IsDowned = cardData.IsDowned;
            Stats = Definition.CreateCombatStats();
            ApplyProgressionModifiers();
            CurrentHealth = Mathf.Max(0, cardData.CurrentHealth);
            CurrentNutrition = cardData.CurrentNutrition;

            UpdateProgressionTitle();
            UpdateStatDisplays();
            ApplyDownedVisual();

            if (gameObject.TryGetComponent<ChestLogic>(out var chest))
            {
                chest.RestoreCoins(cardData.StoredCoins);
            }
        }

        public void ClampCurrentHealthToMaximum()
        {
            CurrentHealth = Mathf.Clamp(
                CurrentHealth,
                0,
                Stats?.MaxHealth.Value ?? Mathf.Max(1, CurrentHealth));
            UpdateStatDisplays();
        }

        public CharacterProgressionResult GainExperience(int amount)
        {
            var state = new CardData
            {
                Level = Level,
                Experience = Experience,
                CurrentEnergy = CurrentEnergy,
                MaxEnergy = MaxEnergy
            };
            int previousLevel = Level;
            int previousMaxHealth = Stats.MaxHealth.Value;
            CharacterProgressionResult result =
                CharacterProgressionService.GrantExperience(state, amount);
            Level = state.Level;
            Experience = state.Experience;
            if (result.LevelsGained > 0)
            {
                Stats.MaxHealth.AddModifier(new ProgressionModifier(
                    CharacterProgressionService.GetMaxHealthBonus(Level) -
                    CharacterProgressionService.GetMaxHealthBonus(previousLevel)));
                Stats.Attack.AddModifier(new ProgressionModifier(
                    CharacterProgressionService.GetAttackBonus(Level) -
                    CharacterProgressionService.GetAttackBonus(previousLevel)));
                Stats.Defense.AddModifier(new ProgressionModifier(
                    CharacterProgressionService.GetDefenseBonus(Level) -
                    CharacterProgressionService.GetDefenseBonus(previousLevel)));
                CurrentHealth = Mathf.Min(
                    Stats.MaxHealth.Value,
                    CurrentHealth + Stats.MaxHealth.Value - previousMaxHealth);
            }

            UpdateProgressionTitle();
            UpdateStatDisplays();
            CardManager.Instance?.NotifyStatsChanged();
            return result;
        }

        public void PlayLevelUpFeedback()
        {
            if (_renderer == null)
                return;

            _levelUpTween?.Kill();
            var sequence = DOTween.Sequence()
                .Append(_renderer.material.DOFloat(
                    1f,
                    "_FlashAmount",
                    0.15f))
                .Append(_renderer.material.DOFloat(
                    0f,
                    "_FlashAmount",
                    0.35f))
                .SetLoops(2)
                .SetUpdate(true);

            if (titleText != null)
            {
                TextMeshPro popup = Instantiate(
                    titleText,
                    titleText.transform.parent);
                popup.name = "LevelUpPopup";
                popup.text = "升级！";
                popup.color = new Color(1f, 0.82f, 0.24f);
                popup.fontSize *= 1.15f;
                popup.transform.localPosition +=
                    new Vector3(0f, 0.02f, 0.5f);
                sequence.Join(
                    popup.transform.DOLocalMoveZ(
                        popup.transform.localPosition.z + 0.25f,
                        1.25f));
                sequence.Join(popup.DOFade(0f, 1.25f));
                sequence.OnKill(() =>
                {
                    if (popup != null)
                        Destroy(popup.gameObject);
                    if (_levelUpTween == sequence)
                        _levelUpTween = null;
                });
            }
            else
            {
                sequence.OnKill(() =>
                {
                    if (_levelUpTween == sequence)
                        _levelUpTween = null;
                });
            }

            _levelUpTween = sequence;
        }

        public void RestoreEnergy(int amount)
        {
            CurrentEnergy = Mathf.Clamp(CurrentEnergy + amount, 0, MaxEnergy);
        }

        public bool TrySpendEnergy(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (IsDowned || CurrentEnergy < amount)
                return false;

            CurrentEnergy -= amount;
            return true;
        }

        private void ApplyProgressionModifiers()
        {
            if (Stats == null || Definition == null ||
                Definition.Category != CardCategory.Character)
            {
                return;
            }

            Stats.MaxHealth.AddModifier(new ProgressionModifier(
                CharacterProgressionService.GetMaxHealthBonus(Level)));
            Stats.Attack.AddModifier(new ProgressionModifier(
                CharacterProgressionService.GetAttackBonus(Level)));
            Stats.Defense.AddModifier(new ProgressionModifier(
                CharacterProgressionService.GetDefenseBonus(Level)));
        }

        private void UpdateProgressionTitle()
        {
            if (titleText == null || Definition == null)
                return;

            if (!ProtagonistRules.IsProtagonist(this))
            {
                titleText.text = Definition.DisplayName;
                return;
            }

            titleText.text = IsDowned
                ? $"{Definition.DisplayName} Lv.{Level}【倒地】"
                : $"{Definition.DisplayName} Lv.{Level}";
        }

        private void ApplyDownedVisual()
        {
            if (_renderer == null || !Application.isPlaying)
                return;

            Material material = _renderer.material;
            if (!material.HasProperty("_Color"))
                return;

            material.SetColor(
                "_Color",
                IsDowned
                    ? Color.Lerp(
                        _defaultCardColor,
                        new Color(0.32f, 0.34f, 0.38f, 1f),
                        0.8f)
                    : _defaultCardColor);
        }

        private readonly struct ProgressionModifier : IStatModifier
        {
            public float Value { get; }

            public ProgressionModifier(float value)
            {
                Value = value;
            }
        }
        #endregion

        #region Helpers & Utilities
        private bool CanStack(CardDefinition bottom, CardDefinition top)
        {
            return CardManager.Instance.CanStack(bottom, top);
        }

        /// <summary>
        /// Moves the card to a specified target position using a DOTween animation,
        /// overriding any existing movement tweens.
        /// </summary>
        /// <param name="target">The world position to move the card to.</param>
        /// <param name="forceGround">If true, forces the Y position of the target to 0f.</param>
        public void SetTargetAnimated(Vector3 target, bool forceGround = false)
        {
            SetTargetAnimated(target, Settings.MoveDuration, forceGround);
        }

        public void SetTargetAnimated(
            Vector3 target,
            float duration,
            bool forceGround = false)
        {
            _isFollowingDamped = false;

            if (forceGround) target.y = 0f;
            KillTweens();
            _moveTween = transform.DOMove(target, Mathf.Max(0f, duration))
                .SetEase(Settings.MoveEase)
                .SetUpdate(true);

            if (Time.timeScale == 0f)
            {
                _moveTween.OnUpdate(() => Physics.SyncTransforms());
            }
        }

        /// <summary>
        /// Settles presentation height without cancelling combat, level-up,
        /// or other non-layout animation channels.
        /// </summary>
        public void SetPresentationTargetAnimated(Vector3 target, float duration)
        {
            _isFollowingDamped = false;
            _dampVelocity = Vector3.zero;
            _moveTween?.Kill();
            _moveTween = transform.DOMove(target, Mathf.Max(0f, duration))
                .SetEase(Settings.MoveEase)
                .SetUpdate(true);

            if (Time.timeScale == 0f)
                _moveTween.OnUpdate(() => Physics.SyncTransforms());
        }

        /// <summary>
        /// Immediately sets the card's position to the specified target, cancelling any active move tweens.
        /// </summary>
        /// <param name="target">The world position to place the card at.</param>
        /// <param name="forceGround">If true, forces the Y position of the target to 0f.</param>
        public void SetTargetInstant(Vector3 target, bool forceGround = false)
        {
            _isFollowingDamped = false;

            if (forceGround) target.y = 0f;
            KillTweens();
            transform.position = target;

            if (Time.timeScale == 0f) Physics.SyncTransforms();
        }

        /// <summary>
        /// Sets the target for the card to move towards using SmoothDamp.
        /// Used specifically for trailing cards during a drag.
        /// </summary>
        public void SetTargetDamped(Vector3 target)
        {
            KillTweens();
            _dampedTargetPos = target;
            _isFollowingDamped = true;
        }

        /// <summary>
        /// Stops only positional movement at the card's current world position.
        /// This is used before a parent surface starts moving so an old tween or
        /// damped drag target cannot pull the child back into the previous space.
        /// </summary>
        public void StopMovementAtCurrentPosition()
        {
            _isFollowingDamped = false;
            _dampVelocity = Vector3.zero;
            _dampedTargetPos = transform.position;
            _moveTween?.Kill();
            _moveTween = null;
        }

        private Vector3 ExponentialMove(Vector3 current, Vector3 target, float sharpness, float deltaTime)
        {
            float t = 1f - Mathf.Exp(-sharpness * deltaTime);
            return Vector3.LerpUnclamped(current, target, t);
        }

        /// <summary>
        /// Checks if the given card is categorized as a Character or a Mob,
        /// indicating it is a combat-capable entity.
        /// </summary>
        /// <param name="c">The <see cref="CardInstance"/> to check.</param>
        /// <returns>True if the card is a Character or Mob; otherwise, false.</returns>
        public bool IsCombatant(CardInstance c) =>
            c.Definition.Category == CardCategory.Character ||
            c.Definition.Category == CardCategory.Mob;

        /// <summary>
        /// Sets a new combat-related DOTween animation, first clearing any existing move or combat tweens.
        /// </summary>
        /// <param name="tween">The new DOTween animation to execute for combat visuals.</param>
        /// <returns>The combat Tween that was started.</returns>
        public Tween StartCombatTween(Tween tween)
        {
            KillTweens();

            _combatTween = tween;
            return _combatTween;
        }

        /// <summary>
        /// Safely stops and cleans up the active movement and combat DOTween animations.
        /// </summary>
        public void KillTweens()
        {
            _moveTween?.Kill();
            _combatTween?.Kill();
            _levelUpTween?.Kill();
        }
        #endregion
    }
}
