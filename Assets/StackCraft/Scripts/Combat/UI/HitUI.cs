using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    public class HitUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("The Image component used to display the type of hit (Miss, Normal, Critical).")]
        private Image hitImage;

        [SerializeField, Tooltip("The Image component used to display combat type effectiveness (Advantage or Disadvantage).")]
        private Image effectivenessImage;

        [SerializeField, Tooltip("The TextMeshProUGUI component used to display the damage dealt.")]
        private TextMeshProUGUI damageLabel;

        [Header("Hit Sprites")]
        [SerializeField, Tooltip("The sprite to display when the attack results in a Miss.")]
        private Sprite missSprite;

        [SerializeField, Tooltip("The sprite to display when the attack results in a Normal hit.")]
        private Sprite normalSprite;

        [SerializeField, Tooltip("The sprite to display when the attack results in a Critical hit.")]
        private Sprite criticalSprite;

        [Header("Effectiveness Sprites")]
        [SerializeField, Tooltip("The sprite to display when the attack has a Combat Type Advantage.")]
        private Sprite advantageSprite;

        [SerializeField, Tooltip("The sprite to display when the attack has a Combat Type Disadvantage.")]
        private Sprite disadvantageSprite;

        /// <summary>
        /// Sets the visual state of the damage pop-up based on the outcome of a hit result.
        /// </summary>
        /// <remarks>
        /// This method configures the sprite (Miss, Normal, Critical) and the damage number text.
        /// It also sets the effectiveness icon (Advantage, Disadvantage) and starts a DOTween
        /// animation (DOPunchScale) that destroys the GameObject upon completion.
        /// </remarks>
        /// <param name="result">
        /// The <see cref="HitResult"/> data structure containing the type of hit, damage amount, and combat advantage.
        /// </param>
        public void Initialize(HitResult result)
        {
            switch (result.Type)
            {
                case HitType.Miss:
                    hitImage.sprite = missSprite;
                    damageLabel.text = "";
                    break;

                case HitType.Normal:
                    hitImage.sprite = normalSprite;
                    damageLabel.text = result.Damage.ToString();
                    break;

                case HitType.Critical:
                    hitImage.sprite = criticalSprite;
                    damageLabel.text = result.Damage.ToString();
                    break;
            }

            switch (result.Advantage)
            {
                case CombatTypeAdvantage.Advantage:
                    effectivenessImage.sprite = advantageSprite;
                    effectivenessImage.enabled = true;
                    break;

                case CombatTypeAdvantage.Disadvantage:
                    effectivenessImage.sprite = disadvantageSprite;
                    effectivenessImage.enabled = true;
                    break;

                case CombatTypeAdvantage.None:
                default:
                    effectivenessImage.sprite = null;
                    effectivenessImage.enabled = false;
                    break;
            }

            transform.DOPunchScale(new Vector3(0.15f, 0.15f), 1f)
                .SetUpdate(true)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}
