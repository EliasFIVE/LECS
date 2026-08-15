# LECS — руководство

**Namespace:** `EliasFive.LECS`  
**Пакет:** `com.eliasfive.lecs`  
**Unity:** 2022.2+  
**Зависимости:** нет (чистый C#, без `UnityEngine` в runtime)

English version: [LECS_GUIDE.md](LECS_GUIDE.md)

---

## 1. Что это

**LECS** — лёгкий runtime для **детерминированной симуляции** с разделением Model / View.

Это **не** классический ECS: нет archetypes, chunk-итерации, Burst и query-builder’ов. Компоненты — `struct`, сущности — `int`, системы — упорядоченный список с методом `Tick()`.

### Зачем

1. **Понятная модель** — низкий порог входа, без reflection в hot path.
2. **Разделение Model / View** — запись через команды и системы, чтение через `IEntitiesWorldDataProvider`.
3. **Команды** — единая точка входа для действий игрока; удобно логировать и тестировать.
4. **Struct-компоненты** — предсказуемая семантика значений.
5. **Dirty flags** — инкрементальные реакции на изменения.
6. **Снапшоты** — клонирование мира для save/load.
7. **Одноразовые компоненты (`Pop`)** — паттерн «запрос на тик» без отдельной очереди сообщений.
8. **Triggers** — типизированные события для View (`Action<T>`).

### Когда подходит / не подходит

| Подходит | Не подходит |
|----------|-------------|
| Небольшая / средняя логика, прототипы | Тысячи сущностей с SIMD/Burst |
| Чёткий Model/View, save/load | Полностью data-oriented pipeline |
| Команды от UI, доменные события | Мультипоточная симуляция |
| Читаемость важнее экстремума perf | Zero-allocation ECS каждый кадр |

---

## 2. Архитектура

```
View / UI ──PushCommand──► CommandsQueue
                                │
Game loop ──Tick──► SystemsRepository (по порядку)
                                │
              ┌─────────────────┼──────────────────┐
              ▼                 ▼                  ▼
      RefreshWorldSystem  CommandsApplySystem  ваши системы
              │                 │                  │
              └────────► EntitiesWorld ◄───────────┘
                              │
                        FireTrigger ──► TriggersRepository ──► View
                              │
                 IEntitiesWorldDataProvider (только чтение)
```

**Принцип (CQRS в одном процессе):**

1. **View** не меняет мир напрямую — только `ICommandsReceiver.PushCommand`.
2. **Systems / appliers** читают и пишут `IEntitiesWorld`.
3. **Triggers** сигналят View о событиях.
4. **View** читает состояние через `IEntitiesWorldDataProvider` и подписки на триггеры.

---

## 3. Основные концепции

### 3.1 EntitiesWorld

Центральное хранилище:

- сущности — `Dictionary<int, Entity>`;
- теги — `Dictionary<Type, HashSet<int>>` (`IEntityTag`);
- **singleton-сущность** — создаётся при `Create()`, глобальное состояние через `*AsSingle`;
- **отложенное удаление** — `RemoveEntity` помечает id, физическое удаление в `Refresh()`;
- **dirty-tracking** — при изменении компонента выставляется dirty, сброс в `Refresh()`.

Создание только через фабрику:

```csharp
IEntitiesWorld world = EntitiesWorldFactory.Create();
// или из снимка:
IEntitiesWorld world = EntitiesWorldFactory.Create(snapshot);
```

```csharp
int unitId = world.AddEntity<UnitEntityTag>();
world.AddNewComponent(unitId, new HealthComponent { value = 100 });

// Глобальное состояние (singleton)
world.AddNewComponentAsSingle(new ScoreComponent { value = 0 });

// Одноразовый запрос на тик
world.AddNewComponentAsSingle(new MoveRequestDynamicComponent { targetId = 42 });
// система обработает через PopComponentDataAsSingle
```

### 3.2 Компоненты

- Данные — **`struct`**.
- Внутри оборачиваются в `Component<T>` (класс + dirty-флаг).
- Один тип компонента на сущность.
- `AddNewComponent` — только если компонента ещё нет (иначе exception).
- `SetComponentData` — обновить + dirty (компонент уже должен существовать).
- `PopComponentData` — прочитать и **удалить**.
- `RemoveComponentData` — удалить без чтения.

**Соглашения имён:**

| Суффикс | Назначение |
|---------|------------|
| `*Component` | Постоянные данные |
| `*DynamicComponent` | Одноразовые запросы / результаты тика (часто через `Pop`) |

### 3.3 Теги (`IEntityTag`)

Пустые marker-типы, без данных:

```csharp
public class UnitEntityTag : IEntityTag { }

int id = world.AddEntity<UnitEntityTag>();
foreach (int unitId in world.GetEntitiesByTag<UnitEntityTag>())
{
    // ...
}

world.AddTag<EnemyEntityTag>(id); // тег к уже существующей сущности
world.RemoveTag<EnemyEntityTag>(id);
```

Особенности:

- `GetEntitiesByTag` / `GetEntityIds` не возвращают id, помеченные к удалению.
- `GetEntityIds` включает singleton-сущность.
- Физическая очистка тег-связей удалённых сущностей — в `Refresh()`.

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

Порядок вызова — **строго порядок массива**.

**Рекомендуемый порядок:**

1. `RefreshWorldSystem` — commit удалений, очистка тегов, сброс dirty.
2. `CommandsApplySystem` — разбор очереди команд.
3. Игровые системы.

> `Refresh` в начале тика применяет изменения **прошлого** тика. Dirty, выставленные в текущем тике, сбросятся только в следующем `Refresh`.

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

Очередь:

```csharp
ICommandsQueue commands = CommandsQueueFactory.Create();

// View / UI
commands.PushCommand(new DealDamageCommand { targetId = 1, amount = 10 });

// Systems получают тот же объект как ICommandsProvider
```

Фабрика appliers — ваша реализация `ICommandAppliersFactory`:

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

`CommandsApplySystem` в цикле `while` забирает **все** команды, включая те, что системы/appliers положили в очередь **в этом же тике**.

> Если для типа команды нет applier — будет `InvalidOperationException` с именем типа команды.

### 3.6 Triggers

```csharp
public class ScoreChangedTrigger : BaseTrigger<ScoreChangedTrigger.Context>
{
    public struct Context
    {
        public int score;
    }
}

// В системе / applier
triggers.FireTrigger<ScoreChangedTrigger, ScoreChangedTrigger.Context>(
    new ScoreChangedTrigger.Context { score = 10 });

// Во View
ITriggersProvider provider = triggers; // тот же ITriggersRepository
provider.GetTrigger<ScoreChangedTrigger, ScoreChangedTrigger.Context>().onFire += OnScoreChanged;
```

Триггеры создаются lazy (`new T()`), живут в `TriggersRepository` весь срок жизни мира. Не забудьте отписаться при уничтожении View.

### 3.7 Снапшоты

```csharp
EntitiesWorldSnapshot snapshot = world.GetSnapshot();
IEntitiesWorld restored = EntitiesWorldFactory.Create(snapshot);
```

**Сохраняется:** сущности, компоненты, теги, `singletonEntityId`, `lastUsedId`, `releasedIds`.  
**Не сохраняется:** очередь команд, подписки на триггеры, dirty-флаги.

Снапшот хранит `Type` и экземпляры `IComponent`. Для JSON/бинарной сериализации нужна поддержка полиморфизма и типов; поле `Component<T>._internalData` публичное специально под сериализаторы, пишущие только public instance fields. Снимайте снимок **между тиками**, не в середине мутаций.

---

## 4. Жизненный цикл тика

```mermaid
sequenceDiagram
    participant Loop as Game loop
    participant SR as SystemsRepository
    participant EW as EntitiesWorld

    Loop->>SR: Tick()
    SR->>EW: Refresh() — удаления, теги, dirty
    SR->>SR: CommandsApplySystem — все команды в очереди
    SR->>SR: Game systems
    Note over EW: новые dirty сбросятся<br/>в Refresh следующего тика
```

Типичный игровой цикл:

```csharp
// каждый кадр / фиксированный шаг
systems.Tick();
```

---

## 5. Bootstrap (минимальный пример)

```csharp
using System;
using System.Collections.Generic;
using EliasFive.LECS;

// --- Данные ---
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

// --- View: подписка ---
triggers.GetTrigger<ScoreChangedTrigger, ScoreChangedTrigger.Context>().onFire += ctx =>
{
    // обновить UI
};

// --- View: действие игрока ---
commands.PushCommand(new AddScoreCommand { delta = 5 });

// --- Симуляция ---
systems.Tick();

// --- View: чтение ---
int score = world.GetComponentAsSingle<ScoreComponent>().value;
```

Для View лучше хранить ссылку только на `IEntitiesWorldDataProvider`, а не на полный `IEntitiesWorld`.

---

## 6. Как расширять (чеклист)

1. **Данные:** `struct MyFeatureComponent` (+ при необходимости `MyEntityTag`).
2. **Инициализация:** `AddEntity` / `AddNewComponent` в bootstrap или в applier.
3. **Команда (от UI):** `ICommand` + `BaseCommandApplier<T>` + запись в `ICommandAppliersFactory`.
4. **Логика тика:** `ISystem` + место в массиве `SystemsRepositoryFactory.Create` (**порядок важен**).
5. **Событие для View:** `BaseTrigger<TContext>` + `FireTrigger` из системы/applier.
6. **View:** подписка на триггер + чтение через `IEntitiesWorldDataProvider`; в контекст триггера — только то, чего нет в стабильных компонентах (или id для последующего чтения).

### Порядок систем — типичные правила

| Правило | Зачем |
|---------|--------|
| `RefreshWorldSystem` всегда первый | Консистентный мир до чтения тегов и данных |
| `CommandsApplySystem` сразу после | Applier видит уже «закоммиченный» мир |
| Обработка `*Request*` до реакций на результат | Сначала `Pop`, потом триггер / побочные эффекты |
| Команды, поставленные в том же тике | Обработаются в том же `CommandsApplySystem` (`while`) |

---

## 7. API — справочник

Публичные точки входа — **фабрики** и интерфейсы. Внутренние классы (`EntitiesWorld`, `CommandsQueue`, …) создавать напрямую не нужно.

### Фабрики

| Фабрика | Результат |
|---------|-----------|
| `EntitiesWorldFactory.Create()` / `Create(snapshot)` | `IEntitiesWorld` |
| `CommandsQueueFactory.Create()` | `ICommandsQueue` (= receiver + provider) |
| `TriggersRepositoryFactory.Create()` | `ITriggersRepository` (= invoker + provider) |
| `SystemsRepositoryFactory.Create(ISystem[])` | `ISystemsRepository` |

### `IEntitiesWorld` (запись + чтение)

| Метод | Назначение |
|-------|------------|
| `AddEntity()` / `AddEntity<T>()` | Новая сущность (+ тег) |
| `RemoveEntity(id)` | Отложенное удаление |
| `AddTag<T>(id)` | Добавить тег |
| `RemoveTag<T>(id)` | Снять тег |
| `AddNewComponent` / `AddNewComponentAsSingle` | Добавить компонент |
| `SetComponentData` / `SetComponentDataAsSingle` | Обновить + dirty |
| `PopComponentData` / `PopComponentDataAsSingle` | Прочитать и удалить |
| `RemoveComponentData` / `RemoveComponentDataAsSingle` | Удалить без чтения |
| `IsDirtyComponentData` / `IsDirtyComponentDataAsSingle` | Dirty компонента |
| `Refresh()` | Commit (обычно только из `RefreshWorldSystem`) |

### `IEntitiesWorldDataProvider` (чтение)

| Метод | Назначение |
|-------|------------|
| `GetComponent` / `GetComponentAsSingle` | Чтение |
| `HasComponent` / `HasComponentAsSingle` | Проверка компонента |
| `HasTag` | Проверка тега |
| `GetEntitiesByTag` | Выборка id по тегу |
| `GetEntityIds` | Все живые id (включая singleton) |
| `GetSnapshot()` | Save / клон |

### Commands / Triggers / Systems

| Роль | Тип |
|------|-----|
| Запись команд | `ICommandsReceiver.PushCommand<T>` |
| Чтение очереди | `ICommandsProvider` (`hasCommands`, `PopCommand`) |
| Applier | `BaseCommandApplier<T>` / `ICommandAppliersFactory` |
| Встроенные системы | `RefreshWorldSystem`, `CommandsApplySystem` |
| Fire | `ITriggersInvoker.FireTrigger<T, TC>` |
| Подписка | `ITriggersProvider.GetTrigger<T, TC>().onFire` |

---

## 8. Антипаттерны

1. **Мутировать мир из View** — только команды.
2. **Читать `Pop`-компоненты из View** — они для систем; View читает стабильные компоненты.
3. **Подписываться на триггер без отписки** при пересоздании UI.
4. **Тяжёлая логика в applier** — applier тонкий; цепочки — в systems.
5. **Полагаться на dirty во View без тика** — dirty живёт до следующего `Refresh`.
6. **Менять порядок систем «на глаз»** — фиксировать в factory и тестах.
7. **Снимать snapshot mid-tick** — между тиками, после `Refresh` или до начала следующего.
8. **Создавать внутренние типы напрямую** — используйте `*Factory`.

---

## 9. Ограничения (осознанно)

- Нет multi-tag / component queries — теги, `GetEntityIds` и прямой доступ по id.
- Нет пулов объектов (только reuse id после `Refresh`).
- Нет многопоточности — один поток симуляции.
- Нет встроенной сериализации — только структура `EntitiesWorldSnapshot`.
- Разделение Model/View — **конвенция**: View должен получать `IEntitiesWorldDataProvider` + `ICommandsReceiver` + `ITriggersProvider`, а не полный write-доступ.

---

## 10. Глоссарий

| Термин | Значение в LECS |
|--------|-----------------|
| Singleton entity | Первая сущность мира; API `*AsSingle` |
| Dynamic component | Одноразовый сигнал/запрос, часто через `Pop` |
| Tag | Marker-тип для выборки сущностей |
| Tick | Один проход `ISystemsRepository.Tick()` |
| Refresh | Commit удалений + очистка тегов + сброс dirty |
| Command | Запрос на изменение модели от View |
| Trigger | Событие модели → View |
| Dirty | Флаг «компонент менялся с прошлого Refresh» |
