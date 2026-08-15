LECS
Lightweight Entities Components Systems

LECS is a lightweight runtime designed for deterministic game simulation with a strict Model/View separation. Unlike traditional ECS frameworks, LECS prioritizes a simple mental model, readable code, and predictable data flow over extreme SIMD performance.

🚀 Key Features
Model / View Split: UI/View never mutates the world directly; it sends Commands.
Command-Driven: A single entry point for player actions, making your game easy to log and test.
Deterministic: Predictable execution flow for consistent simulation.
Snapshots: Built-in support for full world cloning (save/load).
Triggers: Typed events to notify the View of changes in the Model.
Zero Runtime Dependencies: Plain C# (no UnityEngine required in the core).
🎯 Who is it for?
Good fit: Small/medium game logic, UI-heavy systems, prototypes, and projects requiring robust save/load mechanics.
Not a fit: High-performance pipelines requiring thousands of entities with SIMD/Burst optimization.
