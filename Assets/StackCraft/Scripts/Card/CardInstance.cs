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
        public CardSettings Settings { get; private set; }
        public CardStack Stack { get; set; }
        public Vector2 Size { get; private set; }

        public CombatStats Stats { get; private set; }
        public int UsesLeft { get; private set; }
        public int CurrentHealth { get; private set; }
        public int CurrentNutrition { get; private set; }

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

        private Highlight _highlight;

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
        public void Initialize(CardDefinition definition, CardSettings settings = null, CardStack stackToIgnore = null)
        {
            _mainCam = Camera.main;
            _renderer = GetComponent<MeshRenderer>();
            _col = GetComponent<BoxCollider>();

            Combatant = GetComponent<CardCombatant>();
            EquipperComponent = GetComponent<CardEquipper>();
            EquipmentComponent = GetComponent<CardEquipment>();

            gameObject.name = $"{(definition is PackDefinition ? "Pack" : "Card")}_{definition.DisplayName}";

            Definition = definition;
            Settings = settings;
            Size = new Vector2(_col.size.x, _col.size.z) + settings.Margin;

            Stats = definition.CreateCombatStats();

            UsesLeft = (definition is PackDefinition packDefinition)
                ? packDefinition.Slots.Count
                : definition.Uses;

            CurrentHealth = Stats.MaxHealth.Value;
            CurrentNutrition = definition.Nutrition;

            titleText.text = Definition.DisplayName;

            UpdateStatDisplays();

            ApplyVisualTextures(_renderer.material, Definition);

            Stack = new CardStack(this, transform.position);
            CardManager.Instance?.RegisterStack(Stack);
            TryAttachToNearbyStack(settings.SpawnAttachRadius, stackToIgnore);
            CardManager.Instance?.ResolveOverlaps();
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
                Stack.RemoveCard(this);
                Stack = new CardStack(this, transform.position);
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
                Stack.SetTargetPosition(transform.position);
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
                if (otherCard == null) continue;

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
            if (titleText != null) titleText.text = Definition.DisplayName;
            if (_renderer != null)
                ApplyVisualTextures(_renderer.material, Definition);
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
            UsesLeft = cardData.UsesLeft;
            CurrentHealth = cardData.CurrentHealth;
            CurrentNutrition = cardData.CurrentNutrition;

            UpdateStatDisplays();

            if (gameObject.TryGetComponent<ChestLogic>(out var chest))
            {
                chest.RestoreCoins(cardData.StoredCoins);
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
            _isFollowingDamped = false;

            if (forceGround) target.y = 0f;
            KillTweens();
            _moveTween = transform.DOMove(target, Settings.MoveDuration)
                .SetEase(Settings.MoveEase)
                .SetUpdate(true);

            if (Time.timeScale == 0f)
            {
                _moveTween.OnUpdate(() => Physics.SyncTransforms());
            }
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
        }
        #endregion
    }
}
