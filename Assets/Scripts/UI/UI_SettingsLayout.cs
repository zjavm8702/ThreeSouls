using UnityEngine;

namespace ThreeSouls.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_SettingsLayout : MonoBehaviour
    {
        private void LateUpdate()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            var scale = Mathf.Min(parent.rect.width / 1040f, parent.rect.height / 980f);
            rect.localScale = Vector3.one * scale;
        }
    }
}
