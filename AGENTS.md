# Codex Rules for DeckBattle

## Project Context

- This project is a Unity game targeting mobile devices.
- Rendering should use Unity URP.
- Optimization, stable frame time, memory discipline, and battery-friendly behavior have priority over visual complexity.
- Prefer simple, deterministic systems that are easy to profile, test, and tune on real devices.

## Priority Order

1. Keep the game performant on mid-range mobile hardware.
2. Preserve clear gameplay code and predictable data flow.
3. Follow Unity and URP best practices.
4. Keep changes small, focused, and easy to review.
5. Add visual polish only when it does not compromise performance or maintainability.

## Unity Version and Packages

- Do not upgrade Unity, URP, or core packages unless explicitly asked.
- Before using a new package, check whether the same goal can be achieved with Unity built-in APIs or packages already in the project.
- Avoid adding heavy runtime dependencies for small features.
- If a package is needed, explain why it is worth the build size, maintenance, and runtime cost.

## Mobile Performance Rules

- Optimize for stable frame pacing, not only average FPS.
- Avoid per-frame allocations in gameplay, UI, input, animation, and rendering code.
- Avoid LINQ, boxing, string concatenation, reflection, and repeated component lookups in hot paths.
- Cache references used frequently, especially inside `Update`, `LateUpdate`, `FixedUpdate`, coroutines, animations, and UI refresh loops.
- Use object pooling for frequently spawned objects such as cards, effects, projectiles, damage text, particles, and UI list items.
- Avoid unnecessary `Update` methods. Prefer events, timers, state machines, or central tick systems where appropriate.
- Keep physics simple. Avoid expensive continuous collision detection unless necessary.
- Profile before making large optimization changes. Prefer measured fixes over speculative rewrites.
- Test assumptions on device or in a mobile-like profile whenever possible.

## URP and Rendering Rules

- Keep URP settings mobile-oriented.
- Prefer simple lit or unlit shaders over complex custom shader graphs.
- Avoid expensive post-processing by default.
- Avoid real-time shadows unless they are clearly required and budgeted.
- Prefer baked lighting, simple blob shadows, or stylized contact shadows for mobile scenes.
- Keep transparent overdraw low, especially for particles, fullscreen effects, card glows, and UI.
- Keep shader variant count under control.
- Use texture atlases or sprite atlases where appropriate.
- Compress textures for mobile targets and keep texture resolution proportional to on-screen size.
- Do not introduce high-poly meshes, high-resolution textures, or dense particle systems without a clear reason.

## UI Rules

- Build UI to scale cleanly across common phone aspect ratios and safe areas.
- Avoid layout rebuilds every frame.
- Do not update text, images, layout groups, or canvases unless the displayed data actually changed.
- Split static and frequently changing UI into separate canvases when useful.
- Pool repeated UI elements such as hand cards, deck lists, history rows, popups, and combat log entries.
- Keep animations short and lightweight.
- Prefer readable hierarchy and naming over clever UI code.

## Gameplay Code Rules

- Keep card, deck, combat, and rule logic independent from visual presentation where practical.
- Keep simulation logic separate from visuals such as animations, particles, camera shake, sound triggers, and other presentation effects.
- Prefer data-driven card definitions using `ScriptableObject` assets or structured config.
- Keep runtime card state separate from immutable card definitions.
- Avoid hardcoding card behavior in UI components.
- Make gameplay actions deterministic when possible, especially for replay, debugging, tests, and future multiplayer support.
- Centralize random number generation for gameplay logic instead of calling random APIs from scattered code.
- Use clear command/result structures for gameplay actions when the feature complexity justifies it.

## Architecture Rules

- Follow existing project patterns once they exist.
- Keep MonoBehaviours thin when possible. Use them for Unity lifecycle, scene binding, input, and presentation.
- Keep pure gameplay logic in plain C# classes where practical.
- Prefer composition over inheritance for card effects and combat modifiers.
- Avoid global mutable state unless it is intentionally scoped and documented.
- Do not create broad manager classes that own unrelated systems.
- Keep serialization Unity-friendly. Avoid patterns that fight the Inspector without clear benefit.

## Assets and Memory

- Keep asset import settings mobile-friendly.
- Use compressed audio and textures unless quality requirements justify otherwise.
- Avoid loading large assets synchronously during gameplay.
- Use Addressables or another project-approved loading strategy only when the project scale requires it.
- Release references to temporary assets and pooled content when leaving screens or battles.
- Be careful with static references that can prevent cleanup.

## Input and Device Rules

- Design for touch first.
- Keep tap targets large enough for phones.
- Account for safe areas, notches, and different aspect ratios.
- Avoid interactions that require precise mouse-like input.
- Support pause, app backgrounding, and resume without corrupting battle state.
- Avoid battery-heavy behavior while menus are idle.

## Testing and Verification

- When interacting with Unity Editor, always use Unity MCP first for editor state, compilation checks, asset inspection, scene operations, and test automation.
- Use Unity CLI only as a fallback when MCP cannot perform the required action, or for CI/batch workflows where the project is not already open in the Editor.
- Never run edit mode tests in Unity batchmode.
- For gameplay logic, prefer edit mode tests on pure C# systems.
- For Unity scene or prefab behavior, use play mode tests when the risk justifies it.
- After code changes, run the narrowest relevant tests first.
- If tests cannot be run locally, state that clearly and explain what was checked instead.
- For performance-sensitive changes, include expected profiling points or counters to inspect.

## Code Style

- Use C# conventions common in Unity projects.
- Keep names explicit and domain-focused.
- Avoid unnecessary abstractions, factories, services, and generic frameworks.
- Prefer small methods with clear responsibilities.
- Add comments only for non-obvious decisions, performance constraints, or Unity-specific lifecycle reasons.
- Do not mix formatting-only changes with behavioral changes unless asked.

## Git and File Safety

- Do not revert user changes unless explicitly asked.
- Keep edits scoped to the requested task.
- Do not rename or move Unity assets casually, because `.meta` GUID stability matters.
- When moving or renaming Unity assets, preserve associated `.meta` files.
- Do not delete generated Unity folders such as `Library`, `Temp`, `Obj`, or build outputs unless explicitly asked.

## Before Finishing a Task

- Check whether the change affects mobile performance, memory, loading time, or build size.
- Check whether the change affects URP settings, shader variants, texture memory, or overdraw.
- Check whether gameplay logic remains separated from presentation where practical.
- Run relevant tests or explain why they were not run.
- Summarize what changed and any remaining risk clearly.
