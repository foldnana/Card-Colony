using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        private readonly HashSet<object> inputLocks = new();
        private readonly HashSet<object> cameraAllowedLocks = new();

        public bool IsInputEnabled => inputLocks.Count == 0;
        public bool IsCameraInputEnabled
        {
            get
            {
                foreach (object requester in inputLocks)
                {
                    if (!cameraAllowedLocks.Contains(requester))
                        return false;
                }
                return true;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Adds an input lock, disabling all general user input throughout the game.
        /// </summary>
        /// <remarks>
        /// Input remains disabled until all unique locks (requesters) have been removed.
        /// This is used to block input during transitions, cutscenes, or menus.
        /// </remarks>
        /// <param name="requester">The object requesting the lock. Must be a unique object instance.</param>
        public void AddLock(object requester)
        {
            AddLock(requester, allowCameraInput: false);
        }

        /// <summary>
        /// Adds a scoped input lock and optionally keeps map pan and zoom available.
        /// </summary>
        public void AddLock(object requester, bool allowCameraInput)
        {
            if (requester == null)
                return;

            inputLocks.Add(requester);
            if (allowCameraInput)
                cameraAllowedLocks.Add(requester);
            else
                cameraAllowedLocks.Remove(requester);
        }

        /// <summary>
        /// Removes a previously acquired input lock.
        /// </summary>
        /// <remarks>
        /// If the requester object exists in the set of active locks, it is removed.
        /// Input will only become enabled again when the last lock is removed.
        /// </remarks>
        /// <param name="requester">The object that originally requested the lock.</param>
        public void RemoveLock(object requester)
        {
            if (requester != null)
            {
                inputLocks.Remove(requester);
                cameraAllowedLocks.Remove(requester);
            }
        }
    }
}
