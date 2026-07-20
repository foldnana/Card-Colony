using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Builds the fixed, native-card world map used when a new game enters Main.
    /// Saved games bypass this bootstrap and are restored by <see cref="CardManager"/>.
    /// </summary>
    public sealed class WorldMapBootstrap : MonoBehaviour
    {
        [System.Serializable]
        public struct WorldMapCardSpawn
        {
            public CardDefinition definition;
            public Vector3 position;
        }

        [Header("World locations")]
        [SerializeField] private List<WorldMapCardSpawn> locationSpawns = new();

        [Header("Player cards")]
        [SerializeField] private CardDefinition travelerDefinition;
        [SerializeField] private Vector3 travelerPosition = new(-2.8f, 0f, -1.15f);
        [SerializeField] private List<CardDefinition> jobDefinitions = new();
        [SerializeField] private Vector3 jobStackPosition = new(0.45f, 0f, -2.5f);

        [Header("Board skin")]
        [SerializeField] private Texture2D worldMapTexture;
        [SerializeField] private Shader worldMapShader;

        private Material mapMaterial;

        private bool hasSpawned;

        private void Start()
        {
            ApplyWorldMapBackground();
        }

        private void OnDestroy()
        {
            if (mapMaterial != null)
                Destroy(mapMaterial);
        }

        public void SpawnNewGame(CardManager cardManager)
        {
            if (hasSpawned || cardManager == null)
                return;

            hasSpawned = true;

            foreach (WorldMapCardSpawn spawn in locationSpawns)
            {
                if (spawn.definition != null)
                    cardManager.CreateCardInstance(spawn.definition, spawn.position, CardStack.RefuseAll);
            }

            if (travelerDefinition != null)
                cardManager.CreateCardInstance(travelerDefinition, travelerPosition, CardStack.RefuseAll);

            SpawnJobStack(cardManager);
        }

        private void SpawnJobStack(CardManager cardManager)
        {
            CardStack mainStack = null;

            foreach (CardDefinition definition in jobDefinitions)
            {
                if (definition == null)
                    continue;

                CardInstance card = cardManager.CreateCardInstance(
                    definition,
                    jobStackPosition,
                    CardStack.RefuseAll);
                if (card == null)
                    continue;

                if (mainStack == null)
                {
                    mainStack = card.Stack;
                    continue;
                }

                CardStack temporaryStack = card.Stack;
                temporaryStack.RemoveCard(card);
                mainStack.AddCard(card);
            }

            mainStack?.SetTargetPosition(jobStackPosition, instant: true);
        }

        private void ApplyWorldMapBackground()
        {
            if (worldMapTexture == null || worldMapShader == null)
                return;

            if (Board.Instance != null)
            {
                SkinnedMeshRenderer defaultBoard = Board.Instance.GetComponent<SkinnedMeshRenderer>();
                if (defaultBoard != null)
                    defaultBoard.enabled = false;
            }

            GameObject background = GameObject.Find("Background");
            if (background == null || !background.TryGetComponent(out MeshRenderer renderer))
                return;

            // The original grass plane is square. Match the 16:9 map art and camera
            // so the whole illustration is visible instead of cropping its center.
            // Unity's built-in Plane mesh is already 10x10 world units.
            background.transform.localScale = new Vector3(2f, 1f, 1.125f);

            mapMaterial = new Material(worldMapShader) { name = "World Map Background Material" };
            mapMaterial.SetTexture("_MainTex", worldMapTexture);
            mapMaterial.SetTextureScale("_MainTex", Vector2.one);
            renderer.sharedMaterial = mapMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
