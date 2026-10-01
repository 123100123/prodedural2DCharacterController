---
name: unity-clean-code-campaign
description: Plan and execute substantial Clean Code refactoring campaigns for existing Unity modules while preserving gameplay, serialized assets, GUIDs, user work, and reviewable Git history. Use for module-wide Unity audits and phased refactors; do not use for ordinary small fixes or isolated feature edits.
---

# Unity Clean Code Campaign

Refactor an existing Unity subsystem through evidence-backed, behavior-preserving phases. Optimize
for clearer ownership and safer future changes, not fewer lines or more classes.

Apply the repository's general clean-code rules as the baseline. This skill adds the Unity-specific
campaign procedure learned from refactoring a coupled Inventory, crafting, item-effect, combat, and
UI subsystem. Project instructions and the user's explicit choices always take precedence.

## Establish the Campaign Contract

Before editing production code, agree on or recover these constraints:

- subsystem breadth and refactoring depth;
- behavior-preserving policy and any separate bug-fix lane;
- one independently revertible commit per approved phase;
- human Unity validation required before each phase commit;
- generated, vendored, user-owned, or otherwise protected files;
- whether the execution plan or ledger must remain local and uncommitted;
- whether Unity automation is available or testing must remain human-driven;
- commit message form, including whether trailers or tool attribution are forbidden.

Do not treat approval for a refactor as permission to alter gameplay, delete uncertain assets, push
commits, or overwrite unrelated work.

The user may change the gate mid-campaign, for example replacing the per-phase manual Unity gate with
"branch, implement every phase, compile-check only, commit per phase, merge on my word". Record the
amendment in the plan and the ledger, keep the compile check, and state in the ledger exactly which
Unity suites and manual scenarios were not run after the change. A dropped gate is a recorded risk,
not a reason to skip verification you can still do.

## Recover State Before Acting

At the beginning of every session or phase:

1. Read repository instructions, the campaign plan or ledger, architecture decisions, and the
   latest relevant commits.
2. Run `git status --short` and classify every existing modification as campaign work, user work,
   generated verification state, or unknown.
3. Record protected paths. Never stage, stash, revert, move, or reformat them incidentally.
4. Re-read the current target files, callers, tests, prefabs, scenes, ScriptableObjects, and metas.
   Earlier phases may have invalidated remembered structure.
5. State the phase outcome and the evidence that will prove it.

If the tree is dirty, work around it with exact paths. A dirty tree is not permission to clean it.

Keep the ledger's progress list at one line per plan phase with its commit hash or status. A coarse
"remaining work" item that hides several phases misreports progress to the next session; so do
finding tables that still cite pre-rename file names. Refresh both whenever a phase lands.

## Audit Before Planning

Inventory every production file in scope, including files created during the campaign. For each
class and significant method, record:

- its one-sentence primary responsibility;
- the actor or reason that causes it to change;
- important state and collaborators;
- inbound callers and outbound dependencies;
- serialized fields, UnityEvents, prefab/scene references, and meta GUID;
- test coverage and Unity-only behavior;
- a disposition: `KEEP`, `SIMPLIFY`, `EXTRACT`, `MOVE`, `RENAME`, `DELETE`, or `DEFER`.

Trace behavior across subsystem boundaries. Unity features often span domain state, services,
ownership, presenters, MonoBehaviours, generated UI objects, physics, and lifecycle callbacks. A
file-by-file review without that flow misses the dangerous coupling.

Establish a baseline before structural edits:

- build the applicable assemblies;
- identify which tests genuinely run outside Unity;
- have the user run the relevant Unity tests and smoke paths when required;
- record existing warnings and failures rather than attributing them to later phases.

Do not delete a failing test to make the baseline green. First determine whether the failure is a
real regression, a stale expectation, or a runner/version incompatibility.

## Design Reviewable Phases

Group work by cohesive change axis, not arbitrary file count. Prefer phases broad enough to produce
a meaningful improvement but small enough to validate and revert independently. Common axes are:

1. focused characterization tests;
2. correctness-critical bug fixes isolated from refactoring;
3. authoritative domain policy and state transitions;
4. ownership, reconciliation, events, and lifecycle coordination;
5. shared presentation semantics and details UI;
6. item visuals, input geometry, animation, and physics seams;
7. reference-proven deletion, naming, and serialized-contract cleanup;
8. profiler-gated performance work.

Order phases by dependency. Policy and state transitions should stabilize before UI coordination;
UI meaning should stabilize before presentation cleanup; deletion and broad renames belong near the
end.

Every phase definition must name:

- exact findings and expected paths;
- behavior that must remain unchanged;
- responsibility changes and rejected alternatives;
- automated checks;
- manual Unity matrix;
- serialization and regression risk;
- proposed commit message and rollback boundary;
- explicit stop condition before commit.

Merge phases only when they share one behavioral seam and one manual test matrix. Split a phase when
its diff can no longer be explained by one intent.

## Implement Around Real Responsibilities

Use the following design tests while editing:

- A class or method should be describable without "and", "also", or "then".
- An orchestrator may sequence collaborators but should not contain their policies.
- Domain decisions must not depend on MonoBehaviours, Canvas objects, physics components, or scene
  timing.
- Unity adapters should apply decisions and manage lifecycle; they should not rediscover policy from
  generated hierarchy.
- A new class needs an independent reason to change. Do not create a `Planner`, `Manager`, or
  forwarding wrapper when the existing cohesive owner can express the operation clearly.
- Prefer explicit state transitions over correctness that depends on a frame delay or incidental
  callback order. Retain a Unity scheduling wait when evidence shows it is genuinely required.
- Share gameplay meaning before UI formatting. Multiple screens may format differently while using
  one semantic projection.
- Events should describe completed outcomes, not expose partially mutated state.
- A public overload whose only callers are tests is production code that drifts from production
  semantics (it will skip an ordering or validation step the real path applies). Remove it and let
  the tests go through the production path with an explicit helper.
- Before keeping a frame wait (`yield return null`) or a "readiness" handshake, trace the actual event
  flow: a `Changed` handler that reconciles views synchronously, a forced layout rebuild before that
  reconcile, or an `[InitializeOnLoad]` initializer usually means the views already exist when the
  call returns. Delete the wait under a test that pins the synchronous contract, and record the trace.
- In a project with one game assembly and no `InternalsVisibleTo`, `internal` hides nothing from
  production code and only blocks the Editor test assembly. A pure policy extracted so it can be
  tested directly may be `public static`; record the reason.

Extract pure calculations from Unity mutation when this creates a meaningful test boundary. Useful
examples include placement policy, popup positioning, rotated footprint geometry, containment, and
animation progression. Keep the MonoBehaviour as a humble adapter.

### Mandatory readability and responsibility gate

Apply these rules to every production edit before presenting a phase for validation:

- **Clear names:** names must reveal the domain concept, decision, result, or side effect. Generic
  verbs such as `Handle`, `Process`, `Run`, `Execute`, `Play`, `Set`, `Get`, or `Calculate` are not
  sufficient by themselves. Qualify them with what changes or what is produced. A reader should not
  need to open a function to learn its primary effect.
- **Small functions:** every function must perform one operation at one abstraction level. Split a
  function when it mixes discovery, validation, calculation, mutation, effects, restoration, or
  orchestration. Orchestrators may sequence several named operations, but must not implement those
  operations inline. Do not extract trivial forwarding methods merely to reduce line count.
- **Single responsibility:** every class and file must have one primary reason to change and one
  sentence describing its responsibility without "and", "also", or "then". Keep domain policy,
  application sequencing, Unity lifecycle, view mutation, and visual effects separate when they
  change independently. A smaller file is not automatically cohesive; split only at a real change
  axis and keep MonoBehaviours as humble adapters.
- **No long parameter lists:** an ordinary function may have at most two top-level input parameters.
  When it needs three or more related inputs, accept one named tuple whose element names expose the
  meaning at the call site. If the inputs are unrelated, split the responsibility instead of hiding
  them in a tuple. Prefer an existing stable domain value object when it already models the concept;
  do not create a class solely to evade this rule. Fixed Unity messages, overrides, delegate or
  interface implementations, and preserved public/serialized contracts are exempt because their
  signatures are not locally owned; record the exception when it is not obvious.

A top-level method should read as a sequence of domain or presentation operations. Each helper must
remain at one abstraction level, and tuple elements must use domain names rather than positional
labels such as `item1`, `item2`, and `item3`.

Judge the method-length trigger after discounting the vertical formatting that named tuples impose;
a 50-line method that is one guard chain or one sequence of named operations is acceptable, while a
30-line method that mixes discovery and mutation is not. When a method takes many parameters and
scans in stages (a preferred position, then the whole grid), give it one request tuple, hold the
constant search state in a private readonly struct, and name each stage.

After extracting a helper or class, immediately ask whether the original became clearer. If the
result introduces two similarly named concepts, pass-through layers, or navigation without an
independent responsibility, merge it back or rename it around the true job.

## Preserve Unity Serialization

C# reference search alone is never sufficient evidence for deletion or movement.

Before deleting a component, asset, or type:

1. Read its `.meta` and capture the GUID.
2. Search C#, reflection/string-based lookup, `.unity`, `.prefab`, `.asset`, UnityEvent YAML, and all
   metas for its type name and GUID.
3. Distinguish a dead script from a live prefab that merely has a similar name.
4. Delete source and matching meta together only after the reference proof is conclusive.
5. Re-run the old-name, GUID, and missing-script searches after the edit.

For a rename or move, preserve the original GUID. Move or recreate the source and `.meta` as one
operation, update all callers, and verify that exactly one meta retains the GUID. Inspect the Git
diff for an actual rename or a deliberate delete/add pair with identical GUID content.

When removing a serialized field, remove stale YAML only from known owning prefabs or scenes and
include those assets in the manual reopen test. Never bulk-edit serialized assets without proving
ownership and scope. Find the owners by the script's meta GUID across `.prefab`, `.unity`, and
`.asset`, delete the single `fieldName: value` line in the same commit as the field, and confirm the
YAML block stays well formed.

When a new script must be committed before Unity has imported it, author its meta by hand in the
form the repository already uses (many projects keep the minimal `fileFormatVersion: 2` plus `guid:`
pair, which Unity accepts and leaves unchanged), generate a fresh 32-hex GUID, and verify every meta
GUID under `Assets` is unique before staging. Delete a folder's meta together with the folder.

Owned runtime assets such as materials created with `new Material(...)` must be destroyed with
`Destroy` in play mode and `DestroyImmediate` outside it. `Destroy` inside `OnDestroy` logs an error
in edit mode, and that error fails any EditMode test that tears the component down, often with no
assertion message to explain it.

## Treat Runtime Diagnosis as Temporary

Logging is useful when Unity behavior cannot be inferred statically. Add narrow logs around the
suspected transition, ask the user for the relevant Console trace, and use it to distinguish domain
behavior from presentation refresh or lifecycle problems.

Once the issue is understood and verified, remove diagnostic logging before the phase checkpoint.
Do not leave noisy success-path logs or commit secrets, object dumps, or temporary test scripts.

## Verify Each Phase

Use this sequence:

```text
IMPLEMENT
  -> NARROW TEST OR BUILD
  -> BROADER NON-UNITY CHECKS
  -> CALLER / NAME / GUID / YAML SEARCHES
  -> FULL DIFF REVIEW
  -> STOP WITHOUT COMMITTING
  -> USER RUNS UNITY TESTS AND MANUAL MATRIX
  -> USER EXPLICITLY APPROVES
  -> STAGED-DIFF REVIEW
  -> LOCAL PHASE COMMIT
```

Compilation proves API consistency, not Unity behavior. Human validation should cover the actual
seam changed by the phase: initialization, prefab inheritance, drag/cancel/drop, rotation, irregular
geometry, resize, crafting, adjacency, details, loose physics, combat, scene exit/re-entry, Console,
and missing scripts as applicable.

When a test failure conflicts with observed gameplay, investigate fixture setup, lifecycle timing,
runner behavior, and dependency versions. Fix the test only when its assertion mechanism or setup is
invalid; never remove it simply because gameplay appears correct. A failure that arrives without an
assertion message is usually an error log during the test or its teardown (edit-mode `Destroy`,
missing references, coroutine errors); read the component lifecycle before suspecting the assertion.

For a formatting-only phase, prove that no code changed: strip comments and whitespace from the
committed version and the working copy and compare the two streams before committing. Normalize
only files the campaign already justified touching, or one explicit convergence phase over the
remaining legacy-style files, never as a side effect of another phase.

## Compile Before Unity Has Imported Your Files

`dotnet build` on the Unity-generated `.csproj` files compiles only the sources Unity listed at its
last refresh. New scripts, renamed scripts, and files from earlier commits that Unity has not
re-imported are silently absent, and deleted scripts are still listed. Do not edit the generated
project files. Instead:

1. Walk every `.cs` under `Assets`, subtract the files listed by any generated `.csproj`
   (unescape `&amp;` and normalize slashes when comparing), and classify the rest by path
   (`/Editor/` and `/Tests/Editor/` belong to the Editor assembly).
2. Write a temporary `.targets` file outside the project tree with `<Compile Include>` items
   conditioned on `$(MSBuildProjectName)` for each assembly, plus `<Compile Remove>` for files that
   no longer exist, and build with
   `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo -p:CustomAfterMicrosoftCommonTargets=<file>`.
3. Quote the error and warning counts in the ledger and compare warnings to the recorded baseline.

Compilation still proves only API consistency. Unity import, serialization, and behavior remain
separate evidence.

## Drive the Editor Without an MCP Client

An MCP server cannot be attached to a running agent session, and enabling a bridge's auto-start
through EditorPrefs (Windows registry under `HKCU\Software\Unity Technologies\Unity Editor 5.x`,
value name `<Key>_h<hash>` where the hash is djb2-xor, `h = h * 33 ^ c`, over the key) may be a
persistence action the agent is not allowed to perform. Ask the user to click the bridge's start
button or to add the key rather than working around a denial.

When the project uses the Visual Studio Editor package, the running editor already listens on a
UDP messaging channel that needs no setup: port `56000 + (editor pid % 1000) + 2`, datagram =
int32 message type + int32 length + UTF-8 value, types in enum order `None, Ping, Pong, Play, Stop,
Pause, Unpause, Build, Refresh, Info, Error, Warning, Open, Opened, Version, UpdatePackage,
ProjectPath, Tcp, RunStarted, RunFinished, TestStarted, TestFinished, TestListRetrieved,
RetrieveTestList, ExecuteTests, ShowUsage`. Useful facts:

- `Ping` answers `Pong`; `Refresh` runs `AssetDatabase.Refresh`, which recompiles changed scripts,
  regenerates the project files, creates missing metas, and reloads the domain. Detect the reload
  as a gap in `Pong` replies and wait for replies to resume.
- `ExecuteTests` with value `EditMode:<full name>` runs a test, fixture, or namespace (the NUnit
  full-name filter passes descendants). Results are broadcast to every client that sent a message
  in the last 4 seconds, so ping every 2 seconds while listening. Payloads above 8 KB arrive as a
  `Tcp` message whose value is `port:size`; connect to that port and read the serialized message.
- `RunFinished` carries JSON `TestResultAdaptors` with `FullName`, `PassCount`, `FailCount`,
  `TestStatus` (0 passed, 1 skipped, 2 inconclusive, 3 failed) and an often-empty `ResultState`.
  Assertion messages are not included; use the editor log, a bridge with console access, or a
  narrow temporary diagnostic test that writes to a scratch file and is deleted before the checkpoint.
- On Windows a UDP `recv` may raise a connection-reset error after the editor re-binds its socket
  across a domain reload; treat it as a timeout and keep the socket.

Using this channel to compile, refresh, or run tests is verification, not the manual matrix; it does
not replace scene-level checks the plan assigns to a human.

## Commit Without Capturing User Work

After explicit Unity approval:

1. Re-run `git status --short` and the final relevant checks.
2. Stage every intended source, test, prefab, scene, and meta by exact path.
3. Do not use broad staging when unrelated modifications exist.
4. Run `git diff --cached --check`, inspect `git diff --cached --name-only`, and review rename/delete
   summaries.
5. Confirm protected files and local-only plans are absent from the index.
6. Commit one phase with a message describing its single intent. When the contract forbids
   trailers or tool attribution, the message is exactly one line; check with
   `git log -1 --format=%B` and grep the whole history for forbidden words before reporting.
7. Verify status immediately after commit and report remaining user changes.

When phases were developed on a local branch, integrate with a fast-forward merge
(`git merge --ff-only`) so each phase commit stays individually revertible; a squash or merge commit
destroys the checkpoint granularity the campaign paid for.

Do not push unless the user explicitly requests it. If staging fails because one path is wrong,
inspect status before retrying; do not assume partial staging did or did not occur.

## Gate Performance Work on Evidence

Repeated `Instantiate`, `Destroy`, layout rebuilds, or allocations are candidates, not proven
problems. Before adding pools, caches, or invalidation state, request Unity Profiler Timeline and GC
Alloc evidence for a repeatable interaction. Optimize only the measured owner, compare before and
after captures, and retain exact visual and lifecycle behavior. If no meaningful hotspot exists,
record the phase as deferred and make no code change.

## Stop Conditions

Stop and report instead of pushing forward when:

- a phase requires an unapproved behavior change;
- the baseline is red and causality is unknown;
- serialized references cannot be proven safe;
- Unity-only behavior lacks a viable manual gate;
- a new abstraction makes the system harder to navigate;
- the phase grows beyond one reviewable intent;
- user changes overlap the exact lines or assets required;
- profiler evidence does not justify performance complexity.

## Campaign Closeout

At completion or explicit deferral:

- list every phase and local commit;
- compare final checks with the baseline;
- repeat old-name, dead-code, GUID, YAML, and missing-script searches;
- perform a final read-only structural sweep over every production file in scope;
- classify residual issues as required, acceptable with justification, or deferred with evidence;
- record deferred behavior, serialization, lifecycle, and performance risks;
- confirm that no protected user work or local-only plan was committed;
- report honestly which Unity tests and profiler captures were not run.

The campaign succeeds when responsibilities and dependency flow are easier to understand, domain
rules have authoritative owners, Unity details are explicit adapters, behavior is protected, and
each checkpoint can be reviewed or reverted independently.
