using System.Collections.Generic;
using System.Linq;
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

        [System.Serializable]
        public struct WorldMapRoute
        {
            public int fromLocationIndex;
            public int toLocationIndex;
        }

        [Header("World locations")]
        [SerializeField] private List<WorldMapCardSpawn> locationSpawns = new();
        [SerializeField] private List<WorldMapRoute> routes = new();

        [Header("Party")]
        [SerializeField] private CardDefinition partyDefinition;
        [SerializeField] private int initialPartyLocationIndex;
        [SerializeField] private Vector3 partyDockOffset = new(0f, 0.01f, -0.55f);
        [SerializeField, Range(0.5f, 1f)] private float partyDockScale = 0.78f;
        [SerializeField, Min(0.1f)] private float destinationSnapRadius = 1.25f;
        [SerializeField, Min(0.1f)] private float partyTravelSpeed = 0.9f;

        [Header("Legacy save cleanup")]
        [SerializeField, HideInInspector] private CardDefinition legacyTravelerDefinition;
        [SerializeField, HideInInspector] private List<CardDefinition> legacyJobDefinitions = new();

        [Header("Board skin")]
        [SerializeField] private Texture2D worldMapTexture;
        [SerializeField] private Shader worldMapShader;
        [SerializeField] private float backgroundSurfaceOffset = -0.01f;

        private Material mapMaterial;
        private bool hasSpawned;
        private CardManager cardManager;
        private WorldMapLocation[] runtimeLocations;
        private WorldMapPartyController partyController;

        private void Awake()
        {
            cardManager = GetComponent<CardManager>();
            runtimeLocations = new WorldMapLocation[locationSpawns.Count];

            if (cardManager != null)
            {
                cardManager.OnCardCreated += ConfigureSpawnedCard;
                cardManager.OnStatsChanged += HandleStatsChanged;
            }
        }

        private void Start()
        {
            ApplyWorldMapBackground();
            ConfigureExistingCards();
            RemoveLegacyJobCards();
            RefreshPartyInfo("驻扎中");
        }

        private void OnDestroy()
        {
            if (cardManager != null)
            {
                cardManager.OnCardCreated -= ConfigureSpawnedCard;
                cardManager.OnStatsChanged -= HandleStatsChanged;
            }

            InfoPanel.Instance?.ClearInfoRequest(this);

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

            if (partyDefinition != null && locationSpawns.Count > 0)
            {
                int startIndex = Mathf.Clamp(initialPartyLocationIndex, 0, locationSpawns.Count - 1);
                Vector3 partyPosition = locationSpawns[startIndex].position + partyDockOffset;
                cardManager.CreateCardInstance(partyDefinition, partyPosition, CardStack.RefuseAll);
            }
        }

        public void ConfigureSpawnedCard(CardInstance card)
        {
            if (card == null || card.Definition == null || card.Stack == null)
                return;

            for (int index = 0; index < locationSpawns.Count; index++)
            {
                if (card.Definition != locationSpawns[index].definition)
                    continue;

                WorldMapLocation location = card.GetComponent<WorldMapLocation>();
                if (location == null)
                    location = card.gameObject.AddComponent<WorldMapLocation>();

                location.Initialize(index, card);
                EnsureRuntimeLocationCapacity();
                runtimeLocations[index] = location;

                if (partyController != null &&
                    partyController.PartyCard != null &&
                    partyController.CurrentLocationIndex == index)
                {
                    DockPartyAtLocation(index, partyController.PartyCard, instant: true);
                }
                return;
            }

            if (card.Definition != partyDefinition && card.Definition != legacyTravelerDefinition)
                return;

            partyController = card.GetComponent<WorldMapPartyController>();
            if (partyController == null)
                partyController = card.gameObject.AddComponent<WorldMapPartyController>();

            partyController.Initialize(
                this,
                card,
                destinationSnapRadius,
                partyTravelSpeed);
            RefreshPartyInfo("驻扎中");
        }

        public bool AreLocationsConnected(int firstIndex, int secondIndex)
        {
            if (!IsValidLocationIndex(firstIndex) || !IsValidLocationIndex(secondIndex))
                return false;

            return routes.Any(route =>
                (route.fromLocationIndex == firstIndex && route.toLocationIndex == secondIndex) ||
                (route.fromLocationIndex == secondIndex && route.toLocationIndex == firstIndex));
        }

        public int FindNearestLocationIndex(Vector3 worldPosition, float maxDistance)
        {
            int bestIndex = -1;
            float bestSqrDistance = float.IsPositiveInfinity(maxDistance)
                ? float.PositiveInfinity
                : maxDistance * maxDistance;

            for (int index = 0; index < locationSpawns.Count; index++)
            {
                float sqrDistance = (GetLocationWorldPosition(index) - worldPosition).sqrMagnitude;
                if (sqrDistance <= bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        public Vector3 GetLocationWorldPosition(int locationIndex)
        {
            if (!IsValidLocationIndex(locationIndex))
                return Vector3.zero;

            EnsureRuntimeLocationCapacity();
            WorldMapLocation location = runtimeLocations[locationIndex];
            if (location != null && location.Card?.Stack != null)
                return location.Card.Stack.TargetPosition.Flatten();

            return locationSpawns[locationIndex].position.Flatten();
        }

        public Vector3 GetPartyDockPosition(int locationIndex)
        {
            if (!IsValidLocationIndex(locationIndex))
                return Vector3.zero;

            EnsureRuntimeLocationCapacity();
            WorldMapLocation location = runtimeLocations[locationIndex];
            return location != null
                ? location.GetPartyDockWorldPosition(partyDockOffset)
                : GetLocationWorldPosition(locationIndex) + partyDockOffset;
        }

        public void DockPartyAtLocation(int locationIndex, CardInstance partyCard, bool instant)
        {
            if (!IsValidLocationIndex(locationIndex) || partyCard == null)
                return;

            DetachPartyFromLocation(partyCard);
            EnsureRuntimeLocationCapacity();
            WorldMapLocation location = runtimeLocations[locationIndex];
            if (location != null)
            {
                location.AttachParty(partyCard, partyDockOffset, partyDockScale, instant);
                return;
            }

            partyCard.Stack?.SetTargetPosition(GetPartyDockPosition(locationIndex), instant);
        }

        public void DetachPartyFromLocation(CardInstance partyCard)
        {
            if (partyCard == null)
                return;

            EnsureRuntimeLocationCapacity();
            foreach (WorldMapLocation location in runtimeLocations)
                location?.DetachParty(partyCard);
        }

        public string GetLocationName(int locationIndex)
        {
            if (!IsValidLocationIndex(locationIndex))
                return "未知地点";

            CardDefinition definition = locationSpawns[locationIndex].definition;
            return definition != null ? definition.DisplayName : "未知地点";
        }

        public void NotifyPartyStateChanged(WorldMapPartyController controller, string status)
        {
            if (controller != partyController)
                return;

            RefreshPartyInfo(status);
        }

        private bool IsValidLocationIndex(int index)
        {
            return index >= 0 && index < locationSpawns.Count;
        }

        private void EnsureRuntimeLocationCapacity()
        {
            if (runtimeLocations == null || runtimeLocations.Length != locationSpawns.Count)
                runtimeLocations = new WorldMapLocation[locationSpawns.Count];
        }

        private void ConfigureExistingCards()
        {
            if (cardManager == null)
                return;

            foreach (CardInstance card in cardManager.AllCards.ToList())
                ConfigureSpawnedCard(card);
        }

        private void RemoveLegacyJobCards()
        {
            if (cardManager == null || legacyJobDefinitions == null || legacyJobDefinitions.Count == 0)
                return;

            List<CardInstance> legacyCards = cardManager.AllCards
                .Where(card => card != null && legacyJobDefinitions.Contains(card.Definition))
                .ToList();

            foreach (CardInstance card in legacyCards)
                card.Stack?.DestroyCard(card);

            if (legacyCards.Count > 0)
                cardManager.NotifyStatsChanged();
        }

        private void HandleStatsChanged(StatsSnapshot _)
        {
            RefreshPartyInfo(partyController != null && partyController.IsTraveling ? "旅行中" : "驻扎中");
        }

        private void RefreshPartyInfo(string status)
        {
            if (InfoPanel.Instance == null || partyController == null || partyController.PartyCard == null)
                return;

            CardInstance partyCard = partyController.PartyCard;
            int maxHealth = partyCard.Stats != null ? partyCard.Stats.MaxHealth.Value : partyCard.CurrentHealth;
            string locationName = partyController.CurrentLocationIndex >= 0
                ? GetLocationName(partyController.CurrentLocationIndex)
                : "旅途中";
            string body =
                $"所在地点：{locationName}\n" +
                "成员：1 人\n" +
                $"生命：{partyCard.CurrentHealth}/{maxHealth}\n" +
                $"状态：{status}\n\n" +
                "将小队拖到相邻地点卡上开始旅行";

            InfoPanel.Instance.RequestInfoDisplay(
                this,
                InfoPriority.Hover,
                ("小队", body));
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

            if (Board.Instance != null)
            {
                Vector3 boardPosition = Board.Instance.transform.position;
                background.transform.position = new Vector3(
                    boardPosition.x,
                    boardPosition.y + backgroundSurfaceOffset,
                    boardPosition.z);
            }

            // The original grass plane is square. Match the 16:9 map art and camera
            // so the whole illustration is visible instead of cropping its center.
            // Unity's built-in Plane mesh is already 10x10 world units.
            background.transform.localScale = new Vector3(2f, 1f, 1.125f);

            if (Board.Instance != null)
            {
                Bounds visibleMapBounds = renderer.bounds;
                visibleMapBounds.center = new Vector3(
                    background.transform.position.x,
                    0f,
                    background.transform.position.z);
                visibleMapBounds.size = new Vector3(
                    visibleMapBounds.size.x,
                    0.1f,
                    visibleMapBounds.size.z);
                Board.Instance.SetWorldBoundsOverride(visibleMapBounds);
            }

            mapMaterial = new Material(worldMapShader) { name = "World Map Background Material" };
            mapMaterial.SetTexture("_MainTex", worldMapTexture);
            mapMaterial.SetTextureScale("_MainTex", Vector2.one);
            renderer.sharedMaterial = mapMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
