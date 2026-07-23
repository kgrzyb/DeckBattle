# Unit Redesign Plan

## Goal

Redesign unit data and realtime combat behavior so each unit can be balanced with a richer set of stats while keeping the MVP deterministic, easy to test, and mobile-friendly.

The redesign should build on the current architecture:

- `UnitDefinition` stores immutable unit data.
- `UnitRuntimeState` stores per-battle mutable state.
- `CombatResolver` resolves attacks and damage.
- `MovementResolver` resolves pathing and hex movement.
- UI and views consume battle events instead of owning gameplay logic.

## Unit Stats

Each unit should support these stats:

- `MaxHp`
- `ApCost`
- `Attack`
- `AttackRange`
- `CritChance`
- `CritMultiplier`
- `AttackCooldown`
- `Rarity`
- `ManaThreshold`
- `ManaPerAttack`
- `ManaPerDamageTaken`
- `Power`
- `Armor`
- `ArmorPenetration`

`CritChance`, `Armor`, and `ArmorPenetration` are authored as `0-100` percentage values in the Unity Inspector.

For MVP, both mana gain values should default to `10`:

- `ManaPerAttack = 10`
- `ManaPerDamageTaken = 10`

## Suggested UnitDefinition Fields

```csharp
public int MaxHp = 1;
public int ApCost = 1;
public int Attack = 1;
public int AttackRange = 1;
public float CritChance = 0f;
public float CritMultiplier = 2f;
public float AttackCooldown = 1f;
public UnitRarity Rarity;
public int ManaThreshold = 100;
public int ManaPerAttack = 10;
public int ManaPerDamageTaken = 10;
public int Power = 1;
public float Armor = 0f;
public float ArmorPenetration = 0f;
```

## Validation Rules

`UnitDefinition.OnValidate()` should enforce:

- `MaxHp >= 1`
- `ApCost >= 0`
- `Attack >= 0`
- `AttackRange >= 1`
- `CritChance` clamped to `0-100`
- `CritMultiplier >= 1`
- `AttackCooldown > 0`
- `ManaThreshold >= 0`
- `ManaPerAttack >= 0`
- `ManaPerDamageTaken >= 0`
- `Power >= 0`
- `Armor` clamped to `0-100`
- `ArmorPenetration` clamped to `0-100`

## Runtime Unit State

Add mutable battle state to `UnitRuntimeState`:

- `CurrentMana`
- movement transition state
- active special ability duration
- active attack cooldown multiplier

Suggested fields:

```csharp
public int CurrentMana;
public bool IsMoving;
public HexCoord MovementDestination;
public float MovementTimeRemaining;
public float SpecialDurationRemaining;
public float AttackCooldownMultiplier;
```

The exact field names can follow the implementation style already used in the project.

## Damage Calculation

Move damage math out of `CombatResolver` into a small pure service, for example `DamageCalculator`.

Damage formula:

```text
effectiveArmor = target.Armor * (1 - attacker.ArmorPenetration / 100)
damageAfterArmor = attacker.Attack * (1 - effectiveArmor / 100)
if crit: damageAfterArmor *= attacker.CritMultiplier
finalDamage = max(0, rounded damageAfterArmor)
```

Example:

```text
target.Armor = 50
attacker.ArmorPenetration = 50

effectiveArmor = 50 * (1 - 0.5) = 25
damage reduction = 25%
```

Critical hits should use the project's deterministic random source, not `UnityEngine.Random`.

## Mana and MVP Special Ability

Mana gain:

```text
After a successful attack:
    attacker.CurrentMana += attacker.Definition.ManaPerAttack

After receiving damage:
    target.CurrentMana += target.Definition.ManaPerDamageTaken
```

When:

```text
CurrentMana >= ManaThreshold
```

the unit activates its special ability and mana resets:

```text
CurrentMana = 0
```

For the MVP, every unit uses the same special ability:

```text
Reduce attack cooldown by 50% for 5 seconds.
```

Implementation behavior:

- `ManaThreshold = 0` should disable the special ability.
- The active special sets the unit's attack cooldown multiplier to `0.5`.
- After `5s`, the multiplier returns to `1.0`.
- The effect should live in runtime state, not in `UnitDefinition`.

Do not build a broad ability framework yet. Keep the MVP ability small and isolated, but name the code so it can later evolve into unit-specific abilities.

## Attack Cooldown

Effective attack cooldown should include both global tuning and runtime effects:

```text
effectiveCooldown =
    definition.AttackCooldown
    * globalAttackCooldownMultiplier
    * runtimeAttackCooldownMultiplier
```

For the MVP special:

```text
runtimeAttackCooldownMultiplier = 0.5
```

while the special is active.

## Movement Model

Movement should be TFT-like: units do not author movement range or movement duration. All units use the battle's global movement step duration.

Recommended MVP behavior:

```text
If target is in attack range:
    do not move
else:
    find the nearest valid attack position
    path toward it
    start moving one hex toward the target position
```

Movement transition:

```text
When a unit starts moving to an adjacent hex:
    IsMoving = true
    MovementDestination = nextHex
    MovementTimeRemaining = globalMovementStepDuration

Each tick:
    MovementTimeRemaining -= tickDuration

When MovementTimeRemaining <= 0:
    commit CurrentHex = MovementDestination
    IsMoving = false
```

For MVP, a unit should move at most one hex per movement transition.

## Movement Conflict Resolution

Movement conflicts should be resolved deterministically with a shared movement speed.

For each movement intent:

```text
arrivalTime = pathLengthInHexes * globalMovementStepDuration
```

Priority rules:

```text
1. Lower arrivalTime wins.
2. If tied, shorter path length wins.
3. If still tied, lower UnitId wins.
```

Example:

```text
Unit A:
- path length: 1 hex
- arrivalTime = 1 * globalMovementStepDuration

Unit B:
- path length: 2 hexes
- arrivalTime = 2 * globalMovementStepDuration
```

Result: Unit A reserves the contested hex because it is closer.

MVP recommendation:

- Resolve conflicts mainly for the next hex.
- Do not reserve full long paths several seconds ahead.
- If the next hex is occupied or reserved, try an alternate neighboring step.
- If no useful alternate step exists, the unit waits for the next movement decision.

## Attacking Moving Units

For MVP, separate logical position from visual position.

During movement:

```text
The unit visually moves toward its destination,
but gameplay still treats it as standing on its last committed hex.
```

Rules:

```text
1. A moving unit can be attacked.
2. Attack range is checked against the moving unit's current committed hex.
3. A moving unit cannot attack.
4. When the global movement step duration finishes, the unit commits to the destination hex.
5. If a unit dies during movement, its movement is cancelled.
```

Blocking behavior:

```text
A moving unit blocks:
- its current committed hex
- its reserved destination hex
```

This prevents other units from entering either the source or destination hex during the movement animation.

Example:

```text
Warrior moves from Hex A to Hex B.

During movement:
- visually, Warrior is between A and B
- logically, Warrior still occupies Hex A
- Hex A is occupied
- Hex B is reserved

An Archer checks range to Hex A.
If Hex A is in range, the Archer can attack Warrior.

After movement completes:
- Warrior commits to Hex B
- Hex A is released
- future range checks use Hex B
```

Do not calculate attack range against interpolated positions for MVP. That would make range checks, targeting, replay/debugging, and animation synchronization more complex.

## Battle Events

Add events only where UI or debug tooling needs them.

Likely useful additions:

- `UnitManaChanged`
- `UnitSpecialActivated`
- optional `UnitCrit`

Avoid expanding event payloads too broadly until UI requirements are clear.

## Asset Migration

Update all unit assets in `Assets/DeckBattle/Data/Units`.

Recommended initial values:

- `CritChance = 0`
- `CritMultiplier = 2`
- Global movement step duration = `0.4`
- `ManaThreshold = 100`
- `ManaPerAttack = 10`
- `ManaPerDamageTaken = 10`
- `Armor = 0`
- `ArmorPenetration = 0`

These defaults should preserve current combat behavior as much as possible while enabling the new systems.

## Implementation Order

1. Extend `UnitDefinition` with new stats and validation.
2. Update unit assets with safe default values.
3. Add runtime fields to `UnitRuntimeState`.
4. Extract damage calculation into a pure, tested service.
5. Add deterministic critical hits.
6. Add armor and armor penetration.
7. Add mana gain after attacks and after receiving damage.
8. Add MVP special activation and duration handling.
9. Apply runtime attack cooldown multiplier.
10. Use a global TFT-like movement step duration.
11. Add movement conflict resolution by arrival time.
12. Add moving-unit targeting and blocking rules.
13. Add focused battle events for mana and special activation.
14. Update card/debug UI to show new stats.
15. Expand edit mode tests.

## Test Coverage

Add focused edit mode tests for:

- damage without armor matches current behavior
- armor reduces damage
- armor penetration reduces effective armor
- critical hit applies `CritMultiplier`
- crit uses deterministic RNG
- mana increases after attacking
- mana increases after receiving damage
- special activates at `ManaThreshold`
- special resets mana
- special reduces effective attack cooldown by 50%
- special expires after 5 seconds
- moving units cannot attack
- moving units can be attacked on their committed hex
- moving units block both current and destination hex
- faster units can win contested hexes despite starting farther away
- movement conflict tie-breakers are deterministic

## Mobile Performance Notes

- Keep gameplay logic allocation-free during ticks.
- Reuse existing resolver workspaces where possible.
- Avoid LINQ in combat and movement hot paths.
- Keep ability handling simple until unit-specific abilities are actually needed.
- Keep visual interpolation separate from gameplay movement commits.
- Preserve deterministic simulation behavior for testing, debugging, and future replay/multiplayer work.
