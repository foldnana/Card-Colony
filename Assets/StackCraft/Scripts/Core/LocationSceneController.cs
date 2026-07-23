using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    public readonly struct LocationRandomSpawnPlanItem
    {
        public LocationRandomSpawnPlanItem(
            CardDefinition definition,
            Vector3 position)
        {
            Definition = definition;
            Position = position;
        }

        public CardDefinition Definition { get; }
        public Vector3 Position { get; }
    }

    [DisallowMultipleComponent]
    public sealed class LocationSceneController : MonoBehaviour
    {
        [SerializeField] private List<LocationDefinition> locationDefinitions = new();
        [SerializeField] private Button returnButton;
        [SerializeField] private TMP_Text locationTitleLabel;
        [SerializeField] private Shader backgroundShader;
        [SerializeField] private float backgroundSurfaceOffset = -0.01f;

        private Material backgroundMaterial;
        private LocationDefinition activeDefinition;
        private bool isReturning;

        public LocationDefinition ActiveDefinition => activeDefinition;

        public static IReadOnlyList<LocationDefinition> MergeDefinitions(
            IEnumerable<LocationDefinition> configuredDefinitions,
            IEnumerable<LocationDefinition> discoveredDefinitions)
        {
            var merged = new List<LocationDefinition>();
            var knownIds = new HashSet<string>();

            void AddRange(IEnumerable<LocationDefinition> definitions)
            {
                if (definitions == null)
                    return;

                foreach (LocationDefinition definition in definitions)
                {
                    if (definition == null)
                        continue;

                    string key = string.IsNullOrWhiteSpace(definition.Id)
                        ? $"instance:{definition.GetInstanceID()}"
                        : definition.Id;
                    if (!knownIds.Add(key))
                        continue;

                    merged.Add(definition);
                }
            }

            AddRange(configuredDefinitions);
            AddRange(discoveredDefinitions);
            return merged;
        }

        private void Awake()
        {
            returnButton?.onClick.AddListener(ReturnToWorldMap);
            if (GameDirector.Instance != null)
                GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;
        }

        private void OnDestroy()
        {
            returnButton?.onClick.RemoveListener(ReturnToWorldMap);
            if (GameDirector.Instance != null)
                GameDirector.Instance.OnSceneDataReady -= HandleSceneDataReady;

            if (backgroundMaterial != null)
                Destroy(backgroundMaterial);
        }

        private void HandleSceneDataReady(SceneData _, bool wasLoaded)
        {
            string locationId = GameDirector.Instance?.GameData?.ActiveLocationId;
            IReadOnlyList<LocationDefinition> availableDefinitions =
                GetAvailableDefinitions();
            activeDefinition = availableDefinitions.FirstOrDefault(definition =>
                definition != null && definition.Id == locationId);
            if (activeDefinition == null)
            {
                Debug.LogError($"Location definition '{locationId}' is not configured.");
                if (returnButton != null)
                    returnButton.interactable = false;
                return;
            }

            if (returnButton != null)
            {
                returnButton.interactable = true;
                TMP_Text returnLabel = returnButton.GetComponentInChildren<TMP_Text>(true);
                if (returnLabel != null)
                {
                    returnLabel.text = GetReturnButtonLabel(
                        GameDirector.Instance?.GameData,
                        availableDefinitions);
                }
            }

            if (locationTitleLabel != null)
                locationTitleLabel.text = activeDefinition.DisplayName;

            ApplyBackground(activeDefinition.BackgroundTexture);
            InfoPanel.Instance?.SetWorldMapSuppressed(false);
            WorldMapPartyStatusView.Instance?.Hide();

            LocationTransitionReason transitionReason = GameDirector.Instance.GameData
                .ConsumeLocationTransitionReason();
            bool hasIncomingParty =
                transitionReason != LocationTransitionReason.None;
            bool shouldRandomize = ShouldRandomizeLocationCards(
                activeDefinition.RandomizeCardsOnEntry,
                wasLoaded,
                transitionReason);

            if (!wasLoaded)
            {
                SpawnInitialLocationCards();
                SpawnExpandedPartyMembers(GameDirector.Instance.GameData.PartyMembers);
                if (shouldRandomize)
                    SpawnRandomLocationCards();
            }
            else
            {
                StartCoroutine(EnsureInitialLocationCardsAfterRestore(
                    hasIncomingParty,
                    shouldRandomize));
            }
        }

        public static bool ShouldRandomizeLocationCards(
            bool randomizationEnabled,
            bool wasLoaded,
            LocationTransitionReason transitionReason)
        {
            return randomizationEnabled &&
                (!wasLoaded ||
                    transitionReason == LocationTransitionReason.WorldMapEntry);
        }

        public static IReadOnlyList<LocationRandomSpawnPlanItem> CreateRandomSpawnPlan(
            LocationDefinition definition,
            int seed)
        {
            return CreateRandomSpawnPlan(
                definition,
                seed,
                null,
                null);
        }

        public static IReadOnlyList<LocationRandomSpawnPlanItem> CreateRandomSpawnPlan(
            LocationDefinition definition,
            int seed,
            IEnumerable<Vector3> runtimeOccupiedPositions,
            IEnumerable<Vector3> partyPositions)
        {
            if (definition == null)
                return System.Array.Empty<LocationRandomSpawnPlanItem>();

            var random = new System.Random(seed);
            var result = new List<LocationRandomSpawnPlanItem>();
            var occupied = definition.InitialCardSpawns
                .Where(spawn => spawn.Definition != null)
                .Select(spawn => new Vector2(spawn.Position.x, spawn.Position.z))
                .ToList();
            if (runtimeOccupiedPositions != null)
            {
                occupied.AddRange(runtimeOccupiedPositions.Select(position =>
                    new Vector2(position.x, position.z)));
            }
            List<Vector2> partyOccupied = partyPositions?
                .Select(position => new Vector2(position.x, position.z))
                .ToList() ?? new List<Vector2>();
            occupied.AddRange(partyOccupied);
            Vector2 center = definition.RandomSpawnAreaCenter;
            Vector2 size = new(
                Mathf.Max(0f, definition.RandomSpawnAreaSize.x),
                Mathf.Max(0f, definition.RandomSpawnAreaSize.y));
            float minimumSpacing = Mathf.Max(0f, definition.RandomSpawnMinSpacing);
            float partyClearance = Mathf.Max(0f, definition.RandomSpawnPartyClearance);
            if (partyOccupied.Count == 0)
            {
                partyOccupied.Add(new Vector2(
                    definition.PartySpawnPosition.x,
                    definition.PartySpawnPosition.z));
            }

            foreach (LocationRandomCardSpawn rule in definition.RandomCardSpawns)
            {
                if (rule.Definition == null)
                    continue;

                int minimum = Mathf.Max(0, rule.MinimumCount);
                int maximum = Mathf.Max(minimum, rule.MaximumCount);
                int count = random.Next(minimum, maximum + 1);
                for (int index = 0; index < count; index++)
                {
                    if (!TryFindRandomSpawnPosition(
                        random,
                        center,
                        size,
                        occupied,
                        partyOccupied,
                        minimumSpacing,
                        partyClearance,
                        out Vector2 position))
                    {
                        continue;
                    }

                    occupied.Add(position);
                    result.Add(new LocationRandomSpawnPlanItem(
                        rule.Definition,
                        new Vector3(position.x, 0f, position.y)));
                }
            }

            return result;
        }

        private static bool TryFindRandomSpawnPosition(
            System.Random random,
            Vector2 center,
            Vector2 size,
            IReadOnlyList<Vector2> occupied,
            IReadOnlyList<Vector2> partyPositions,
            float minimumSpacing,
            float partyClearance,
            out Vector2 position)
        {
            position = center;
            const int maximumAttempts = 80;
            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                Vector2 candidate = new(
                    center.x + ((float)random.NextDouble() - 0.5f) * size.x,
                    center.y + ((float)random.NextDouble() - 0.5f) * size.y);
                if (partyClearance > 0f &&
                    partyPositions.Any(partyPosition =>
                        Vector2.Distance(candidate, partyPosition) < partyClearance))
                {
                    continue;
                }

                if (minimumSpacing <= 0f ||
                    occupied.All(position =>
                        Vector2.Distance(candidate, position) >= minimumSpacing))
                {
                    position = candidate;
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<CardInstance> SpawnRandomLocationCards()
        {
            ResolveActiveDefinition();
            if (activeDefinition == null || CardManager.Instance == null ||
                !activeDefinition.RandomizeCardsOnEntry)
            {
                return System.Array.Empty<CardInstance>();
            }

            List<CardInstance> existingCards = CardManager.Instance.AllCards
                .Where(card => card?.Stack != null)
                .ToList();
            IEnumerable<Vector3> occupiedPositions = existingCards
                .Where(card => card.Definition == null ||
                    card.Definition.Faction != CardFaction.Player)
                .Select(card => card.Stack.TargetPosition);
            IEnumerable<Vector3> partyPositions = existingCards
                .Where(card => card.Definition != null &&
                    card.Definition.Faction == CardFaction.Player)
                .Select(card => card.Stack.TargetPosition);
            IReadOnlyList<LocationRandomSpawnPlanItem> plan = CreateRandomSpawnPlan(
                activeDefinition,
                System.Guid.NewGuid().GetHashCode(),
                occupiedPositions,
                partyPositions);
            var spawnedCards = new List<CardInstance>(plan.Count);
            foreach (LocationRandomSpawnPlanItem item in plan)
            {
                if (item.Definition == null)
                    continue;

                CardInstance card = CardManager.Instance.CreateCardInstance(
                    item.Definition,
                    item.Position,
                    CardStack.RefuseAll);
                if (card == null)
                    continue;

                card.Stack.IsLocked = false;
                card.gameObject.AddComponent<LocationRandomSpawnMarker>();
                if (Board.Instance != null)
                {
                    Vector3 validPosition = Board.Instance.EnforcePlacementRules(
                        item.Position,
                        card.Stack);
                    card.Stack.SetTargetPosition(validPosition, instant: true);
                }
                spawnedCards.Add(card);
            }

            CardManager.Instance.ResolveOverlaps();
            CardManager.Instance.NotifyStatsChanged();
            return spawnedCards;
        }

        public IReadOnlyList<CardInstance> SpawnInitialLocationCards()
        {
            ResolveActiveDefinition();
            if (activeDefinition == null || CardManager.Instance == null)
                return System.Array.Empty<CardInstance>();

            IEnumerable<string> existingCardIds = CardManager.Instance.AllCards
                .Where(card => card?.Definition != null)
                .Select(card => card.Definition.Id);
            IReadOnlyList<LocationCardSpawn> missingSpawns = FindMissingInitialCardSpawns(
                activeDefinition,
                existingCardIds);

            var spawnedCards = new List<CardInstance>();
            foreach (LocationCardSpawn spawn in missingSpawns)
            {
                if (spawn.Definition == null)
                    continue;

                CardInstance card = CardManager.Instance.CreateCardInstance(
                    spawn.Definition,
                    spawn.Position,
                    CardStack.RefuseAll);
                if (card == null)
                    continue;

                card.Stack.IsLocked = false;
                spawnedCards.Add(card);
            }

            CardManager.Instance.ResolveOverlaps();
            ConfigureLocationCardBehaviours(CardManager.Instance.AllCards, activeDefinition);
            CardManager.Instance.NotifyStatsChanged();
            return spawnedCards;
        }

        public static void ConfigureLocationCardBehaviours(IEnumerable<CardInstance> cards)
        {
            ConfigureLocationCardBehaviours(cards, null);
        }

        public static void ConfigureLocationCardBehaviours(
            IEnumerable<CardInstance> cards,
            LocationDefinition locationDefinition)
        {
            if (cards == null)
                return;

            foreach (CardInstance card in cards)
            {
                if (card?.Definition == null)
                    continue;

                LocationEntranceDefinition configuredEntrance = locationDefinition?.Entrances
                    .FirstOrDefault(entrance => entrance.SourceCardDefinition != null &&
                        entrance.SourceCardDefinition.Id == card.Definition.Id) ?? default;
                if (configuredEntrance.SourceCardDefinition != null)
                {
                    LocationEntrance entrance = card.GetComponent<LocationEntrance>();
                    if (entrance == null)
                        entrance = card.gameObject.AddComponent<LocationEntrance>();
                    entrance.Configure(configuredEntrance.DestinationLocationId);
                }

                if (!card.Definition.AmbientNpcAiEnabled)
                    continue;

                LocationNpcActivity activity = card.GetComponent<LocationNpcActivity>();
                if (activity == null)
                    activity = card.gameObject.AddComponent<LocationNpcActivity>();

                LocationCardSpawn configuredSpawn = locationDefinition?.InitialCardSpawns
                    .FirstOrDefault(spawn => spawn.Definition != null &&
                        spawn.Definition.Id == card.Definition.Id) ?? default;
                Vector3 homePosition = configuredSpawn.Definition != null
                    ? configuredSpawn.Position
                    : card.Stack?.TargetPosition ?? card.transform.position;
                activity.Configure(
                    card,
                    homePosition,
                    card.Definition.AmbientWanderRadius,
                    card.Definition.AmbientMoveSpeed,
                    card.Definition.AmbientIdleRange);
            }
        }

        public static IReadOnlyList<LocationCardSpawn> FindMissingInitialCardSpawns(
            LocationDefinition definition,
            IEnumerable<string> existingCardIds)
        {
            if (definition == null)
                return System.Array.Empty<LocationCardSpawn>();

            var existing = new HashSet<string>(
                existingCardIds?.Where(id => !string.IsNullOrWhiteSpace(id)) ??
                Enumerable.Empty<string>());
            return definition.InitialCardSpawns
                .Where(spawn => spawn.Definition != null &&
                    !existing.Contains(spawn.Definition.Id))
                .ToList();
        }

        public static string GetReturnButtonLabel(
            GameData gameData,
            IEnumerable<LocationDefinition> definitions)
        {
            string parentId = gameData?.LocationHistory?.LastOrDefault();
            if (string.IsNullOrWhiteSpace(parentId))
                return "返回世界地图";

            string parentName = definitions?
                .FirstOrDefault(definition => definition != null && definition.Id == parentId)?
                .DisplayName;
            return string.IsNullOrWhiteSpace(parentName)
                ? "返回上一地点"
                : $"返回{parentName}";
        }

        private IEnumerator EnsureInitialLocationCardsAfterRestore(
            bool replacePlayerParty,
            bool randomizeLocationCards)
        {
            // CardManager restores saved stacks from the same scene-ready event.
            // Wait one frame so migration only fills cards missing from older saves.
            yield return null;
            if (replacePlayerParty)
                CombatManager.Instance?.EndAllCombats();
            SpawnInitialLocationCards();
            if (randomizeLocationCards)
                RemoveRandomLocationCards();
            if (replacePlayerParty)
                ReplacePlayerParty(GameDirector.Instance.GameData.PartyMembers);
            if (randomizeLocationCards)
                SpawnRandomLocationCards();
        }

        private void RemoveRandomLocationCards()
        {
            if (CardManager.Instance == null)
                return;

            List<CardInstance> generatedCards = CardManager.Instance.AllCards
                .Where(card => card != null &&
                    card.GetComponent<LocationRandomSpawnMarker>() != null)
                .ToList();
            foreach (CardInstance card in generatedCards)
                card.Stack?.DestroyCard(card);
        }

        public IReadOnlyList<CardInstance> SpawnExpandedPartyMembers(IEnumerable<CardData> memberData)
        {
            ResolveActiveDefinition();

            if (activeDefinition == null || CardManager.Instance == null)
                return System.Array.Empty<CardInstance>();

            List<CardData> members = memberData?.Where(data => data != null).ToList() ?? new();
            if (members.Count == 0 && activeDefinition.ExpandedPartyMemberDefinition != null)
            {
                CardDefinition fallback = activeDefinition.ExpandedPartyMemberDefinition;
                members.Add(new CardData
                {
                    Id = fallback.Id,
                    UsesLeft = fallback.Uses,
                    CurrentHealth = fallback.CreateCombatStats().MaxHealth.Value,
                    CurrentNutrition = fallback.Nutrition
                });
            }

            var spawnedMembers = new List<CardInstance>();
            for (int index = 0; index < members.Count; index++)
            {
                CardData data = members[index];
                Vector3 position = activeDefinition.PartySpawnPosition +
                    Vector3.right * (index * activeDefinition.PartyMemberSpacing);
                data.EquippedItems ??= new List<CardData>();
                CardInstance member = CardManager.Instance.RestoreCardFromData(data, position);
                if (member == null)
                    continue;

                member.Stack.IsLocked = false;
                spawnedMembers.Add(member);
            }

            CardManager.Instance.NotifyStatsChanged();
            return spawnedMembers;
        }

        public IReadOnlyList<CardInstance> ReplacePlayerParty(IEnumerable<CardData> memberData)
        {
            if (CardManager.Instance == null)
                return System.Array.Empty<CardInstance>();

            List<CardInstance> existingParty = CardManager.Instance.AllCards
                .Where(card => card?.Definition != null &&
                    card.Definition.Category == CardCategory.Character &&
                    card.Definition.Faction == CardFaction.Player)
                .ToList();
            foreach (CardInstance card in existingParty)
                card.Stack?.DestroyCard(card);

            return SpawnExpandedPartyMembers(memberData);
        }

        private void ResolveActiveDefinition()
        {
            if (activeDefinition != null)
                return;

            string locationId = GameDirector.Instance?.GameData?.ActiveLocationId;
            activeDefinition = GetAvailableDefinitions().FirstOrDefault(definition =>
                definition != null && definition.Id == locationId);
        }

        private IReadOnlyList<LocationDefinition> GetAvailableDefinitions()
        {
            return MergeDefinitions(
                locationDefinitions,
                Resources.LoadAll<LocationDefinition>("Locations"));
        }

        public void ReturnToWorldMap()
        {
            if (isReturning || GameDirector.Instance == null)
                return;

            isReturning = true;
            if (returnButton != null)
                returnButton.interactable = false;

            List<CardData> partyMembers = CardManager.Instance == null
                ? new List<CardData>()
                : CardManager.Instance.AllCards
                    .Where(card => card != null &&
                        card.Definition != null &&
                        card.Definition.Category == CardCategory.Character &&
                        card.Definition.Faction == CardFaction.Player)
                    .Select(card => new CardData(card))
                    .ToList();
            GameDirector.Instance.ReturnFromLocation(partyMembers);
        }

        private void ApplyBackground(Texture2D texture)
        {
            if (texture == null || backgroundShader == null)
                return;

            if (activeDefinition == null)
                return;

            Vector2 mapSize = activeDefinition.MapSize;
            if (mapSize.x <= 0f || mapSize.y <= 0f)
                mapSize = new Vector2(48f, 32f);

            GameObject background = GameObject.Find("Background");
            if (background == null || !background.TryGetComponent(out MeshRenderer renderer))
                return;

            if (Board.Instance != null)
            {
                SkinnedMeshRenderer defaultBoard = Board.Instance.GetComponent<SkinnedMeshRenderer>();
                if (defaultBoard != null)
                    defaultBoard.enabled = false;
            }

            // Unity's built-in Plane mesh is 10x10 world units. Scale it to the
            // location's configured wide map area so art and card bounds agree.
            background.transform.localScale = new Vector3(
                mapSize.x / 10f,
                1f,
                mapSize.y / 10f);

            backgroundMaterial = new Material(backgroundShader);
            backgroundMaterial.SetTexture("_MainTex", texture);
            backgroundMaterial.SetTextureScale("_MainTex", Vector2.one);
            renderer.sharedMaterial = backgroundMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (Board.Instance != null)
            {
                Vector3 boardPosition = Board.Instance.transform.position;
                background.transform.position = new Vector3(
                    boardPosition.x,
                    boardPosition.y + backgroundSurfaceOffset,
                    boardPosition.z);

                Board.Instance.SetWorldBoundsOverride(new Bounds(
                    new Vector3(boardPosition.x, 0f, boardPosition.z),
                    new Vector3(mapSize.x, 0.1f, mapSize.y)));
            }

            CameraController cameraController = FindObjectOfType<CameraController>(true);
            cameraController?.ConfigureZoom(
                activeDefinition.CameraMinDistance,
                activeDefinition.CameraMaxDistance,
                activeDefinition.CameraInitialDistance,
                activeDefinition.CameraZoomSpeed);
        }
    }
}
