using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CryingSnow.StackCraft
{
    public static class RenderPipelineSwitcher
    {
        private const string URP_ASSET_PATH = "Assets/StackCraft/Settings/URP/URP_Asset.asset";

        [MenuItem("Tools/Crying Snow/Switch to URP")]
        public static void SwitchToURP()
        {
            var urpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(URP_ASSET_PATH);
            if (urpAsset == null)
            {
                Debug.LogError("URP Asset not found at: " + URP_ASSET_PATH);
                return;
            }

            GraphicsSettings.renderPipelineAsset = urpAsset;

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = urpAsset;
            }

            Debug.Log("Switched to URP");
        }

        [MenuItem("Tools/Crying Snow/Switch to Built-in")]
        public static void SwitchToBuiltIn()
        {
            GraphicsSettings.renderPipelineAsset = null;

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = null;
            }

            Debug.Log("Switched to Built-in Pipeline");
        }
    }
}
