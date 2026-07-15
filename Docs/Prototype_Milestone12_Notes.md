# Deck Battle - Prototype Milestone 12 Notes

## Cleanup Done

- Removed the temporary `playInitialVisibleUnits` scene/debug shortcut.
- Renamed placeholder runtime assets:
  - `PF_UnitPlaceholder` -> `PF_UnitView`
  - `M_UnitPlaceholder` -> `M_Unit`
- Kept battle rules in pure services; UI still delegates actions to `BattleController`.
- Reduced combat movement allocations by reusing a movement workspace during `CombatSimulator.Simulate`.
- Added an edit mode test for reusing the movement workspace across moves.

## Known Prototype Limits

- Combat presentation is still a state snap, not a full event playback animation pass.
- Unit and card visuals are still simple MVP assets.
- `BattleController` still owns scene orchestration for the prototype; split only if battle flow grows.
- Runtime unit/card pooling is partial: card UI and board rebuilds are acceptable for scene start and restart, but unit view pooling should be revisited before longer sessions or mobile profiling.
- Profiler pass should focus on combat tick allocations, UI rebuilds during hand refresh, and object lifetime during restart.

## Next Profiling Points

- Verify `CombatSimulator.Simulate` allocations with deep profiling disabled and GC allocation column visible.
- Check Canvas rebuilds when cards are played and when a new round draws cards.
- Check restart flow for leftover scene objects under `UnitsRoot` and `BoardRoot`.
