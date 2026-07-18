#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CryingSnow.StackCraft
{
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        private const string PlayModeScenePath = "Assets/StackCraft/Scenes/Title.unity";
        private const string PreviousSceneKey = "PlayModeStartScene.PreviousScene";

        static PlayModeStartScene()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    HandleEnteringPlayMode();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    HandleExitingPlayMode();
                    break;
            }
        }

        private static void HandleEnteringPlayMode()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.path == PlayModeScenePath)
                return;

            SessionState.SetString(PreviousSceneKey, activeScene.path);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorApplication.isPlaying = false;
                return;
            }

            EditorSceneManager.OpenScene(PlayModeScenePath);
        }

        private static void HandleExitingPlayMode()
        {
            string previousScenePath = SessionState.GetString(PreviousSceneKey, string.Empty);

            if (string.IsNullOrEmpty(previousScenePath))
                return;

            if (!System.IO.File.Exists(previousScenePath))
                return;

            EditorSceneManager.OpenScene(previousScenePath);
            SessionState.EraseString(PreviousSceneKey);
        }
    }
}
#endif
