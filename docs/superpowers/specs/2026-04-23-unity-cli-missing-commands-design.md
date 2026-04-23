# unity-cli Missing Commands Design

Date: 2026-04-23
Repo: `/Users/ertugrulkara/Desktop/unity-cli`
Scope: Add four frequently used Unity inspection and capture capabilities to `unity-cli` as first-class grouped commands backed by dedicated Unity-side handlers.

## Summary

`unity-cli` already has the right overall shape for this project: a small Go CLI, a thin Unity connector, direct commands for common operations, and a dedicated test runner. The missing gap is a small set of high-frequency UI and hierarchy workflows that are currently handled better by Coplay:

- capture a specific UI canvas
- capture a specific scene object subtree
- inspect a game object with stable structured output
- list a game object hierarchy with bounded depth

These should be added as first-class CLI commands with exact-only targeting rules and JSON-first output. They should not be implemented as ad hoc `exec` snippets or generic custom tools only.

## Goals

- Make the four missing workflows reliable enough for repeated agent-driven use.
- Preserve `unity-cli`'s small, intentional command surface.
- Reuse existing screenshot infrastructure where it is already correct.
- Keep the correctness-critical logic inside dedicated Unity-side handlers.
- Support prefab inspection for non-screenshot inspection commands.

## Non-Goals

- Full Coplay parity.
- Fuzzy object lookup.
- Pixel-perfect image-diff testing.
- Prefab screenshot capture.
- Large serialized field dumps by default.

## Chosen Approach

Use grouped first-class CLI commands in Go, each backed by a dedicated Unity-side handler:

- `unity-cli ui capture-canvas`
- `unity-cli scene capture-object`
- `unity-cli gameobject info`
- `unity-cli gameobject list`

The transport-level Unity handler names can remain descriptive and Coplay-like internally:

- `capture_ui_canvas`
- `capture_scene_object`
- `get_game_object_info`
- `list_game_objects_in_hierarchy`

This gives the CLI a stable public interface without forcing the Unity connector into a vague generic-tool-only model.

## Why This Approach

### Rejected: custom tools only

This would minimize Go changes, but it weakens the interface for commands that will be used constantly. The exact same Unity-side logic could be correct, but the user-facing and agent-facing surface would be looser, harder to validate, and less discoverable.

### Rejected: one generic gameobject command family

This would reduce the number of top-level groups, but it would blur the boundary between UI capture, scene capture, and hierarchy inspection. The command tree would become less intentional over time.

## Command Surface

### `ui capture-canvas`

Capture a specific `Canvas` object by exact path.

Example:

```bash
unity-cli ui capture-canvas --path HUD/MainCanvas[0]
unity-cli ui capture-canvas --path HUD/MainCanvas[0] --output captures/main-hud.png
unity-cli ui capture-canvas --path HUD/MainCanvas[0] --width 2560 --height 1440
```

Rules:

- scene object only
- exact path only
- target must resolve to a `Canvas`
- works in edit mode and play mode
- `--output` optional

### `scene capture-object`

Capture a specific scene object subtree by exact path.

Example:

```bash
unity-cli scene capture-object --path Units/BossRoot[0]
unity-cli scene capture-object --path BattleScene::Units/BossRoot[0] --output captures/boss.png
```

Rules:

- scene object only
- exact path only
- isolate target subtree when technically possible
- works in edit mode and play mode
- `--output` optional

### `gameobject info`

Return a stable core schema for one object.

Examples:

```bash
unity-cli gameobject info --path HUD/MainCanvas[0]/Panel[1]
unity-cli gameobject info --prefab Assets/Prefabs/UI/MainHUD.prefab --path Root/MainCanvas[0]
```

Rules:

- exact path only
- supports scene objects and prefabs
- core schema by default
- no full serialized dump by default

### `gameobject list`

Return a bounded hierarchy listing.

Examples:

```bash
unity-cli gameobject list --path HUD/MainCanvas[0]
unity-cli gameobject list --path HUD/MainCanvas[0] --depth 3
unity-cli gameobject list --path HUD/MainCanvas[0] --recursive
unity-cli gameobject list --prefab Assets/Prefabs/UI/MainHUD.prefab --path Root
```

Rules:

- exact path only
- supports scene objects and prefabs
- omitted `--path` means list from the source root
- default depth is one level
- `--depth` and `--recursive` extend the traversal

## Path Resolution Rules

Path resolution is the core reliability dependency for all four commands.

### Exact-only lookup

- No fuzzy matching
- No partial-name fallback
- No implicit “best guess”

### Duplicate sibling disambiguation

Paths must support duplicate sibling names using bracketed indices:

```text
HUD/MainCanvas[0]/Panel[2]/Button[0]
```

If duplicate sibling names exist and no index is provided, return an ambiguity error.

### Scene qualification

Scene object commands use this rule:

- if exactly one scene is loaded, the active scene is implied
- if multiple scenes are loaded, an explicit scene-qualified path is required
- if resolution is ambiguous, error

Qualified format:

```text
BattleScene::HUD/MainCanvas[0]
```

### Prefab qualification

Prefab access is explicit and separate from scene qualification:

```bash
unity-cli gameobject info --prefab Assets/Prefabs/UI/MainHUD.prefab --path Root/MainCanvas[0]
```

Screenshots do not support prefabs.

## Output Format

All four commands are JSON-first.

The CLI should keep using its existing pretty-printed JSON output behavior for structured results.

### Success shape

Each success response should contain:

- `command`
- `target`
- `resolvedPath`
- `mode`
- `source`
- command-specific `data`

Where:

- `mode` is `edit` or `play`
- `source` is `scene` or `prefab`

### Error shape

Each error response should contain:

- `code`
- `message`
- `details`

Expected error codes:

- `object_not_found`
- `ambiguous_path`
- `scene_required`
- `prefab_not_found`
- `invalid_target_type`
- `capture_failed`
- `unsupported_mode`

## Runtime Design

### Go CLI layer

Responsibilities:

- parse grouped commands and flags
- normalize the public interface
- send the correct Unity command name and parameters
- preserve JSON-first output
- keep help text and examples aligned with the new groups

The Go side should not perform scene traversal or path resolution logic.

### Unity connector layer

Responsibilities:

- resolve exact scene and prefab targets
- capture images
- serialize stable object and hierarchy data
- restore temporary editor state after captures

This is where the correctness-sensitive logic belongs.

### Shared Unity-side infrastructure

Add shared helpers instead of duplicating logic inside each handler:

- exact path parser with `Name[index]` support
- scene-qualified resolver
- prefab loader and cleanup helper
- hierarchy serializer
- component summarizer
- screenshot writer helper

The shared screenshot writer should reuse the existing low-level parts of `EditorScreenshot.cs`:

- output path resolution
- width and height handling
- render-to-texture and PNG write flow
- success and error response shape

The current scene/game camera targeting logic should not be reused as the target-selection model for the new commands.

## Command Behavior Details

### `ui capture-canvas`

Flow:

1. Resolve exact scene target
2. Verify target is a `Canvas`
3. Build a capture path specific to that canvas
4. Write PNG to explicit or default output path
5. Return JSON metadata

Return data should include:

- written file path
- width
- height
- scene name
- resolved object path
- mode

### `scene capture-object`

Flow:

1. Resolve exact scene target
2. Temporarily isolate the target subtree when possible
3. Capture the result
4. Restore all modified state even if capture fails
5. Return JSON metadata

Isolation may involve temporarily disabling unrelated renderers or canvases. Restoration is mandatory. If the subtree cannot be isolated safely, return `capture_failed` instead of silently falling back to a noisy full-scene capture.

### `gameobject info`

Default schema should include:

- name
- exact resolved path
- scene or prefab source
- active state
- layer
- tag
- child count
- component type list
- transform summary
- rect transform summary when present
- prefab linkage summary when present

Optional future expansion can add a deeper inspection flag, but this design does not require it.

### `gameobject list`

Each node should include:

- name
- exact path
- active state
- child count
- abbreviated component list
- whether children were truncated by depth

The default result should be bounded and easy for agents to consume.

## Default Output Paths For Screenshots

`--output` is optional.

If omitted, the Unity side should write to a deterministic default location under the project with an auto-generated unique filename, for example:

```text
Screenshots/ui-capture-<timestamp>.png
Screenshots/scene-capture-<timestamp>.png
```

The exact file path written must always be returned in JSON.

## Mode Support

Screenshot commands must work in both:

- edit mode
- play mode

The response must include the current mode so callers can distinguish runtime verification from editor preview capture.

## Testing Strategy

### Go tests

Add CLI tests for:

- grouped command parsing
- flag mapping
- parameter construction
- public command help routing

### Unity edit-mode tests

Add tests for:

- exact path parsing
- duplicate sibling ambiguity behavior
- bracketed index disambiguation
- one-scene implicit resolution
- multi-scene explicit-scene requirement
- prefab load and unload cleanup
- default depth vs recursive hierarchy traversal
- stable info schema serialization

### Unity screenshot tests

Add tests for:

- non-canvas rejection for `capture-canvas`
- file creation at explicit output path
- file creation at default output path
- metadata correctness
- subtree isolation restoration

These tests should verify dimensions, file creation, and metadata rather than brittle full-image pixel comparisons.

## Risks And Mitigations

### Risk: path syntax drift between commands

Mitigation:

- one shared resolver used by all four handlers
- resolver tested directly

### Risk: scene capture leaves editor state dirty

Mitigation:

- explicit restore blocks
- tests that verify restore behavior

### Risk: prefab handling leaks loaded contents

Mitigation:

- one shared prefab lifecycle helper
- explicit unload in both success and failure paths

### Risk: outputs become too verbose for agents

Mitigation:

- core schema by default
- bounded list depth by default
- JSON-first stable shapes

## Implementation Boundaries

This spec covers only:

- the public command surface
- runtime behavior
- path semantics
- error semantics
- testing expectations

It does not prescribe the exact file layout beyond requiring shared resolver and serializer helpers. The implementation plan should choose the smallest clean file structure that matches existing `unity-cli` patterns.

## Recommendation

Implement the four missing workflows as grouped first-class CLI commands backed by dedicated Unity-side handlers, with one shared exact-path resolution system and one shared serializer layer.

This is the smallest design that closes the main Coplay gap without dragging `unity-cli` toward MCP-style surface bloat.
