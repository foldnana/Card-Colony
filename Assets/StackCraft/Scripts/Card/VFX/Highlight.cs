using UnityEngine;

namespace CryingSnow.StackCraft
{
    public class Highlight
    {
        private GameObject highlightObject;

        public Highlight(Transform parent, Mesh mesh, Material material)
        {
            GameObject obj = new GameObject("Highlight");
            obj.transform.SetParent(parent);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localScale = Vector3.one;
            highlightObject = obj;

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void SetActive(bool value) => highlightObject.SetActive(value);
    }
}
