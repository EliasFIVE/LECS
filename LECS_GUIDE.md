# LECS Guide

**Namespace:** `EliasFive.LECS`  
**Package:** `com.eliasfive.lecs`  
**Unity:** 2022.2+  
**Dependencies:** none (plain C#, no `UnityEngine` in runtime)

Russian version: [LECS_GUIDE_RU.md](LECS_GUIDE_RU.md)

---

## 1. What it is

**LECS** is a lightweight runtime for **deterministic simulation** with a Model / View split.

This is **not** a classic ECS: no archetypes, chunk iteration, Burst, or query builders. Components are `struct`s, entities are `int`s, systems are an ordered list with `Tick()`.

### Why

1. **Simple mental model** — low barrier for Unity developers, no reflection on the hot path.
2. **Model / View separation** — writes go through commands and systems; reads go through `IEntitiesWorldDataProvider`.
3. **Commands** — a single entry point for player actions; easy to log and test.
4. **Struct components** — predictable value semantics.
5. **Dirty flags** — lightweight incremental reactions to changes.
6. **Snapshots** — full world cloning for save/load.
7. **One-shot components (`Pop`)** — a “request for this tick” pattern without a separate message queue.
8. **Triggers** — typed events for the View (`Action<T>`).

### Good fit / poor fit

| Good fit | Poor fit |
|----------|----------|
| Small / medium game logic, prototypes | Thousands of entities with SIMD/Burst |
| Clear Model/View, save/load | Fully data-oriented pipelines |
| UI commands, domain events | Multithreaded simulation |
| Readability over extreme perf | Zero-allocation ECS every frame |

---

## 2. Architecture

```
View / UI ──PushCommand──► CommandsQueue
                                │
Game loop ──Tick──► SystemsRepository (in order)
                                │
              ┌─────────────────┼──────────────────┐
              ▼                 ▼                  ▼
      RefreshWorldSystem  CommandsApplySystem  your systems
              │                 │                  │
              └────────► EntitiesWorld ◄───────────┘
                              │
                        FireTrigger ──► TriggersRepository ──► View
                              │
                 IEntitiesWorldDataProvider (read-only)
```

**Principle (in-process CQRS):**

1. **View** never mutates the world directly — only `ICommandsReceiver.PushCommand`.
2. **Systems / appliers** read and write `IEntitiesWorld`.
3. **Triggers** notify the View about events.
4. **View** reads state via `IEntitiesWorldDataProvider` and trigger subscriptions.

---

## 3. Core concepts

### 3.1 EntitiesWorld

Central store:

- entities — `Dictionary<int, Entity>`;
- tags — `Dictionary<Type, HashSet<int>>` (`IEntityTag`);
- **singleton entity** — created in `Create()`, global state via `*AsSingle`;
- **deferred removal** — `RemoveEntity` marks an id; physical delete happens in `Refresh()`;
- **dirty tracking** — component changes set dirty; cleared in `Refresh()`.

Create only through the factory:

```csharp
IEntitiesWorld world = EntitiesWorldFactory.Create();
// or from a snapshot:
IEntitiesWorld world = EntitiesWorldFactory.Create(snapshot);
```

```csharp
int unitId = world.AddEntity<UnitEntityTag>();
world.AddNewComponent(unitId, new HealthComponent { value = 100 });

// Global state (singleton)
world.AddNewComponentAsSingle(new ScoreComponent { value = 0 });

// One-shot request for this tick
world.AddNewComponentAsSingle(new MoveRequestDynamicComponent { targetId = 42 });
// a system will handle it via PopComponentDataAsSingle
```

### 3.2 Components

- Data is a **`struct`**.
- Internally wrapped in `Component<T>` (class + dirty flag).
- One component type per entity.
- `AddNewComponent` — only if the component does not exist yet (otherwise throws).
- `SetComponentData` — update + dirty (component must already exist).
- `PopComponentData` — read and **remove**.
- `RemoveComponentData` — remove without reading.

**Naming conventions:**

| Suffix | Purpose |
|--------|---------|
| `*Component` | Persistent data |
| `*DynamicComponent` | One-shot requests / tick results (often via `Pop`) |

### 3.3 Tags (`IEntityTag`)

Empty marker types, no data:

```csharp
public class UnitEntityTag : IEntityTag { }

int id = world.AddEntity<UnitEntityTag>();
foreach (int unitId in world.GetEntitiesByTag<UnitEntityTag>())
{
    // ...
}

world.AddTag<EnemyEntityTag>(id); // tag an existing entity
world.RemoveTag<EnemyEntityTag>(id);
```

Notes:

- `GetEntitiesByTag` / `GetEntityIds` do not return ids marked for removal.
- `GetEntityIds` includes the singleton entity.
- Tag links for deleted entities are cleaned up in `Refresh()`.

### 3.4 Systems

```csharp
public interface ISystem
{
    void Tick();
}
```

```csharp
ISystemsRepository systems = SystemsRepositoryFactory.Create(new ISystem[]
{
    new RefreshWorldSystem(world),
    new CommandsApplySystem(commands, appliersFactory),
    new MyGameSystem(world, triggers),
});
```

Call order is **exactly the array order**.

**Recommended order:**

1. `RefreshWorldSystem` — commit removals, clean tags, clear dirty.
2. `CommandsApplySystem` — drain the command queue.
3. Game systems.

> `Refresh` at the start of a tick applies changes from the **previous** tick. Dirty flags set in the current tick are cleared only on the next `Refresh`.

### 3.5 Commands

```csharp
public class DealDamageCommand : ICommand
{
    public int targetId;
    public int amount;
}

public class DealDamageCommandApplier : BaseCommandApplier<DealDamageCommand>
{
    readonly IEntitiesWorld _world;

    public DealDamageCommandApplier(IEntitiesWorld world)
    {
        _world = world;
    }

    protected override void Apply(DealDamageCommand command)
    {
        var health = _world.GetComponent<HealthComponent>(command.targetId);
        health.value -= command.amount;
        _world.SetComponentData(command.targetId, health);
    }
}
```

Queue:

```csharp
ICommandsQueue commands = CommandsQueueFactory.Create();

// View / UI
commands.PushCommand(new DealDamageCommand { targetId = 1, amount = 10 });

// Systems receive the same object as ICommandsProvider
```

Appliers factory — your `ICommandAppliersFactory` implementation:

```csharp
public class CommandAppliersFactory : ICommandAppliersFactory
{
    readonly IReadOnlyDictionary<Type, ICommandApplier> _map;

    public CommandAppliersFactory(IEntitiesWorld world)
    {
        _map = new Dictionary<Type, ICommandApplier>
        {
            { typeof(DealDamageCommand), new DealDamageCommandApplier(world) },
        };
    }

    public IReadOnlyDictionary<Type, ICommandApplier> Get() => _map;
}
```

`CommandsApplySystem` loops with `while` and applies **all** commands, including ones enqueued by systems/appliers **in the same tick**.

> If no applier is registered for a command type, it throws `InvalidOperationException` with the command type name.

### 3.6 Triggers

```csharp
public class ScoreChangedTrigger : BaseTrigger<ScoreChangedTrigger.Context>
{
    public struct Context
    {
        public int score;
    }
}

// In a system / applier
triggers.FireTrigger<ScoreChangedTrigger, ScoreChangedTrigger.Context>(
    new ScoreChangedTrigger.Context { score = 10 });

// In the View
ITriggersProvider provider = triggers; // same ITriggersRepository
provider.GetTrigger<ScoreChangedTrigger, ScoreChangedTrigger.Context>().onFire += OnScoreChanged;
```

Triggers are created lazily (`new T()`) and live in `TriggersRepository` for the world lifetime. Unsubscribe when the View is destroyed.

### 3.7 Snapshots

```csharp
EntitiesWorldSnapshot snapshot = world.GetSnapshot();
IEntitiesWorld restored = EntitiesWorldFactory.Create(snapshot);
```

**Saved:** entities, components, tags, `singletonEntityId`, `lastUsedId`, `releasedIds`.  
**Not saved:** command queue, trigger subscriptions, dirty flags.

The snapshot stores `Type` and live `IComponent` instances. JSON/binary serialization needs polymorphism and type handling; `Component<T>._internalData` is public for serializers that only write public instance fields. Take snapshots **between ticks**, not mid-mutation.

---

## 4. Tick lifecycle

```mermaid
sequenceDiagram
    participant Loop as Game loop
    participant SR as SystemsRepository
    participant EW as EntitiesWorld

    Loop->>SR: Tick()
    SR->>EW: Refresh() — removals, tags, dirty
    SR->>SR: CommandsApplySystem — all queued commands
    SR->>SR: Game systems
    Note over EW: new dirty flags clear<br/>on next tick Refresh
```

Typical game loop:

```csharp
// every frame / fixed step
systems.Tick();
```

---

## 5. Bootstrap (minimal example)

```csharp
using System;
using System.Collections.Generic;
using EliasFive.LECS;

// --- Data ---
public struct ScoreComponent
{
    public int value;
}

public class AddScoreCommand : ICommand
{
    public int delta;
}

public class ScoreChangedTrigger : BaseTrigger<ScoreChangedTrigger.Context>
{
    public struct Context { public int score; }
}

// --- Applier ---
public class AddScoreCommandApplier : BaseCommandApplier<AddScoreCommand>
{
    readonly IEntitiesWorld _world;
    readonly ITriggersInvoker _triggers;

    public AddScoreCommandApplier(IEntitiesWorld world, ITriggersInvoker triggers)
    {
        _world = world;
        _triggers = triggers;
    }

    protected override void Apply(AddScoreCommand command)
    {
        var score = _world.GetComponentAsSingle<ScoreComponent>();
        score.value += command.delta;
        _world.SetComponentDataAsSingle(score);

        _triggers.FireTrigger<ScoreChangedTrigger, ScoreChangedTrigger.Context>(
            new ScoreChangedTrigger.Context { score = score.value });
    }
}

public class AppliersFactory : ICommandAppliersFactory
{
    readonly IReadOnlyDictionary<Type, ICommandApplier> _map;

    public AppliersFactory(IEntitiesWorld world, ITriggersInvoker triggers)
    {
        _map = new Dictionary<Type, ICommandApplier>
        {
            { typeof(AddScoreCommand), new AddScoreCommandApplier(world, triggers) },
        };
    }

    public IReadOnlyDictionary<Type, ICommandApplier> Get() => _map;
}

// --- Setup ---
IEntitiesWorld world = EntitiesWorldFactory.Create();
ICommandsQueue commands = CommandsQueueFactory.Create();
ITriggersRepository triggers = TriggersRepositoryFactory.Create();

world.AddNewComponentAsSingle(new ScoreComponent { value = 0 });

var appliers = new AppliersFactory(world, triggers);
ISystemsRepository systems = SystemsRepositoryFactory.Create(new ISystem[]
{
    new RefreshWorldSystem(world),
    new CommandsApplySystem(commands, appliers),
});

// --- View: subscribe ---
triggers.GetTrigger<ScoreChangedTrigger, ScoreChangedTrigger.Context>().onFire += ctx =>
{
    // update UI
};

// --- View: player action ---
commands.PushCommand(new AddScoreCommand { delta = 5 });

// --- Simulation ---
systems.Tick();

// --- View: read ---
int score = world.GetComponentAsSingle<ScoreComponent>().value;
```

Prefer giving the View only `IEntitiesWorldDataProvider`, not the full `IEntitiesWorld`.

---

## 6. Extending (checklist)

1. **Data:** `struct MyFeatureComponent` (+ `MyEntityTag` if needed).
2. **Init:** `AddEntity` / `AddNewComponent` in bootstrap or an applier.
3. **Command (from UI):** `ICommand` + `BaseCommandApplier<T>` + register in `ICommandAppliersFactory`.
4. **Tick logic:** `ISystem` + a slot in `SystemsRepositoryFactory.Create` (**order matters**).
5. **View event:** `BaseTrigger<TContext>` + `FireTrigger` from a system/applier.
6. **View:** subscribe to the trigger + read via `IEntitiesWorldDataProvider`; put in the trigger context only what is not already in stable components (or an id for a later read).

### System order — common rules

| Rule | Why |
|------|-----|
| `RefreshWorldSystem` always first | Consistent world before reading tags/data |
| `CommandsApplySystem` right after | Appliers see a committed world |
| Handle `*Request*` before reacting to results | `Pop` first, then triggers / side effects |
| Commands enqueued in the same tick | Applied in the same `CommandsApplySystem` (`while`) |

---

## 7. API reference

Public entry points are **factories** and interfaces. Do not construct internal types (`EntitiesWorld`, `CommandsQueue`, …) directly.

### Factories

| Factory | Result |
|---------|--------|
| `EntitiesWorldFactory.Create()` / `Create(snapshot)` | `IEntitiesWorld` |
| `CommandsQueueFactory.Create()` | `ICommandsQueue` (= receiver + provider) |
| `TriggersRepositoryFactory.Create()` | `ITriggersRepository` (= invoker + provider) |
| `SystemsRepositoryFactory.Create(ISystem[])` | `ISystemsRepository` |

### `IEntitiesWorld` (write + read)

| Method | Purpose |
|--------|---------|
| `AddEntity()` / `AddEntity<T>()` | New entity (+ tag) |
| `RemoveEntity(id)` | Deferred removal |
| `AddTag<T>(id)` | Add tag |
| `RemoveTag<T>(id)` | Remove tag |
| `AddNewComponent` / `AddNewComponentAsSingle` | Add component |
| `SetComponentData` / `SetComponentDataAsSingle` | Update + dirty |
| `PopComponentData` / `PopComponentDataAsSingle` | Read and remove |
| `RemoveComponentData` / `RemoveComponentDataAsSingle` | Remove without reading |
| `IsDirtyComponentData` / `IsDirtyComponentDataAsSingle` | Component dirty flag |
| `Refresh()` | Commit (usually only from `RefreshWorldSystem`) |

### `IEntitiesWorldDataProvider` (read)

| Method | Purpose |
|--------|---------|
| `GetComponent` / `GetComponentAsSingle` | Read |
| `HasComponent` / `HasComponentAsSingle` | Component check |
| `HasTag` | Tag check |
| `GetEntitiesByTag` | Ids by tag |
| `GetEntityIds` | All living ids (including singleton) |
| `GetSnapshot()` | Save / clone |

### Commands / Triggers / Systems

| Role | Type |
|------|------|
| Push commands | `ICommandsReceiver.PushCommand<T>` |
| Read queue | `ICommandsProvider` (`hasCommands`, `PopCommand`) |
| Applier | `BaseCommandApplier<T>` / `ICommandAppliersFactory` |
| Built-in systems | `RefreshWorldSystem`, `CommandsApplySystem` |
| Fire | `ITriggersInvoker.FireTrigger<T, TC>` |
| Subscribe | `ITriggersProvider.GetTrigger<T, TC>().onFire` |

---

## 8. Antipatterns

1. **Mutating the world from the View** — commands only.
2. **Reading `Pop` components from the View** — they belong to systems; View reads stable components.
3. **Subscribing without unsubscribing** when recreating UI.
4. **Heavy logic in appliers** — keep appliers thin; chains belong in systems.
5. **Relying on dirty in the View without a tick** — dirty lasts until the next `Refresh`.
6. **Guessing system order** — fix it in the factory and tests.
7. **Taking a snapshot mid-tick** — between ticks, after `Refresh` or before the next one starts.
8. **Constructing internal types directly** — use `*Factory`.

---

## 9. Intentional limitations

- No multi-tag / component queries — tags, `GetEntityIds`, and direct id access.
- No object pools (only id reuse after `Refresh`).
- No multithreading — single simulation thread.
- No built-in serialization — only the `EntitiesWorldSnapshot` structure.
- Model/View split is a **convention**: give View `IEntitiesWorldDataProvider` + `ICommandsReceiver` + `ITriggersProvider`, not full write access.

---

## 10. Glossary

| Term | Meaning in LECS |
|------|-----------------|
| Singleton entity | First world entity; `*AsSingle` API |
| Dynamic component | One-shot signal/request, often via `Pop` |
| Tag | Marker type for entity selection |
| Tick | One `ISystemsRepository.Tick()` pass |
| Refresh | Commit removals + clean tags + clear dirty |
| Command | Model change request from the View |
| Trigger | Model → View event |
| Dirty | Flag: component changed since last Refresh |
