using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum TimePace
    {
        Paused,
        Normal,
        Fast
    }

    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        public event System.Action<TimePace> OnTimePaceChanged;
        public event System.Action<int> OnDayEnded;
        public event System.Action<int> OnDayStarted;

        public TimePace CurrentPace { get; private set; } = TimePace.Normal;
        public float NormalizedTime => Mathf.Clamp01(currentTime / dayDuration);
        public int CurrentDay { get; private set; } = 1;

        private float dayDuration;
        private float currentTime;

        private int externalPauseLocks = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady += HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;

                dayDuration = GameDirector.Instance.GameData.GameplayPrefs.DayDuration;
            }
        }

        private void Update()
        {
            if (currentTime < dayDuration && externalPauseLocks == 0)
            {
                currentTime += Time.deltaTime;
            }
            else if (currentTime >= dayDuration)
            {
                currentTime = 0f;
                CurrentPace = TimePace.Paused;
                UpdateTimeScale();

                OnDayEnded?.Invoke(CurrentDay);
            }
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnSceneDataReady -= HandleSceneDataReady;
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;
            }
        }

        private void HandleSceneDataReady(SceneData sceneData, bool wasLoaded)
        {
            if (wasLoaded)
            {
                currentTime = sceneData.SavedTime.CurrentTime;
                CurrentDay = sceneData.SavedTime.CurrentDay;
            }
        }

        private void HandleBeforeSave(GameData gameData)
        {
            if (gameData.TryGetScene(out var sceneData))
            {
                sceneData.SavedTime = new TimeData(currentTime, CurrentDay);
            }
        }

        /// <summary>
        /// Controls an external pause lock that globally stops the flow of time (sets Time.timeScale to 0).
        /// </summary>
        /// <remarks>
        /// This method uses a counter. Time remains paused as long as one or more external systems 
        /// (e.g., scene transitions, menus, combat resolution) have an active lock. Time only resumes 
        /// when the last lock is removed.
        /// </remarks>
        /// <param name="isPaused">If true, adds a lock; if false, removes one lock.</param>
        public void SetExternalPause(bool isPaused)
        {
            if (isPaused)
                externalPauseLocks++;
            else
                externalPauseLocks = Mathf.Max(0, externalPauseLocks - 1);

            UpdateTimeScale();
        }

        private void UpdateTimeScale()
        {
            if (externalPauseLocks > 0)
            {
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = (float)CurrentPace;
            }
        }

        /// <summary>
        /// Cycles the game's time flow pace between Paused, Normal, and Fast speeds.
        /// </summary>
        /// <remarks>
        /// This directly controls Time.timeScale unless an external pause lock is active. 
        /// </remarks>
        /// <param name="timePaceIndex">Outputs the integer value (index) of the new TimePace enum.</param>
        public void CycleTimePace(out int timePaceIndex)
        {
            CurrentPace = (TimePace)(((int)CurrentPace + 1) % System.Enum.GetValues(typeof(TimePace)).Length);

            UpdateTimeScale();

            OnTimePaceChanged?.Invoke(CurrentPace);
            timePaceIndex = (int)CurrentPace;
        }

        /// <summary>
        /// Advances the game state to the next day and resets the time pace to Normal.
        /// </summary>
        public void StartNewDay()
        {
            CurrentDay++;
            CurrentPace = TimePace.Normal;

            UpdateTimeScale();

            OnDayStarted?.Invoke(CurrentDay);
        }
    }
}
