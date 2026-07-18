using UnityEngine;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Card Settings")]
    public class CardSettings : ScriptableObject
    {
        [Header("Game Rules & Economy")]
        [SerializeField, Tooltip("The starting limit for how many cards the player can have.")]
        private int baseCardLimit = 24;

        [SerializeField, Tooltip("The amount of nutrition each Character card requires per feeding cycle.")]
        private int hungerPerCharacter = 2;

        [Header("Input & Interaction")]
        [SerializeField, Tooltip("How far the mouse must move to be considered a 'drag' instead of a 'click'.")]
        private float clickThreshold = 0.02f;

        [SerializeField, Tooltip("The height the card lifts to when dragged.")]
        private float dragHeight = 0.1f;

        [Header("Physics & Stacking Logic")]
        [SerializeField, Tooltip("How close you need to drop a card to attach to another.")]
        private float attachRadius = 0.25f;

        [SerializeField, Tooltip("The wider radius used to find a stack automatically when a card is first spawned.")]
        private float spawnAttachRadius = 1f;

        [SerializeField, Tooltip("Extra space added to the card's collider size, used for layout and stacking calculations.")]
        private Vector2 margin = new Vector2(0.1f, 0.1f);

        [SerializeField, Tooltip("Maximum iterations per resolve call to avoid infinite loops.")]
        private int maxIterations = 8;

        [Header("Visual Layout & Animation")]
        [SerializeField, Tooltip("Per-card visual offset within a stack.")]
        private Vector3 stackStep = new Vector3(0f, 0.002f, -0.18f);

        [SerializeField, Tooltip("How long it takes to tween into the resolved position.")]
        private float moveDuration = 0.1f;

        [SerializeField, Tooltip("Ease type for smooth movement.")]
        private Ease moveEase = Ease.OutQuad;

        [SerializeField, Range(50f, 150f), Tooltip("How quickly a card follows its target. Higher values make the stack tighter and more responsive.")]
        private float swaySharpness = 100f;

        [Header("Visual Effects")]
        [SerializeField, Tooltip("Puff particle that plays when the card performs an action.")]
        private PuffParticle puffParticle;

        [SerializeField, Tooltip("Material used by the highlight system to draw the card outline.")]
        private Material outlineMaterial;

        [Header("Mob AI")]
        [SerializeField, Tooltip("The time in seconds between each automatic move attempt for a mob card.")]
        private float moveInterval = 5f;

        [SerializeField, Tooltip("The maximum distance a mob will travel during a single random move or when pursuing a target.")]
        private float moveRadius = 1f;

        [SerializeField, Tooltip("How many times the mob will try to find a valid random position before giving up on the current move.")]
        private int maxAttemptsPerMove = 5;

        public int BaseCardLimit => baseCardLimit;
        public int HungerPerCharacter => hungerPerCharacter;

        public float ClickThreshold => clickThreshold;
        public float DragHeight => dragHeight;

        public float AttachRadius => attachRadius;
        public float SpawnAttachRadius => spawnAttachRadius;
        public Vector2 Margin => margin;
        public int MaxIterations => maxIterations;

        public Vector3 StackStep => stackStep;
        public float MoveDuration => moveDuration;
        public Ease MoveEase => moveEase;
        public float SwaySharpness => swaySharpness;

        public PuffParticle PuffParticle => puffParticle;
        public Material OutlineMaterial => outlineMaterial;

        public float MoveInterval => moveInterval;
        public float MoveRadius => moveRadius;
        public int MaxAttemptsPerMove => maxAttemptsPerMove;
    }
}
