using UnityEngine;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class CombatProjectile : MonoBehaviour
    {
        [SerializeField, Tooltip("Time (in seconds) the projectile takes to reach the target position.")]
        private float duration = 0.6f;

        /// <summary>
        /// Initializes the projectile's position and orientation, then starts a linear animation
        /// from startPos to targetPos.
        /// </summary>
        /// <remarks>
        /// Upon completion of the movement animation, the projectile's GameObject is automatically destroyed.
        /// The projectile is rotated to face the target position in the horizontal (X/Z) plane.
        /// </remarks>
        /// <param name="startPos">The initial world position of the projectile.</param>
        /// <param name="targetPos">The destination world position of the projectile.</param>
        /// <returns>The DOTween Tween object controlling the movement animation.</returns>
        public Tween Fire(Vector3 startPos, Vector3 targetPos)
        {
            transform.position = startPos;

            // Rotate towards target
            Vector3 direction = (targetPos - startPos).Flatten();
            transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0);

            Tween moveTween = transform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(() => Destroy(gameObject));

            return moveTween;
        }
    }
}
