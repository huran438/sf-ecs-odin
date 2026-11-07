# Embedded ECS implementation plan

**Goal:** Make sf-ecs-odin self-contained by embedding the complete installed ECS runtime and using sf-core for dependency injection.

**Architecture:** Keep EcsWorld, EcsPool, EcsFilter, packed entities, system interfaces and lifecycle behavior, moving them into SFramework.ECS.Runtime. EcsSystems.Init injects ISFInjectable systems with SFContainer before any PreInit/Init callback. SFSystem keeps its PreInit interface but no longer injects a second time.

**Tech stack:** Unity 6000.0.71f1, C#, Unity Test Framework, SFContainer.

## Constraints and decisions

- Embed all five runtime source files from the host's locked ECS runtime revision d46f3b209ea163810823aeea7d2610d38ad6cdf8.
- Preserve upstream copyright, full MIT-Red license, compile-time switches and runtime behavior apart from namespace and SFContainer integration.
- Keep type names and SFramework assembly names. Consumers use SFramework.ECS.Runtime.
- Use SFInject for service fields, properties and methods, and systems.GetWorld()/GetPool<T>()/Filter<T>() for ECS data.
- Embed the kernel into the existing runtime assembly so other packages can retain their own external ECS dependencies.
- Update sf-ecs-tween and the sf-demo host together so both external packages can actually be removed.
- Preserve pre-existing uncommitted changes. Work in the current local package paths used by the running Unity host; no commits or branch operations.

## Tasks

- [x] Add and run migration regression tests: runtime ownership, injection into plain systems before lifecycle callbacks, SFSystem injection exactly once.
- [x] Copy the complete kernel and license; integrate Core injection; remove external using directives and asmdef references.
- [x] Remove both packages from host manifests and package-switcher settings; update documentation and package AI knowledge, then sync the host.
- [x] Verify copied source parity, lifecycle, entity/pool/filter behavior and all EditMode/PlayMode tests without external packages. Review final changes.

## Review focus

- Private SFInject members and multiple injection methods must be filled before any PreInit callback.
- SFSystem must receive exactly one injection per pipeline initialization.
- Named worlds, shared data and reverse teardown order retain upstream semantics.
- Pool recycling, packed-entity invalidation and filters mutated during enumeration retain upstream behavior.
- The host and tween assembly compile after removing both external package dependencies.

## Evidence

- Baseline: Unity EditMode, 197 passed, 0 failed.
- Regression before implementation: ECS suite 15 passed, 2 failed as expected (external runtime ownership and missing Core injection before PreInit).
- After embedding: ECS suite 17 passed, 0 failed.
- Source parity: all five files match upstream after accounting for namespace and the documented Core injection loop; the license matches byte-for-byte.
- After removing both external packages: full EditMode suite 200 passed, 0 failed; Unity PackageInfo returned no registered external ECS packages.
- Added kernel characterization checks for growth/recycling, stale packed handles, auto-copy/reset, include/exclude filter mutations, shared/named worlds and lifecycle/teardown order.
- Final full EditMode suite: 204 passed, 0 failed, 0 skipped (job 8171204db82e44bdac3945c6db7571e5).
- Final PlayMode suite: 8 passed, 0 failed, 0 skipped (job 800efae30ff8402791d09535b8109d37). Console contains no errors after this run.
- Independent read-only review found no actionable migration issue. Corrected one outdated README API-table description. Pre-existing Core reflection and WorldsService resource-ownership behavior are unchanged.
- Package AI knowledge synchronized and freshness check passed. No commits, pushes or player builds performed.
