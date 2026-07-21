using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/World/Location Definition", fileName = "Location_")]
    public sealed class LocationDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Texture2D backgroundTexture;
        [SerializeField] private Vector2 mapSize = new(48f, 32f);
        [SerializeField, Min(0.1f)] private float cameraMinDistance = 5f;
        [SerializeField, Min(0.1f)] private float cameraMaxDistance = 20f;
        [SerializeField, Min(0.1f)] private float cameraInitialDistance = 10f;
        [SerializeField, Min(0.1f)] private float cameraZoomSpeed = 1f;
        [SerializeField] private CardDefinition expandedPartyMemberDefinition;
        [SerializeField] private Vector3 partySpawnPosition = new(0f, 0f, -1.2f);
        [SerializeField, Min(0.1f)] private float partyMemberSpacing = 0.9f;

        public string Id => id;
        public string DisplayName => displayName;
        public Texture2D BackgroundTexture => backgroundTexture;
        public Vector2 MapSize => mapSize;
        public float CameraMinDistance => cameraMinDistance;
        public float CameraMaxDistance => cameraMaxDistance;
        public float CameraInitialDistance => cameraInitialDistance;
        public float CameraZoomSpeed => cameraZoomSpeed;
        public CardDefinition ExpandedPartyMemberDefinition => expandedPartyMemberDefinition;
        public Vector3 PartySpawnPosition => partySpawnPosition;
        public float PartyMemberSpacing => partyMemberSpacing;
    }
}
