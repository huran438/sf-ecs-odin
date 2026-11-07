# ECS naming and style implementation plan

**Goal:** Move the five embedded ECS sources to the Runtime root, use the user's selected SFEcs/ISFEcs names and match the package's C# style.

**Design:** Rename EcsWorld/Pool/Filter/Systems/PackedEntity to SFEcsWorld/Pool/Filter/Systems/PackedEntity. Apply the same prefix to related ECS contracts and extensions; retain the existing ISFSystem authoring marker and SFEntity.EcsPackedEntity property. Move script metadata with the source files to retain GUIDs. Keep the complete runtime algorithms, SF Core injection and compiler switches unchanged.

**Style:** Four spaces, Allman braces, ordinary C# invocation spacing, expanded statement blocks and explicit private/internal modifiers. Use a local Roslyn formatter across editor, debug, release, event and IL2CPP branches; record the style in .editorconfig.

- [x] Move and rename the five sources plus metadata; update contracts and consumers.
- [x] Format sources and verify token equivalence across compiler configurations, accounting only for renames and explicit default access modifiers.
- [x] Update README and package AI knowledge; keep upstream license/provenance at package root.
- [x] Run all Unity EditMode/PlayMode tests, verify metadata GUIDs and check final references.

**Verification:** Existing behavior tests cover runtime ownership, Core injection order, entities/pools/filters and system lifecycle. This refactor adds no new behavior, so it does not add tests that merely assert spellings or layout.

## Evidence

- All five script metadata files match the original bytes and GUIDs.
- Token and preprocessor-directive parity verified under four sets of debug/release, editor/player, IL2CPP and event symbols, after accounting for type renames and explicit default access modifiers.
- dotnet-format whitespace verification passed for all five runtime sources using .editorconfig. Git diff whitespace checks passed.
- Unity successfully compiled the renamed runtime; live reflection resolved SFEcsWorld, SFEcsPool and ISFEcsSystems from SFramework.ECS.Runtime.
- Fresh EditMode run after successful compilation: 204 passed, 0 failed, 0 skipped (job 4d3d2776c9bf4d268d5c126cf3a1c627).
- Final PlayMode job 2807fc2ca18f4c818220bc6521c5b018 succeeded, completing 8 of 8 tests with no reported failures. Console contains no errors.
- Independent read-only review found no actionable naming/formatting issue.
- Package AI knowledge synchronized and freshness check passed. No old ECS type references remain in authored C# consumers, apart from the deliberately retained packed-entity property and the unchanged upstream IEcsCopy diagnostic/reflection string.
