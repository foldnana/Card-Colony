using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class TradeManager : MonoBehaviour
    {
        public static TradeManager Instance { get; private set; }

        public event System.Action<CardStack> OnCardsSold;
        public void NotifyCardsSold(CardStack stack) => OnCardsSold?.Invoke(stack);

        public event System.Action<PackDefinition> OnPackPurchased;
        public void NotifyPackPurchased(PackDefinition pack) => OnPackPurchased?.Invoke(pack);

        [Header("Buyer")]
        [SerializeField, Tooltip("Prefab for the Card Buyer trade zone.")]
        private CardBuyer buyerPrefab;

        [SerializeField, Tooltip("The CardDefinition to be used as currency for trading.")]
        private CardDefinition currencyCard;

        [Header("Vendor")]
        [SerializeField, Tooltip("Prefab for the Pack Vendor trade zone.")]
        private PackVendor vendorPrefab;

        [SerializeField, Tooltip("The list of card packs to offer. Each item creates one vendor.")]
        private List<PackDefinition> offeredPacks;

        [Header("Layout")]
        [SerializeField, Tooltip("The horizontal distance between the centers of each trade zone.")]
        private float spacing = 1.1f;

        [SerializeField, Tooltip("The local scale to apply to each instantiated zone prefab.")]
        private Vector3 zoneScale = new Vector3(1.125f, 1.0f, 1.125f);

        [SerializeField, Tooltip("The local offset from the zone's center where cards will spawn.")]
        private Vector3 spawnOffset = new Vector3(0f, 0f, -1.4f);

        public CardDefinition CurrencyCard => currencyCard;

        private readonly List<TradeZone> zones = new();
        private readonly List<PackVendor> vendors = new();
        private readonly List<TradeZone> highlightedZones = new();

        private CameraController cameraController;

        private Dictionary<string, int> savedPaymentMap = new();

        private readonly Queue<PackVendor> activationQueue = new();
        private Coroutine activeSequenceCoroutine = null;

        private readonly object sequenceRequester = "VendorSequenceRequester";
        private readonly object tradeSequenceLock = "TradeSequenceLock";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Instantiate Buyer
            var buyer = Instantiate(buyerPrefab, transform);
            buyer.Initialize(currencyCard, spawnOffset);
            zones.Add(buyer);

            // Instantiate Vendors
            for (int i = 0; i < offeredPacks.Count; i++)
            {
                var vendor = Instantiate(vendorPrefab, transform);
                vendor.Initialize(offeredPacks[i], spawnOffset);
                zones.Add(vendor);
                vendors.Add(vendor);
            }

            UpdateZoneLayout();

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;
            }
        }

        private void Start()
        {
            cameraController = FindFirstObjectByType<CameraController>();

            if (Board.Instance != null)
            {
                Board.Instance.OnBoundsUpdated += HandleBoundsUpdated;
                HandleBoundsUpdated(Board.Instance.WorldBounds);
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
            }

            RestoreVendors();
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady -= HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;
            }

            if (Board.Instance != null)
            {
                Board.Instance.OnBoundsUpdated -= HandleBoundsUpdated;
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
            }
        }

        private void UpdateZoneLayout()
        {
            if (zones.Count == 0) return;

            float totalWidth = (zones.Count - 1) * spacing;
            float startX = -(totalWidth / 2f);

            for (int i = 0; i < zones.Count; i++)
            {
                float newX = startX + (i * spacing);
                zones[i].transform.localPosition = new Vector3(newX, 0, 0);
                zones[i].transform.localScale = zoneScale;
            }
        }

        private void HandleBeforeSave(GameData gameData)
        {
            if (gameData.TryGetScene(out var sceneData))
            {
                sceneData.SaveVendors(vendors);
            }
        }

        private void HandleSceneDataReady(SceneData sceneData, bool wasLoaded)
        {
            if (!wasLoaded) return;

            if (sceneData.SavedVendors != null)
            {
                foreach (var vData in sceneData.SavedVendors)
                {
                    savedPaymentMap[vData.PackId] = vData.PaidAmount;
                }
            }

            // Vendor restoration is intentionally deferred.
            // OnSceneDataReady is triggered by SceneManager.onSceneLoaded (before Start),
            // while QuestManager, CardManager, and CraftingManager are still rebuilding
            // runtime state from save data.
            //
            // Vendor logic depends on finalized quest progress and collections,
            // so restoration is performed in Start() instead.
        }

        private void RestoreVendors()
        {
            int currentQuestCount = QuestManager.Instance.CompletedQuestsCount;

            foreach (var vendor in vendors)
            {
                savedPaymentMap.TryGetValue(vendor.PackId, out int paid);

                vendor.RestoreState(paid, currentQuestCount);
            }
        }

        private void HandleBoundsUpdated(Bounds bounds)
        {
            float topMargin = Board.Instance != null ? Board.Instance.TopMargin : 1f;

            Vector3 headerCenter = new Vector3(
                bounds.center.x,
                0f,
                bounds.max.z - (topMargin * 0.5f)
            );

            transform.position = headerCenter;
        }

        private void HandleQuestCompleted(QuestInstance _)
        {
            int completedQuests = QuestManager.Instance.CompletedQuestsCount;

            foreach (var vendor in vendors)
            {
                if (vendor.TryActivate(completedQuests))
                {
                    activationQueue.Enqueue(vendor);
                }
            }

            TryStartNextSequence();
        }

        private void TryStartNextSequence()
        {
            if (activeSequenceCoroutine == null && activationQueue.Count > 0)
            {
                PackVendor nextVendor = activationQueue.Dequeue();
                activeSequenceCoroutine = StartCoroutine(PlayActivationSequence(nextVendor));
            }
        }

        private IEnumerator PlayActivationSequence(PackVendor vendor)
        {
            InputManager.Instance.AddLock(tradeSequenceLock);

            TimeManager.Instance.SetExternalPause(true);

            var (header, body) = vendor.GetInfo();
            InfoPanel.Instance?.RequestInfoDisplay(
                sequenceRequester,
                InfoPriority.Sequence,
                ("卡包已解锁", body)
            );

            vendor.SetHighlighted(true);

            yield return cameraController.MoveTo(vendor.transform.position);
            yield return new WaitForSecondsRealtime(2f);

            vendor.SetHighlighted(false);

            activeSequenceCoroutine = null;

            InputManager.Instance.RemoveLock(tradeSequenceLock);
            InfoPanel.Instance?.ClearInfoRequest(sequenceRequester);

            TimeManager.Instance.SetExternalPause(false);

            TryStartNextSequence();
        }

        public void HighlightTradeableZones(CardStack stack)
        {
            foreach (var zone in zones)
            {
                if (zone.CanTrade(stack))
                {
                    zone.SetHighlighted(true);
                    highlightedZones.Add(zone);
                }
            }
        }

        public void TurnOffHighlightedZones()
        {
            highlightedZones.ForEach(zone => zone.SetHighlighted(false));
            highlightedZones.Clear();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            int totalZones = 1 + (offeredPacks != null ? offeredPacks.Count : 0);
            if (totalZones <= 0) return;

            float totalWidth = (totalZones - 1) * spacing;
            float startX = -(totalWidth / 2f);

            Vector3 cardSize = new Vector3(0.8f, 0f, 1f);

            for (int i = 0; i < totalZones; i++)
            {
                Gizmos.color = i == 0 ? Color.green : Color.cyan;

                float newX = startX + (i * spacing);
                Vector3 zoneLocalPos = new Vector3(newX, 0, 0);
                Vector3 size = new Vector3(cardSize.x * zoneScale.x, 0f, cardSize.z * zoneScale.z);
                Gizmos.DrawWireCube(zoneLocalPos, size);

                Gizmos.color = Color.yellow;
                Vector3 spawnedCardLocalPos = zoneLocalPos + spawnOffset;
                Gizmos.DrawWireCube(spawnedCardLocalPos, cardSize);
            }
        }
#endif
    }
}
