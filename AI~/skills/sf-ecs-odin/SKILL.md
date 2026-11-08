---
name: sf-ecs-odin
description: Use when adding SFramework embedded ECS worlds, systems, SFEntity authoring, generated component providers, or Odin ECS inspection.
---

# sf-ecs-odin

Read [the package guide](references/package-readme.md) and [installed source](SOURCE.md) for world JSON and component/system examples.

Register Configs before ISFWorldsService. Named SFEntity views enabled before world initialization wait for the configured worlds. SFWorldsConfig can omit world capacity settings and unused phase arrays. Use the embedded ECS contracts in SFramework.ECS.Runtime and preserve the embedded kernel's original license and provenance notice.

World lookup uses full configuration paths such as ECS/Gameplay and supports unambiguous short IDs. Empty, unknown, and ambiguous names select the fallback world, which has no configured systems.

Use parameterless concrete system constructors and assembly-qualified System strings. Phase arrays define order and create separate instances. SFEcsSystems.Init uses SF Core's generated field injection before all PreInit/Init callbacks, including for nested SFEcsGroupSystem children. SFSystem implements ISFInjectable and uses Tick(ref ISFEcsSystems). Use mutable service fields in partial consumers and an ISFEcsContext field to access pipeline worlds, pools, filters and shared data. Configured pipelines expose named worlds under full IDs. Cache ECS data in ISFInjectionCallback or Init when needed.

Use AddGroup and SFEcsGroupSystemState for named switchable groups, and DelHere<T> to clear an event component at a specific step of the run phase. In Unity Play Mode, open Window > SFramework > ECS Monitor to inspect live worlds and add, remove, or edit components. Edits use generation-checked entity handles. Removing the last component also removes its entity.

Open Window > SFramework > ECS Systems Profiler to measure Run and PostRun per system, phase and world. Group timings include nested callbacks. Standalone pipelines use the Manual phase. Record toggles sampling and Clear resets the measurements.

For Unity component providers use SFGenerateComponent on a struct whose source file matches its type name. The editor generator writes an underscore-prefixed provider beside it. Regenerate deliberately and preserve user-authored component data.

SFEntity normally retains its ECS entity on disable. Set removeEntityOnDisable to delete it on disable. Unpack mapped packed handles to check liveness. SFWorldsService owns its relay, all its worlds, and the configured pipelines. Dispose the service or its container to release them.

Verify world selection, phase order, entity enable/disable/recreation, generated authoring and repeated startup/shutdown in the consuming project.
