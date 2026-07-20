using System;
using UnityEngine;
using CardColony.Gameplay;
using CardColony.TimeSystem;
using CardColony.UnityIntegration.Save;

namespace CardColony.UnityIntegration
{
    public sealed class WorldClockDriver : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float gameMinutesPerRealSecond = 2f;
        [SerializeField, Min(0f)] private float initialWorldMinutes = 360f;
        [SerializeField, Min(1)] private int inventorySlots = 24;
        [SerializeField, Min(0.1f)] private float inventoryMaxWeight = 100f;
        [SerializeField] private string saveFileName = "CardColonyPlayableLoop";
        [SerializeField] private bool disableLegacyTimeManager = true;

        private Behaviour disabledLegacyTimeManager;
        private bool legacyTimeManagerWasEnabled;

        public event Action StateChanged;

        public PlayableLoopSession Session { get; private set; }
        public string PersistenceMessage { get; private set; }
        public string StatusMessage => string.IsNullOrEmpty(PersistenceMessage)
            ? Session?.LastMessage
            : PersistenceMessage;

        private void Awake()
        {
            if (Session == null)
            {
                Initialize(new PlayableLoopSession(
                    gameMinutesPerRealSecond,
                    initialWorldMinutes,
                    inventorySlots,
                    inventoryMaxWeight));
            }
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void Start()
        {
            if (disableLegacyTimeManager)
                DisableLegacyTimeManager();
        }

        private void OnDestroy()
        {
            if (disabledLegacyTimeManager != null)
                disabledLegacyTimeManager.enabled = legacyTimeManagerWasEnabled;

            Time.timeScale = 1f;
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
                SaveToDisk();
        }

        private void OnApplicationQuit()
        {
            SaveToDisk();
        }

        public void Initialize(PlayableLoopSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            SyncNativeTimeScale();
            StateChanged?.Invoke();
        }

        public bool DisableLegacyTimeManager()
        {
            foreach (MonoBehaviour behaviour in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour.GetType().FullName != "CryingSnow.StackCraft.TimeManager")
                    continue;

                disabledLegacyTimeManager = behaviour;
                legacyTimeManagerWasEnabled = behaviour.enabled;
                behaviour.enabled = false;
                if (Session == null)
                    Time.timeScale = 1f;
                else
                    SyncNativeTimeScale();
                return true;
            }

            return false;
        }

        public void Advance(float unscaledDeltaSeconds)
        {
            if (Session == null)
                return;

            Session.Tick(unscaledDeltaSeconds);
            StateChanged?.Invoke();
        }

        public void SetPaused(bool isPaused)
        {
            Session.Clock.IsPaused = isPaused;
            SyncNativeTimeScale();
            StateChanged?.Invoke();
        }

        public void SetNormalSpeed()
        {
            Session.Clock.Speed = WorldClockSpeed.Normal;
            SyncNativeTimeScale();
            StateChanged?.Invoke();
        }

        public void SetFastSpeed()
        {
            Session.Clock.Speed = WorldClockSpeed.Fast;
            SyncNativeTimeScale();
            StateChanged?.Invoke();
        }

        private void SyncNativeTimeScale()
        {
            if (Session == null)
                return;

            Time.timeScale = Session.Clock.IsPaused
                ? 0f
                : Session.Clock.Speed == WorldClockSpeed.Fast ? 4f : 1f;
        }

        public void SetWaiting(bool isWaiting)
        {
            Session.Clock.IsWaiting = isWaiting;
            StateChanged?.Invoke();
        }

        public LoopCommandResult StartExplore()
        {
            PersistenceMessage = null;
            LoopCommandResult result = Session.StartExploreWhisperingForest();
            StateChanged?.Invoke();
            return result;
        }

        public LoopCommandResult StartGather()
        {
            PersistenceMessage = null;
            LoopCommandResult result = Session.StartGatherHerbs();
            StateChanged?.Invoke();
            return result;
        }

        public LoopCommandResult StartBrew()
        {
            PersistenceMessage = null;
            LoopCommandResult result = Session.StartBrewPotion();
            StateChanged?.Invoke();
            return result;
        }

        public bool SaveToDisk()
        {
            if (Session == null)
                return false;

            bool saved = RunSnapshotFileStore.TrySave(saveFileName, Session.CreateSnapshot(), out string error);
            PersistenceMessage = saved ? "存档已保存。" : $"保存失败：{error}";
            StateChanged?.Invoke();
            return saved;
        }

        public bool LoadFromDisk()
        {
            if (!RunSnapshotFileStore.TryLoad(saveFileName, out RunSnapshot snapshot, out string error))
            {
                PersistenceMessage = $"读取失败：{error}";
                StateChanged?.Invoke();
                return false;
            }

            try
            {
                Initialize(PlayableLoopSession.Restore(snapshot, gameMinutesPerRealSecond));
                PersistenceMessage = "存档已读取。";
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                PersistenceMessage = $"读取失败：{exception.Message}";
                StateChanged?.Invoke();
                return false;
            }
        }
    }
}
