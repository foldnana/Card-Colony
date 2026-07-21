using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class Highlight
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        private readonly GameObject highlightObject;
        private readonly MeshRenderer highlightRenderer;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private readonly Color defaultOutlineColor;

        public Highlight(Transform parent, Mesh mesh, Material material)
        {
            GameObject obj = new GameObject("Highlight");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localScale = Vector3.one;
            highlightObject = obj;

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            highlightRenderer = obj.AddComponent<MeshRenderer>();
            highlightRenderer.sharedMaterial = material;
            highlightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            highlightRenderer.receiveShadows = false;

            defaultOutlineColor = material != null && material.HasProperty(OutlineColorId)
                ? material.GetColor(OutlineColorId)
                : Color.white;
            SetColor(defaultOutlineColor);
        }

        public void SetActive(bool value) => highlightObject.SetActive(value);

        public void SetColor(Color color)
        {
            highlightRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(OutlineColorId, color);
            highlightRenderer.SetPropertyBlock(propertyBlock);
        }

        public void ResetColor() => SetColor(defaultOutlineColor);
    }
}
