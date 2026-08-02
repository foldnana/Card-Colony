using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    /// <summary>Owns combat visuals. It never changes health, energy, inventory or quests.</summary>
    public sealed class CombatPresentationController
    {
        private readonly CameraController cameraController;

        public CombatPresentationController()
        {
            cameraController = UnityEngine.Object.FindAnyObjectByType<CameraController>();
        }

        public IEnumerator Play(
            CardInstance actor,
            CardInstance target,
            CombatCommand command,
            CombatActionPlan plan,
            CombatRect rect,
            Action notifyImpact,
            Action notifyCompleted)
        {
            if (actor == null || target == null || command == null)
            {
                notifyImpact?.Invoke();
                notifyCompleted?.Invoke();
                yield break;
            }

            CombatType type = actor.Definition.CombatType;
            if (command.Type != CombatCommandType.UseItem)
            {
                PlayAttackSound(type);
                if (type is CombatType.Melee or CombatType.None)
                {
                    Vector3 targetPosition = target.transform.position + Vector3.up * 0.05f;
                    Tween tween = actor.transform.DOJump(targetPosition, 1f, 1, 0.3f)
                        .SetUpdate(false);
                    yield return actor.StartCombatTween(tween).WaitForCompletion();
                }
                else
                {
                    Tween projectile = CombatManager.Instance?.SpawnProjectile(
                        type,
                        actor.transform.position + Vector3.up * 0.05f,
                        target.transform.position + Vector3.up * 0.05f);
                    if (projectile != null)
                        yield return projectile.WaitForCompletion();
                }
            }

            Vector3 hitPosition = target.transform.TransformPoint(
                new Vector3(0.3f, 0.1f, 0.4f));
            notifyImpact?.Invoke();

            if (command.Type != CombatCommandType.UseItem)
            {
                HitResult hit = plan?.Hit ?? default;
                if (hit.Type == HitType.Miss)
                    AudioManager.Instance?.PlaySFX(AudioId.Miss);
                else
                {
                    cameraController?.Shake();
                    PlayHitSound(type);
                    if (hit.IsCritical)
                        AudioManager.Instance?.PlaySFX(AudioId.Critical);
                }
                CombatManager.Instance?.SpawnHitUI(hitPosition, hit);

                const float returnTime = 0.3f;
                if (type is CombatType.Melee or CombatType.None && rect != null &&
                    actor != null)
                {
                    Tween tween = actor.transform
                        .DOJump(rect.GetLayoutPosition(actor), 1f, 1, returnTime)
                        .SetUpdate(false);
                    yield return actor.StartCombatTween(tween).WaitForCompletion();
                }
                else
                {
                    yield return new WaitForSeconds(returnTime);
                }
            }

            notifyCompleted?.Invoke();
        }

        private static void PlayAttackSound(CombatType type)
        {
            AudioId id = type switch
            {
                CombatType.Ranged => AudioId.AttackRanged,
                CombatType.Magic => AudioId.AttackMagic,
                _ => AudioId.AttackMelee
            };
            AudioManager.Instance?.PlaySFX(id);
        }

        private static void PlayHitSound(CombatType type)
        {
            AudioId id = type switch
            {
                CombatType.Ranged => AudioId.HitRanged,
                CombatType.Magic => AudioId.HitMagic,
                _ => AudioId.HitMelee
            };
            AudioManager.Instance?.PlaySFX(id);
        }
    }
}
