using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    // Resolves presentation data and transforms; BattleVfxPool owns instance lifecycle.
    public sealed class BattleVfxPresenter
    {
        private readonly BoardPresenter boardPresenter;
        private readonly BattlePresentationLookup presentationLookup;
        private readonly UnitViewRegistry unitViews;
        private readonly IReadOnlyDictionary<int, UnitPresentationState> presentationStatesByUnitId;
        private readonly BattleVfxPool pool;
        private readonly BattleVfxProfile defaultProfile;
        private readonly Dictionary<int, VfxDefinition> projectileImpactVfxById = new Dictionary<int, VfxDefinition>(8);
        private readonly Dictionary<SequenceVfxKey, VfxHandle> persistentHandlesBySequence = new Dictionary<SequenceVfxKey, VfxHandle>(8);
        private readonly List<SequenceVfxKey> persistentKeysToRelease = new List<SequenceVfxKey>(8);
        private readonly HashSet<BattleVfxProfile> prewarmedProfiles = new HashSet<BattleVfxProfile>();
        private readonly HashSet<VfxDefinition> prewarmedDefinitions = new HashSet<VfxDefinition>();
        private readonly List<VfxDefinition> configuredDefinitions = new List<VfxDefinition>(16);

        public BattleVfxPresenter(
            BoardPresenter boardPresenter,
            BattlePresentationLookup presentationLookup,
            UnitViewRegistry unitViews,
            IReadOnlyDictionary<int, UnitPresentationState> presentationStatesByUnitId,
            BattleVfxPool pool,
            BattleVfxProfile defaultProfile)
        {
            this.boardPresenter = boardPresenter;
            this.presentationLookup = presentationLookup;
            this.unitViews = unitViews;
            this.presentationStatesByUnitId = presentationStatesByUnitId;
            this.pool = pool;
            this.defaultProfile = defaultProfile;
            PrewarmConfiguredEffects();
        }

        public void Handle(BattleEvent battleEvent)
        {
            if (pool == null)
            {
                return;
            }

            HandleSequenceTransition(battleEvent);
            if (IsCancellationEvent(battleEvent.Type))
            {
                return;
            }

            if (TryGetProjectileBinding(battleEvent, out BattleVfxBinding projectileBinding))
            {
                Play(projectileBinding, battleEvent);
                return;
            }

            if (!TryGetCue(battleEvent, out BattleVfxCue cue)
                || !TryResolveBinding(battleEvent, cue, out BattleVfxBinding binding))
            {
                return;
            }

            Play(binding, battleEvent);
        }

        public void ReleaseOwnedByUnit(int unitId)
        {
            ReleasePersistentOwnedByUnit(unitId);
            pool?.ReleaseOwnedByUnit(unitId);
        }

        public void ReleaseAll()
        {
            pool?.ReleaseAll();
            projectileImpactVfxById.Clear();
            persistentHandlesBySequence.Clear();
        }

        public void SetCombatSpeed(float speed)
        {
            pool?.SetCombatSpeed(speed);
        }

        public void PrewarmConfiguredEffects()
        {
            PrewarmProfile(defaultProfile);
            if (pool == null || presentationLookup == null)
            {
                return;
            }

            presentationLookup.CollectVfxDefinitions(configuredDefinitions);
            for (int i = 0; i < configuredDefinitions.Count; i++)
            {
                PrewarmDefinition(configuredDefinitions[i]);
            }
        }

        private bool TryGetProjectileBinding(
            BattleEvent battleEvent,
            out BattleVfxBinding binding)
        {
            binding = default;
            if (presentationLookup == null)
            {
                return false;
            }

            switch (battleEvent.Type)
            {
                case BattleEventType.ProjectileLaunched:
                    if (!presentationLookup.TryGetProjectileVfx(battleEvent.PresentationId, out VfxDefinition launchVfx, out VfxDefinition impactVfx))
                    {
                        return false;
                    }

                    if (battleEvent.ProjectileId > 0 && IsUsable(impactVfx))
                    {
                        projectileImpactVfxById[battleEvent.ProjectileId] = impactVfx;
                    }

                    if (!IsUsable(launchVfx))
                    {
                        return false;
                    }

                    binding = CreateProjectileBinding(launchVfx, VfxSpawnSubject.Source, UnitVfxAnchor.Body);
                    return true;
                case BattleEventType.ProjectileResolved:
                    if (!projectileImpactVfxById.TryGetValue(battleEvent.ProjectileId, out VfxDefinition resolvedImpactVfx))
                    {
                        return false;
                    }

                    projectileImpactVfxById.Remove(battleEvent.ProjectileId);
                    if (battleEvent.Amount <= 0 || !IsUsable(resolvedImpactVfx))
                    {
                        return false;
                    }

                    binding = CreateProjectileBinding(resolvedImpactVfx, VfxSpawnSubject.Target, UnitVfxAnchor.Body);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryGetCue(BattleEvent battleEvent, out BattleVfxCue cue)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.AttackWindupStarted:
                    cue = BattleVfxCue.AttackWindup;
                    return true;
                case BattleEventType.AttackFired:
                    cue = BattleVfxCue.AttackFired;
                    return true;
                case BattleEventType.UnitDamaged:
                    cue = battleEvent.IsCritical ? BattleVfxCue.CriticalImpact : BattleVfxCue.Damaged;
                    return true;
                case BattleEventType.UnitDied:
                    cue = BattleVfxCue.Death;
                    return true;
                case BattleEventType.SpecialWindupStarted:
                    cue = BattleVfxCue.SpecialWindup;
                    return true;
                case BattleEventType.SpecialCastStarted:
                    cue = BattleVfxCue.SpecialCast;
                    return true;
                case BattleEventType.SpecialStrikeFired:
                    cue = BattleVfxCue.SpecialStrike;
                    return true;
                default:
                    cue = BattleVfxCue.None;
                    return false;
            }
        }

        private bool TryResolveBinding(BattleEvent battleEvent, BattleVfxCue cue, out BattleVfxBinding binding)
        {
            int sourceUnitId = GetSourceUnitId(battleEvent);
            int targetUnitId = GetTargetUnitId(battleEvent);
            bool isSpecialCue = cue == BattleVfxCue.SpecialWindup
                || cue == BattleVfxCue.SpecialCast
                || cue == BattleVfxCue.SpecialStrike;
            int profileUnitId = cue == BattleVfxCue.Damaged || cue == BattleVfxCue.CriticalImpact || cue == BattleVfxCue.Death
                ? targetUnitId
                : sourceUnitId;

            if (TryGetUnitProfiles(profileUnitId, out BattleVfxProfile unitProfile, out BattleVfxProfile specialProfile))
            {
                PrewarmProfile(unitProfile);
                PrewarmProfile(specialProfile);

                if (isSpecialCue && TryGetUsableBinding(specialProfile, cue, out binding))
                {
                    return true;
                }

                if (TryGetUsableBinding(unitProfile, cue, out binding))
                {
                    return true;
                }
            }

            return TryGetUsableBinding(defaultProfile, cue, out binding);
        }

        private void PrewarmProfile(BattleVfxProfile profile)
        {
            if (pool == null || profile == null || !prewarmedProfiles.Add(profile))
            {
                return;
            }

            BattleVfxBinding[] bindings = profile.Bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                VfxDefinition definition = bindings[i].Effect;
                PrewarmDefinition(definition);
            }
        }

        private void PrewarmDefinition(VfxDefinition definition)
        {
            if (pool != null && IsUsable(definition) && prewarmedDefinitions.Add(definition))
            {
                pool.Prewarm(definition);
            }
        }

        private void HandleSequenceTransition(BattleEvent battleEvent)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.AttackWindupStarted:
                    ReleasePersistent(new SequenceVfxKey(battleEvent.UnitId, battleEvent.SequenceId, BattleVfxCue.AttackWindup));
                    break;
                case BattleEventType.AttackWindupCancelled:
                case BattleEventType.AttackFired:
                    ReleasePersistent(new SequenceVfxKey(battleEvent.UnitId, battleEvent.SequenceId, BattleVfxCue.AttackWindup));
                    break;
                case BattleEventType.SpecialWindupStarted:
                    ReleasePersistent(new SequenceVfxKey(battleEvent.UnitId, battleEvent.SequenceId, BattleVfxCue.SpecialWindup));
                    break;
                case BattleEventType.SpecialWindupCancelled:
                case BattleEventType.SpecialCastStarted:
                case BattleEventType.UnitSpecialActivated:
                    ReleasePersistent(new SequenceVfxKey(battleEvent.UnitId, battleEvent.SequenceId, BattleVfxCue.SpecialWindup));
                    break;
                case BattleEventType.UnitDied:
                    ReleasePersistentOwnedByUnit(battleEvent.UnitId);
                    break;
            }
        }

        private static bool IsCancellationEvent(BattleEventType type)
        {
            return type == BattleEventType.AttackWindupCancelled
                || type == BattleEventType.SpecialWindupCancelled;
        }

        private void ReleasePersistentOwnedByUnit(int unitId)
        {
            if (unitId <= 0)
            {
                return;
            }

            persistentKeysToRelease.Clear();
            foreach (KeyValuePair<SequenceVfxKey, VfxHandle> pair in persistentHandlesBySequence)
            {
                if (pair.Key.UnitId == unitId)
                {
                    persistentKeysToRelease.Add(pair.Key);
                }
            }

            for (int i = 0; i < persistentKeysToRelease.Count; i++)
            {
                ReleasePersistent(persistentKeysToRelease[i]);
            }
        }

        private void ReleasePersistent(SequenceVfxKey key)
        {
            if (!persistentHandlesBySequence.TryGetValue(key, out VfxHandle handle))
            {
                return;
            }

            persistentHandlesBySequence.Remove(key);
            pool?.Release(handle);
        }

        private void Play(BattleVfxBinding binding, BattleEvent battleEvent)
        {
            if (!IsUsable(binding.Effect) || !TryCreateSpawnRequest(binding, battleEvent, out VfxSpawnRequest request))
            {
                return;
            }

            VfxHandle handle = pool.Play(binding.Effect, request);
            if (binding.Effect.LifetimeMode == VfxLifetimeMode.Manual
                && TryGetPersistentKey(battleEvent, out SequenceVfxKey key)
                && handle.IsValid)
            {
                ReleasePersistent(key);
                persistentHandlesBySequence.Add(key, handle);
            }
        }

        private static bool TryGetPersistentKey(BattleEvent battleEvent, out SequenceVfxKey key)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.AttackWindupStarted:
                    key = new SequenceVfxKey(battleEvent.UnitId, battleEvent.SequenceId, BattleVfxCue.AttackWindup);
                    return true;
                case BattleEventType.SpecialWindupStarted:
                    key = new SequenceVfxKey(battleEvent.UnitId, battleEvent.SequenceId, BattleVfxCue.SpecialWindup);
                    return true;
                default:
                    key = default;
                    return false;
            }
        }

        private bool TryCreateSpawnRequest(BattleVfxBinding binding, BattleEvent battleEvent, out VfxSpawnRequest request)
        {
            int sourceUnitId = GetSourceUnitId(battleEvent);
            int targetUnitId = GetTargetUnitId(battleEvent);
            int anchorUnitId = ResolveAnchorUnitId(binding.Subject, sourceUnitId, targetUnitId);
            Transform anchor = ResolveAnchor(anchorUnitId, binding.Anchor);
            Vector3 basePosition = ResolveBasePosition(binding.Subject, battleEvent, anchor);
            Quaternion rotation = ResolveRotation(binding, battleEvent, basePosition, anchor);
            int ownerUnitId = binding.FollowAnchor ? anchorUnitId : 0;

            if (binding.FollowAnchor && anchor != null)
            {
                request = new VfxSpawnRequest(
                    anchor,
                    binding.LocalPosition,
                    binding.LocalRotation,
                    binding.ResolvedLocalScale,
                    ownerUnitId);
                return true;
            }

            Vector3 worldPosition = anchor != null
                ? anchor.TransformPoint(binding.LocalPosition)
                : basePosition + binding.LocalPosition;
            request = new VfxSpawnRequest(worldPosition, rotation, binding.ResolvedLocalScale, ownerUnitId);
            return true;
        }

        private bool TryGetUnitProfiles(int unitId, out BattleVfxProfile unitProfile, out BattleVfxProfile specialProfile)
        {
            unitProfile = null;
            specialProfile = null;
            return unitId > 0
                && presentationLookup != null
                && presentationStatesByUnitId != null
                && presentationStatesByUnitId.TryGetValue(unitId, out UnitPresentationState state)
                && presentationLookup.TryGetUnitVfxProfiles(state.PresentationId, out unitProfile, out specialProfile);
        }

        private Transform ResolveAnchor(int unitId, UnitVfxAnchor anchor)
        {
            if (unitId > 0 && unitViews != null && unitViews.TryGet(unitId, out UnitView unitView))
            {
                return unitView.ResolveVfxAnchor(anchor);
            }

            return null;
        }

        private Vector3 ResolveBasePosition(BattleVfxBinding binding, BattleEvent battleEvent, Transform anchor)
        {
            return ResolveBasePosition(binding.Subject, battleEvent, anchor);
        }

        private Vector3 ResolveBasePosition(VfxSpawnSubject subject, BattleEvent battleEvent, Transform anchor)
        {
            if (anchor != null)
            {
                return anchor.position;
            }

            if (boardPresenter == null)
            {
                return Vector3.zero;
            }

            switch (subject)
            {
                case VfxSpawnSubject.Source:
                case VfxSpawnSubject.SourceHex:
                    return boardPresenter.GetWorldPosition(battleEvent.From);
                default:
                    return boardPresenter.GetWorldPosition(battleEvent.To);
            }
        }

        private Quaternion ResolveRotation(
            BattleVfxBinding binding,
            BattleEvent battleEvent,
            Vector3 position,
            Transform anchor)
        {
            if (binding.FaceTarget)
            {
                Transform targetAnchor = ResolveAnchor(GetTargetUnitId(battleEvent), UnitVfxAnchor.Body);
                Vector3 targetPosition = ResolveBasePosition(VfxSpawnSubject.Target, battleEvent, targetAnchor);
                Vector3 direction = targetPosition - position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            return (anchor != null ? anchor.rotation : Quaternion.identity) * binding.LocalRotation;
        }

        private static int ResolveAnchorUnitId(VfxSpawnSubject subject, int sourceUnitId, int targetUnitId)
        {
            switch (subject)
            {
                case VfxSpawnSubject.Source:
                    return sourceUnitId;
                case VfxSpawnSubject.Target:
                    return targetUnitId;
                default:
                    return 0;
            }
        }

        private static int GetSourceUnitId(BattleEvent battleEvent)
        {
            return battleEvent.Type == BattleEventType.UnitDamaged || battleEvent.Type == BattleEventType.UnitDied
                ? 0
                : battleEvent.UnitId;
        }

        private static int GetTargetUnitId(BattleEvent battleEvent)
        {
            return battleEvent.TargetUnitId > 0
                ? battleEvent.TargetUnitId
                : battleEvent.UnitId;
        }

        private static BattleVfxBinding CreateProjectileBinding(
            VfxDefinition effect,
            VfxSpawnSubject subject,
            UnitVfxAnchor anchor)
        {
            return new BattleVfxBinding
            {
                Effect = effect,
                Subject = subject,
                Anchor = anchor,
                LocalScale = Vector3.one
            };
        }

        private static bool TryGetUsableBinding(BattleVfxProfile profile, BattleVfxCue cue, out BattleVfxBinding binding)
        {
            if (profile != null && profile.TryGet(cue, out binding) && IsUsable(binding.Effect))
            {
                return true;
            }

            binding = default;
            return false;
        }

        private static bool IsUsable(VfxDefinition definition)
        {
            return definition != null && definition.Prefab != null;
        }

        private readonly struct SequenceVfxKey : System.IEquatable<SequenceVfxKey>
        {
            public readonly int UnitId;
            private readonly int sequenceId;
            private readonly BattleVfxCue cue;

            public SequenceVfxKey(int unitId, int sequenceId, BattleVfxCue cue)
            {
                UnitId = unitId;
                this.sequenceId = sequenceId;
                this.cue = cue;
            }

            public bool Equals(SequenceVfxKey other)
            {
                return UnitId == other.UnitId && sequenceId == other.sequenceId && cue == other.cue;
            }

            public override bool Equals(object obj)
            {
                return obj is SequenceVfxKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = UnitId;
                    hash = hash * 31 + sequenceId;
                    return hash * 31 + (int)cue;
                }
            }
        }
    }
}
