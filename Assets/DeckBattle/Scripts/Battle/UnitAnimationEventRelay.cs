using UnityEngine;

namespace DeckBattle
{
    // Animation Events are dispatched to the Animator's GameObject, which is a child of UnitView.
    public sealed class UnitAnimationEventRelay : MonoBehaviour
    {
        private UnitView unitView;

        private void Awake()
        {
            unitView = GetComponentInParent<UnitView>();
        }

        public void Attack()
        {
            SpecialContact();
        }

        public void AttackContact()
        {
            unitView?.PlayAttackContactAnimationEvent();
        }

        public void ProjectileRelease()
        {
            unitView?.PlayProjectileReleaseAnimationEvent();
        }

        public void SpecialContact()
        {
            unitView?.PlaySpecialAttackAnimationEvent();
        }
    }
}
