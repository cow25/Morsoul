using UnityEngine;

namespace Morsoul.Hub
{
    [CreateAssetMenu(menuName = "Morsoul/Hub/Visual Profile", fileName = "VisualProfile")]
    public class VisualProfile : ScriptableObject
    {
        public Sprite sprite;
        public RuntimeAnimatorController animatorController;
        public Color tint = Color.white;
        public Material material;
        public Vector2 visualOffset = Vector2.zero;
        public Vector2 visualScale = Vector2.one;
        public bool flipX;
        public int sortingOrder;
    }
}
