using System;
using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public struct LocationCardSpawn
    {
        [SerializeField] private CardDefinition definition;
        [SerializeField] private Vector3 position;

        public CardDefinition Definition => definition;
        public Vector3 Position => position;
    }

    [Serializable]
    public struct LocationEntranceDefinition
    {
        [SerializeField] private CardDefinition sourceCardDefinition;
        [SerializeField] private string destinationLocationId;

        public CardDefinition SourceCardDefinition => sourceCardDefinition;
        public string DestinationLocationId => destinationLocationId;
    }

    [Serializable]
    public struct LocationRandomCardSpawn
    {
        [SerializeField] private CardDefinition definition;
        [SerializeField, Min(0)] private int minimumCount;
        [SerializeField, Min(0)] private int maximumCount;

        public CardDefinition Definition => definition;
        public int MinimumCount => minimumCount;
        public int MaximumCount => maximumCount;
    }

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
        [SerializeField] private List<LocationCardSpawn> initialCardSpawns = new();
        [SerializeField] private List<LocationEntranceDefinition> entrances = new();
        [Header("Random location content")]
        [SerializeField] private bool randomizeCardsOnEntry;
        [SerializeField] private Vector2 randomSpawnAreaCenter;
        [SerializeField] private Vector2 randomSpawnAreaSize = new(14f, 7f);
        [SerializeField, Min(0f)] private float randomSpawnMinSpacing = 0.8f;
        [SerializeField, Min(0f)] private float randomSpawnPartyClearance = 2f;
        [SerializeField] private List<LocationRandomCardSpawn> randomCardSpawns = new();

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
        public IReadOnlyList<LocationCardSpawn> InitialCardSpawns => initialCardSpawns;
        public IReadOnlyList<LocationEntranceDefinition> Entrances => entrances;
        public bool RandomizeCardsOnEntry => randomizeCardsOnEntry;
        public Vector2 RandomSpawnAreaCenter => randomSpawnAreaCenter;
        public Vector2 RandomSpawnAreaSize => randomSpawnAreaSize;
        public float RandomSpawnMinSpacing => randomSpawnMinSpacing;
        public float RandomSpawnPartyClearance => randomSpawnPartyClearance;
        public IReadOnlyList<LocationRandomCardSpawn> RandomCardSpawns => randomCardSpawns;
    }
}
