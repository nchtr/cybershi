using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Описание одной покадровой анимации (цикла спрайтов). Создаётся как ассет:
    /// ПКМ в Project → Create → Cybershi → Sprite Animation.
    /// Просто заполните массив кадрами и задайте FPS.
    /// </summary>
    [CreateAssetMenu(menuName = "Cybershi/Sprite Animation", fileName = "SpriteAnimation")]
    public class SpriteAnimation : ScriptableObject
    {
        public string id = "idle";
        public Sprite[] frames;
        [Min(0.01f)] public float fps = 10f;
        public bool loop = true;

        public bool HasFrames => frames != null && frames.Length > 0;
        public float FrameDuration => 1f / Mathf.Max(0.01f, fps);
        public float TotalDuration => HasFrames ? frames.Length * FrameDuration : 0f;
    }
}
