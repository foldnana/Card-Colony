using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
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
            activeDefinition = locationDefinitions.FirstOrDefault(definition =>
                definition != null && definition.Id == locationId);
            if (activeDefinition == null)
            {
                Debug.LogError($"Location definition '{locationId}' is not configured.");
                if (returnButton != null)
                    returnButton.interactable = false;
                return;
            }

            if (returnButton != null)
                returnButton.interactable = true;

            if (locationTitleLabel != null)
                locationTitleLabel.text = activeDefinition.DisplayName;

            ApplyBackground(activeDefinition.BackgroundTexture);
            InfoPanel.Instance?.SetWorldMapSuppressed(false);
            WorldMapPartyStatusView.Instance?.Hide();

            if (!wasLoaded)
                SpawnExpandedPartyMembers(GameDirector.Instance.GameData.PartyMembers);
        }

        public IReadOnlyList<CardInstance> SpawnExpandedPartyMembers(IEnumerable<CardData> memberData)
        {
            if (activeDefinition == null)
            {
                string locationId = GameDirector.Instance?.GameData?.ActiveLocationId;
                activeDefinition = locationDefinitions.FirstOrDefault(definition =>
                    definition != null && definition.Id == locationId);
            }

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
                CardDefinition definition = CardManager.Instance.GetDefinitionById(data.Id) ??
                    activeDefinition.ExpandedPartyMemberDefinition;
                if (definition == null)
                    continue;

                Vector3 position = activeDefinition.PartySpawnPosition +
                    Vector3.right * (index * activeDefinition.PartyMemberSpacing);
                CardInstance member = CardManager.Instance.CreateCardInstance(
                    definition,
                    position,
                    CardStack.RefuseAll);
                if (member == null)
                    continue;

                member.RestoreSavedStats(data);
                member.Stack.IsLocked = false;
                spawnedMembers.Add(member);
            }

            CardManager.Instance.NotifyStatsChanged();
            return spawnedMembers;
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
            GameDirector.Instance.ReturnToWorldMap(partyMembers);
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
