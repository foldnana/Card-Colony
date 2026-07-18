using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

namespace CryingSnow.StackCraft
{
    public class PackVendor : TradeZone
    {
        [Header("UI Components")]
        [SerializeField, Tooltip("The TextMeshPro component used to display the name of the offered card pack.")]
        private TextMeshPro titleText;

        [SerializeField, Tooltip("The TextMeshPro component used to display the remaining cost to purchase the pack.")]
        private TextMeshPro priceText;

        [SerializeField, Tooltip("The TextMeshPro component used to display the progress of card/recipe discovery.")]
        private TextMeshPro trackerText;

        public string PackId => offeredPack != null ? offeredPack.Id : "";
        public int PaidAmount => paidAmount;

        private PackDefinition offeredPack;

        private bool isActive;
        private int buyPrice;
        private int paidAmount;

        private void Start()
        {
            if (CraftingManager.Instance != null)
            {
                CraftingManager.Instance.OnRecipeDiscovered += OnCollectionDataChanged;
            }

            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnStatsChanged += OnCollectionDataChanged;
            }
        }

        private void OnDestroy()
        {
            if (CraftingManager.Instance != null)
            {
                CraftingManager.Instance.OnRecipeDiscovered -= OnCollectionDataChanged;
            }

            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnStatsChanged -= OnCollectionDataChanged;
            }
        }

        private void OnCollectionDataChanged(string _)
        {
            UpdateCollectionTracker();
        }

        private void OnCollectionDataChanged(StatsSnapshot _)
        {
            UpdateCollectionTracker();
        }

        public override void Initialize(CardDefinition definition, Vector3 spawnOffset)
        {
            base.Initialize(definition, spawnOffset);

            if (definition is PackDefinition packDef)
            {
                this.offeredPack = packDef;
            }
            else
            {
                Debug.LogError($"Wrong definition type for PackVendor. Expected {typeof(PackDefinition)} but got {definition.GetType()}", this);
            }
        }

        public void RestoreState(int loadedPaidAmount, int currentQuestCount)
        {
            this.paidAmount = loadedPaidAmount;

            if (!isActive && offeredPack != null && currentQuestCount >= offeredPack.MinQuests)
            {
                isActive = true;
                buyPrice = offeredPack.BuyPrice;

                titleText.text = offeredPack.DisplayName;
                UpdatePriceText();
                UpdateCollectionTracker();
            }
            else if (isActive)
            {
                UpdatePriceText();
            }
        }

        private void UpdatePriceText()
        {
            if (priceText != null)
                priceText.text = $"价格：{buyPrice - paidAmount}";
        }

        public bool TryActivate(int completedQuests)
        {
            if (isActive || offeredPack == null) return false;
            if (completedQuests < offeredPack.MinQuests) return false;

            isActive = true;
            buyPrice = offeredPack.BuyPrice;

            titleText.text = offeredPack.DisplayName;
            UpdatePriceText();
            UpdateCollectionTracker();

            return true;
        }

        private void UpdateCollectionTracker()
        {
            if (trackerText == null || offeredPack == null || !isActive) return;

            // 1. Gather all unique possibilities from this pack
            HashSet<CardDefinition> possibleCards = new HashSet<CardDefinition>();
            HashSet<string> possibleRecipeIds = new HashSet<string>();

            foreach (var slot in offeredPack.Slots)
            {
                // Gather Weighted Cards
                if (slot.Entries != null)
                {
                    foreach (var entry in slot.Entries)
                    {
                        if (entry != null && entry.Card != null)
                        {
                            possibleCards.Add(entry.Card);
                        }
                    }
                }

                // Gather Recipes
                if (slot.PossibleRecipes != null)
                {
                    foreach (var recipe in slot.PossibleRecipes)
                    {
                        if (recipe != null)
                        {
                            possibleRecipeIds.Add(recipe.Id);
                        }
                    }
                }
            }

            int totalItems = possibleCards.Count + possibleRecipeIds.Count;
            int foundItems = 0;

            // 2. Check Card Discovery (Requires CardManager support)
            foreach (var card in possibleCards)
            {
                if (CardManager.Instance != null && CardManager.Instance.IsCardDiscovered(card))
                {
                    foundItems++;
                }
            }

            // 3. Check Recipe Discovery
            foreach (var recipeId in possibleRecipeIds)
            {
                if (CraftingManager.Instance != null && CraftingManager.Instance.IsRecipeDiscovered(recipeId))
                {
                    foundItems++;
                }
            }

            // 4. Update UI Text
            if (totalItems == 0)
            {
                trackerText.text = "";
            }
            else if (foundItems >= totalItems)
            {
                trackerText.text = "<color=#FFD700>已集齐</color>";
            }
            else
            {
                trackerText.text = $"已发现：\n{foundItems}/{totalItems}";
            }
        }

        public override bool CanTrade(CardStack droppedStack)
        {
            if (!isActive) return false;

            return droppedStack.Cards.All(card =>
            {
                // It's a currency card, that's valid.
                if (card.Definition.Category is CardCategory.Currency)
                {
                    return true;
                }

                // It's a chest, check if it has coins.
                if (card.TryGetComponent<ChestLogic>(out var chestLogic))
                {
                    // It's a valid payment method if it has at least 1 coin.
                    return chestLogic.StoredCoins > 0;
                }

                // It's not currency and not a (valid) chest.
                return false;
            });
        }

        protected override void ProcessTransaction(CardStack droppedStack)
        {
            bool statsChanged = false;

            // Keep processing as long as we haven't paid and there are cards in the stack
            while (paidAmount < buyPrice && droppedStack.Cards.Count > 0)
            {
                var bottomCard = droppedStack.BottomCard;

                if (bottomCard == null) break; // Safety check

                if (bottomCard.Definition.Category is CardCategory.Currency)
                {
                    // It's a coin. Consume it.
                    droppedStack.DestroyCard(bottomCard);
                    paidAmount++;
                    statsChanged = true;
                }
                else if (bottomCard.TryGetComponent<ChestLogic>(out var chestLogic))
                {
                    // It's a chest. Try to withdraw one coin WITHOUT spawning it.
                    if (chestLogic.TryWithdrawCoin(false))
                    {
                        // Success!
                        paidAmount++;
                        statsChanged = true;
                    }
                    else
                    {
                        // This chest is empty. Stop processing.
                        break;
                    }
                }
                else
                {
                    // Not a coin and not a chest. Stop processing.
                    break;
                }
            }

            if (paidAmount >= buyPrice)
            {
                CardManager.Instance?.CreatePackInstance(offeredPack, spawnPosition);
                paidAmount = 0;
                TradeManager.Instance?.NotifyPackPurchased(offeredPack);
            }

            if (statsChanged)
            {
                CardManager.Instance?.NotifyStatsChanged();
            }

            UpdatePriceText();

            AudioManager.Instance?.PlaySFX(AudioId.CashRegister);
        }

        public override (string, string) GetInfo()
        {
            if (!isActive || offeredPack == null) return ("", "");

            return ("卡包商人", $"可以在这里购买{offeredPack.DisplayName}卡包。");
        }
    }
}
