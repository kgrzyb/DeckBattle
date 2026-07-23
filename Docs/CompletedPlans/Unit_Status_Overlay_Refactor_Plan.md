# Unit Status Overlay Refactor Plan

## Goal

Refactor unit HP bars into a UI overlay system and add a mana bar for every unit.

The overlay should:

- keep HP bars visible at all times for living units, including full HP;
- keep mana bars visible at all times;
- follow unit world positions from the UI canvas;
- hide immediately when a unit dies;
- reset mana after each round, like unit HP;
- stay lightweight enough for mobile play.

## Current State

- `UnitView` owns a world-space `UnitHealthBarView`.
- `UnitHealthBarView.SetHealth()` hides the bar when HP is full.
- `UnitRuntimeState` already has `CurrentMana`.
- `UnitDefinition` already has `ManaThreshold`, `ManaPerAttack`, and `ManaPerDamageTaken`.
- `BattleEvent.UnitManaChanged` already exists.
- `BattleView.ProcessEvents()` currently does not handle mana UI updates.
- `RuntimeUnit` does not need persistent mana because mana should reset each round.

## Product Decisions

- HP bars are always visible for living units.
- Mana bars are always visible, even at zero mana.
- Mana resets after every round and is not persisted in `RuntimeUnit`.
- The overlay disappears immediately on `UnitDied`, while the unit model may continue its death animation.
- Units with `ManaThreshold <= 0` should keep a stable bar layout. The recommended presentation is an empty or dimmed mana bar rather than hiding it.

## Proposed Architecture

### UnitStatusOverlayView

Create a UI prefab component responsible only for rendering one unit status overlay.

Suggested fields:

- HP fill image;
- mana fill image;
- optional HP text;
- optional mana text;
- cached shown HP, max HP, mana, and max mana values.

Suggested methods:

- `Bind(int unitId, Transform target, int currentHp, int maxHp, int currentMana, int maxMana)`
- `SetHealth(int currentHp, int maxHp)`
- `SetMana(int currentMana, int maxMana)`
- `SetVisible(bool visible)`
- `Release()`

Implementation notes:

- Clamp max HP to at least `1`.
- Clamp max mana to at least `1` for fill math.
- Update images and text only when values change.
- Do not allocate strings every frame.

### UnitStatusOverlayController

Create a controller on the battle UI canvas that owns all active status overlays.

Responsibilities:

- keep a dictionary from unit id to overlay;
- pool overlay views instead of destroying and instantiating repeatedly;
- bind overlays to `UnitView.transform`;
- update screen positions in `LateUpdate`;
- hide overlays when their target disappears or is explicitly released;
- expose clear APIs for `BattleController` and `BattleView`.

Suggested methods:

- `BindRuntimeUnit(RuntimeUnit unit, UnitView view)`
- `BindRealtimeUnit(UnitRuntimeState unit, UnitView view)`
- `SetHealth(int unitId, int currentHp, int maxHp)`
- `SetMana(int unitId, int currentMana, int maxMana)`
- `Release(int unitId)`
- `ReleaseAll()`

Positioning notes:

- Use one cached world camera reference.
- Use `Camera.WorldToScreenPoint`.
- Convert to the overlay root with `RectTransformUtility.ScreenPointToLocalPointInRectangle`.
- Apply a serialized vertical world offset so the overlay sits above the unit.
- Disable overlays when a unit is behind the camera.

## Integration Plan

### Preparation Phase

Update `BattleController`:

1. Add a serialized `UnitStatusOverlayController`.
2. When `CreateOrUpdateUnitView(RuntimeUnit unit)` binds a unit view, bind or update the overlay.
3. During preparation, pass mana as `0` and max mana as `unit.Definition.ManaThreshold`.
4. In `UpdateUnitView(RuntimeUnit unit)`, rely on overlay target tracking instead of manually moving UI.
5. In `ClearUnitViews()`, call `ReleaseAll()` on the overlay controller.
6. In `ReclaimUnitViews(...)`, rebind overlays after unit views return from `BattleView`.

### Realtime Combat

Update `BattleView`:

1. Add a serialized `UnitStatusOverlayController`.
2. When `CreateOrUpdateUnitView(UnitRuntimeState unit)` binds a unit view, bind or update the overlay.
3. On `UnitDamaged`, update HP through the overlay controller.
4. Add handling for `BattleEventType.UnitManaChanged`.
5. On `UnitDied`, immediately release the unit overlay.
6. In `ClearBattle(true)`, release all overlays owned by the realtime combat view.
7. In `ClearBattle(false)`, do not release overlays if ownership is being handed back to `BattleController`; instead let `BattleController.ReclaimUnitViews` rebind them.

### UnitView Cleanup

Update `UnitView`:

1. Remove the direct `UnitHealthBarView` dependency from new code paths.
2. Remove health bar scale compensation logic once the prefab no longer has a child health bar.
3. Keep health state methods only if they are still useful for damage animation timing; otherwise let overlay controller own status rendering entirely.

Prefab updates:

1. Remove or disable the old world-space health bar from `PF_UnitView`.
2. Create `PF_UnitStatusOverlay` under `Assets/DeckBattle/Prefabs/Battle`.
3. Add an overlay root under the battle UI canvas.
4. Wire `UnitStatusOverlayController` references in the battle scene.

## Mana Behavior

Mana remains a realtime combat value:

- `UnitRuntimeState.CurrentMana` starts at `0`.
- mana increases from combat events already emitted by `CombatResolver`;
- mana resets when a new `UnitRuntimeState` is created or reset for battle;
- `RuntimeUnit` should not store mana.

During preparation:

- show mana as `0 / ManaThreshold`;
- keep the mana bar visible.

During combat:

- show `CurrentMana / ManaThreshold`;
- update when `UnitManaChanged` is received;
- reset display to zero when special activation resets mana.

## Performance Notes

- Avoid `Camera.main` lookups per frame.
- Avoid layout rebuilds every frame.
- Avoid `Instantiate` and `Destroy` during regular round transitions by pooling overlay views.
- Avoid LINQ and per-frame allocations in overlay update loops.
- Update fill/text values only when changed.
- Keep overlay hierarchy simple to limit canvas rebuild cost.
- If overlay count grows, consider splitting static HUD and moving unit overlays into separate canvases.

## Suggested Tests

Add focused edit mode tests for gameplay state:

- `UnitRuntimeState` starts combat with `CurrentMana == 0`.
- mana resets when a unit is reset for battle.
- `CombatResolver` emits `UnitManaChanged` after attack mana gain.
- `CombatResolver` emits `UnitManaChanged` after damage-taken mana gain.
- special activation resets mana to zero.

UI behavior is better verified in Play Mode or manually:

- full HP units still show HP bars;
- mana bars are visible at zero;
- overlays follow moving units;
- overlays disappear immediately on unit death;
- overlays do not duplicate after Preparation -> Combat -> RoundResolution transitions.

## Implementation Order

1. Add `UnitStatusOverlayView`.
2. Add `UnitStatusOverlayController` with pooling and screen-position tracking.
3. Integrate overlays into `BattleController` for preparation.
4. Integrate overlays into `BattleView` for realtime combat.
5. Handle `UnitManaChanged` in `BattleView`.
6. Remove or bypass old world-space health bar usage from `UnitView`.
7. Update prefabs and scene references.
8. Add focused mana reset/event tests.
9. Verify in Play Mode on common mobile aspect ratios.

## Open Risks

- Overlay ownership handoff between `BattleController` and `BattleView` must be explicit to avoid duplicate bars during combat transitions.
- Canvas rebuild cost should be checked after implementation, especially if unit count increases.
- If old world-space bars remain on prefabs during migration, duplicate HP bars may appear.
