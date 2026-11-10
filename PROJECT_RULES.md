# Unity Roguelite Architecture & Code Standards

## Tech Stack & Engine Rules
- Engine: Unity (C#)
- Input: Unity New Input System (UnityEngine.InputSystem)
- Physics: 2D Physics (Rigidbody2D, Collider2D, ContactFilter2D)
- Target: WebGL-ready (Zero GC pressure inside `Update()` / `FixedUpdate()`)

## Architectural Constraints
1. **Decoupling:** No direct references across disparate systems. Use C# `Action` events or ScriptableObject channels for communication (e.g., Player death, health changes, score).
2. **Object Management:** Never call `Instantiate()` or `Destroy()` during runtime combat. All bullets, particle instances, and enemies must use a generic `ObjectPool<T>`.
3. **Component Responsibilities (Single Responsibility Principle):**
   - `PlayerController`: Input reading and physics velocity only.
   - `Health`: Damage intake, invulnerability frames, and death events only.
   - `Weapon`: Firing cadence, projectile spawn trigger from pool.
   - `EnemyAI`: Driven strictly by a finite state machine (`IEnemyState`).
4. **Serialization:** Use `[SerializeField] private` for Inspector variables. Never expose raw public fields unless they are read-only properties (`public float Speed => speed;`).
5. **No Hallucinated Unity APIs:** Stick to standard LTS APIs. Prefer explicit non-allocating physics queries (e.g., `Physics2D.OverlapCircleNonAlloc`).