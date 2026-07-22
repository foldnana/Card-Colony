using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Card", fileName = "Card_")]
    public class CardDefinition : ScriptableObject
    {
        // Identification
        [SerializeField, Tooltip("Unique identifier for this card. Automatically generated.")]
        private string id;

        [SerializeField, Tooltip("Readable name shown in UI and tooltips.")]
        private string displayName;

        [SerializeField, TextArea, Tooltip("Short description or flavor text displayed in tooltips.")]
        private string description;

        [SerializeField, Tooltip("Card art displayed in the card GameObject.")]
        private Texture2D artTexture;

        [SerializeField, Tooltip("Optional card base used only for this definition's visual presentation.")]
        private Texture2D baseTextureOverride;

        // Classification
        [SerializeField, Tooltip("Category that defines this card's type and gameplay behavior.")]
        private CardCategory category;

        [SerializeField, Tooltip("Faction this card belongs to (e.g., Player, Mob, Neutral).")]
        private CardFaction faction;

        [SerializeField, Tooltip("Static location content is saved on the local board but does not consume player survival resources or card capacity.")]
        private bool isLocationStatic;

        [SerializeField, Tooltip("Whether the player can pick up and drag this card.")]
        private bool playerDraggable = true;

        [SerializeField, Tooltip("Enables lightweight idle and wandering behaviour for a location NPC.")]
        private bool ambientNpcAiEnabled;

        [SerializeField, Min(0.1f), Tooltip("Maximum distance the location NPC may wander from its home position.")]
        private float ambientWanderRadius = 1f;

        [SerializeField, Min(0.05f), Tooltip("Movement speed used by the location NPC activity state machine.")]
        private float ambientMoveSpeed = 0.5f;

        [SerializeField, Tooltip("Minimum and maximum idle time between ambient NPC movements.")]
        private Vector2 ambientIdleRange = new(2f, 4f);

        [Header("Dialogue")]
        [SerializeField, Tooltip("Allows a player character to start a basic conversation with this neutral NPC.")]
        private bool dialogueEnabled;

        [SerializeField, TextArea(2, 4), Tooltip("First line shown when this NPC enters a conversation.")]
        private string dialogueOpeningText;

        [SerializeField, Tooltip("The player's single basic reply option used by the first dialogue implementation.")]
        private string dialogueReplyText;

        [SerializeField, TextArea(2, 4), Tooltip("NPC line shown after the player chooses the basic reply.")]
        private string dialogueResponseText;

        [SerializeField, Tooltip("The combat type for Rock-Paper-Scissors advantage.")]
        private CombatType combatType = CombatType.None;

        // Loot
        [SerializeField, Tooltip("Weighted list of possible cards this card can produce.")]
        private List<LootEntry> loot;

        // Aggressive Mob
        [SerializeField, Tooltip("FALSE = Passive Mob.")]
        private bool isAggressive = false;

        [SerializeField, Tooltip("The range at which this mob will detect player cards.")]
        private float aggroRadius = 5f;

        [SerializeField, Tooltip("The range at which this mob will stop moving and initiate combat.")]
        private float attackRadius = 1.5f;

        // Passive Mob
        [SerializeField, Tooltip("Card that this mob periodically creates (e.g., Egg, Milk, Wool). Only used on non-aggressive mobs.")]
        private CardDefinition produceCard;

        [SerializeField, Tooltip("Base time in seconds between produce spawns.")]
        private float produceInterval = 10f;

        // Trading
        [SerializeField, Tooltip("If checked, this card can be sold for coins.")]
        private bool isSellable = true;

        [SerializeField, Tooltip("Amount of coins gained when selling this card.")]
        private int sellPrice = 1;

        // Crafting
        [SerializeField, Tooltip("If true, this card has a specific amount of uses before breaking (e.g. Trees, Rocks). If false, it acts as a single item.")]
        private bool hasDurability = false;

        [SerializeField, Min(1), Tooltip("How many times this card can be used as a crafting ingredient.")]
        private int uses = 1;

        // Food
        [SerializeField, Tooltip("Amount of nutrition (health) restored when consumed.")]
        private int nutrition;

        // Stats
        [SerializeField, Tooltip("Maximum health value if this card represents a combatant.")]
        private int maxHealth = 15;

        [SerializeField, Tooltip("Base attack damage dealt by this card in combat.")]
        private int attack = 2;

        [SerializeField, Tooltip("Reduces incoming damage from attacks.")]
        private int defense = 1;

        [SerializeField, Tooltip("Number of attacks per second, in percent (%).")]
        private int attackSpeed = 100;

        [SerializeField, Tooltip("Chance to hit the target, in percent (%).")]
        private int accuracy = 95;

        [SerializeField, Tooltip("Chance to evade an incoming attack, in percent (%).")]
        private int dodge = 5;

        [SerializeField, Tooltip("Chance to land a critical hit, in percent (%).")]
        private int criticalChance = 5;

        [SerializeField, Tooltip("Damage multiplier for critical hits, in percent (%).")]
        private int criticalMultiplier = 150;

        // Equipment
        [SerializeField, Tooltip("Only applies if Card Category is Equipment.")]
        private EquipmentSlot equipmentSlot;

        [SerializeField, Tooltip("The list of stat modifications this equipment provides.")]
        private List<StatModifier> statModifiers;

        [SerializeField, Tooltip("If equipped, transforms the character into this new card definition.")]
        private CardDefinition classChangeResult;

        public string Id => id;
        public string DisplayName => ChineseLocalization.CardName(displayName);
        public string Description => ChineseLocalization.CardDescription(displayName, description);
        public Texture2D ArtTexture => artTexture;
        public Texture2D BaseTextureOverride => baseTextureOverride;

        public CardCategory Category => category;
        public CardFaction Faction => faction;
        public bool IsLocationStatic => isLocationStatic;
        public bool PlayerDraggable => playerDraggable;
        public bool AmbientNpcAiEnabled => ambientNpcAiEnabled;
        public float AmbientWanderRadius => ambientWanderRadius;
        public float AmbientMoveSpeed => ambientMoveSpeed;
        public Vector2 AmbientIdleRange => ambientIdleRange;
        public bool DialogueEnabled => dialogueEnabled;
        public string DialogueOpeningText => dialogueOpeningText;
        public string DialogueReplyText => dialogueReplyText;
        public string DialogueResponseText => dialogueResponseText;
        public CombatType CombatType => combatType;

        public bool IsAggressive => isAggressive;
        public float AggroRadius => aggroRadius;
        public float AttackRadius => attackRadius;

        public CardDefinition ProduceCard => produceCard;
        public float ProduceInterval => produceInterval;

        public bool IsSellable => isSellable;
        public int SellPrice => sellPrice;

        public int Uses => hasDurability ? uses : 1;

        public int Nutrition => nutrition;

        public EquipmentSlot EquipmentSlot => equipmentSlot;
        public List<StatModifier> StatModifiers => statModifiers;
        public CardDefinition ClassChangeResult => classChangeResult;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = System.Guid.NewGuid().ToString("N");
        }

        public CombatStats CreateCombatStats()
        {
            return new CombatStats(
                maxHealth, attack, defense, attackSpeed,
                accuracy, dodge, criticalChance, criticalMultiplier
            );
        }

        public CardDefinition GetRandomLoot()
        {
            if (loot == null || loot.Count == 0) return null;

            int totalWeight = 0;
            foreach (var entry in loot)
            {
                if (entry != null && entry.Weight > 0)
                {
                    totalWeight += entry.Weight;
                }
            }

            if (totalWeight <= 0) return null;

            int randomPoint = Random.Range(0, totalWeight);

            foreach (var entry in loot)
            {
                if (entry == null || entry.Weight <= 0) continue;

                if (randomPoint < entry.Weight)
                {
                    return entry.Card;
                }

                randomPoint -= entry.Weight;
            }

            return null; // Fallback (should not be hit).
        }

        public void SetId(string id)
        {
            this.id = id;
        }

        public void SetDisplayName(string displayName)
        {
            this.displayName = displayName;
        }

        public void SetDescription(string description)
        {
            this.description = description;
        }
    }

    [System.Serializable]
    public class LootEntry
    {
        public CardDefinition Card;
        [Min(1)] public int Weight = 1;
    }

    public enum CardCategory
    {
        None,       // Non-card (e.g. Pack)
        Resource,   // Tree, Rock
        Character,  // Villager, Warrior, Archer, Mage
        Consumable, // Food, Potion
        Material,   // Wood, Stone, Branch
        Equipment,  // Weapon, Armor, Accessory
        Structure,  // Yard, House
        Currency,   // Coin, Gem
        Recipe,     // Recipe exclusive
        Mob,        // Chicken, Cow, Slime, Goblin
        Area,       // Area exclusive
        Valuable    // Treasure Chest, Keys, Artifacts
        // ...
    }

    public enum CardFaction
    {
        Neutral,
        Player,
        Mob
    }

    public enum CombatType
    {
        None,
        Melee,
        Ranged,
        Magic
    }

    public enum EquipmentSlot
    {
        Weapon,
        Armor,
        Accessory
    }
}
