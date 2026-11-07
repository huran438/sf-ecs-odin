---
name: sf-ecs-odin
description: Use when adding SFramework embedded ECS worlds, systems, SFEntity authoring, generated component providers, or Odin ECS inspection.
---

# sf-ecs-odin

Read [the package guide](references/package-readme.md) and [installed source](SOURCE.md) for world JSON and component/system examples.

Register Configs before ISFWorldsService. Named SFEntity views enabled before world initialization wait for the configured worlds. SFWorldsConfig can omit world capacity settings and unused phase arrays. Use the embedded ECS contracts in SFramework.ECS.Runtime and preserve the embedded kernel's original license and provenance notice.

World lookup uses full configuration paths such as ECS/Gameplay and supports unambiguous short IDs. Empty, unknown, and ambiguous names select the fallback world, which has no configured systems.

Use parameterless concrete system constructors and assembly-qualified System strings. Phase arrays define order and create separate instances. SFEcsSystems.Init injects ISFInjectable systems through SFContainer before all PreInit/Init callbacks. SFSystem implements that marker and uses Tick(ref ISFEcsSystems). Register services before pipeline initialization; use mutable service fields in partial consumers (no SFInject; ISFService contracts already carry ISFDependency) for services and systems.GetWorld()/GetPool<T>()/Filter<T>() for ECS data. Use the pipeline lifecycle for injection.

For Unity component providers use SFGenerateComponent on a struct whose source file matches its type name. The editor generator writes an underscore-prefixed provider beside it. Regenerate deliberately and preserve user-authored component data.

SFEntity normally retains its ECS entity on disable. Set removeEntityOnDisable to delete it on disable. Unpack mapped packed handles to check liveness. SFWorldsService owns its relay, all its worlds, and the configured pipelines. Dispose the service or its container to release them.

Verify world selection, phase order, entity enable/disable/recreation, generated authoring and repeated startup/shutdown in the consuming project.
