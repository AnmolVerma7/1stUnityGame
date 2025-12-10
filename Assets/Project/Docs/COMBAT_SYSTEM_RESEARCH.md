# ⚔️ AAA Combat & Movement Research Brief

**Objective**: Design a system capable of "Arkham-style" Free-Flow Combat and "Spider-Man-style" Fluid Movement.

## 1. The Core Architecture: Hierarchical State Machine (HSM)

To achieve "smoothness" without spaghetti code, we **cannot** use a simple `switch` statement or a basic boolean flag system. We need a **Hierarchical State Machine**.

### Why HSM?

- **Regular FSM**: You have states like `Idle`, `Walk`, `Run`, `Jump`, `Attack`.
  - _Problem_: If you want to attack while jumping, you need a `JumpAttack` state. If you want to attack while running, you need `RunAttack`. It explodes exponentially.
- **Hierarchical**: You have a **SuperState** `Grounded`. Inside it, you have `Idle`, `Walk`, `Run`.
  - _Benefit_: The `Grounded` state handles things like "Gravity" and "Getting Hit". The sub-states only care about their specific animation.
  - _Benefit_: You can have a parallel `Combat` state machine that overrides the `Movement` one when necessary.

### Recommended Structure for Antigravity

```mermaid
graph TD
    Root[Player Controller] --> Movement[Movement State Machine]
    Root --> Combat[Combat State Machine]

    Movement --> Grounded
    Movement --> Airborne
    Movement --> WallInteraction

    Grounded --> Idle
    Grounded --> Locomotion

    Airborne --> Fall
    Airborne --> Swing
    Airborne --> Gliding

    Combat --> Passive[Passive (Targeting)]
    Combat --> Active[Active (Striking)]
    Active --> Attack
    Active --> Counter
    Active --> Dodge
```

## 2. "Free-Flow" Combat (Arkham Style)

The secret to Batman's combat isn't just animations; it's **Target Selection** and **Input Buffering**.

### Key Components

1.  **Input Buffer**:
    - If the player presses "Attack" while currently punching, the system _remembers_ it.
    - As soon as the current punch finishes, the next one triggers instantly.
    - _Result_: Combat feels responsive, never "dropped".
2.  **Soft Targeting (Magnetism)**:
    - When you press Attack + Stick Direction, the game finds the "Best Target" in that cone.
    - The character **slides** (root motion or code-driven) towards that enemy during the wind-up of the animation.
    - _Result_: You feel like you never miss.
3.  **Animation Cancelling**:
    - "Dodge" usually has higher priority than "Attack".
    - If you press Dodge mid-punch, the punch is cancelled immediately to play the dodge.

## 3. Fluid Movement (Spider-Man Style)

Spider-Man's movement is about **Momentum Preservation**.

### Key Concepts

1.  **Physics-Driven Swing**:
    - Don't just play an animation. Actually attach a joint to a building and let physics pull you.
    - _Crucial_: When you let go, **keep that velocity**.
2.  **Contextual Actions**:
    - One button (e.g., `Right Trigger`) does different things based on context:
      - **In Air + Near Wall**: Wall Run.
      - **In Air + Clear Space**: Swing.
      - **On Ground**: Sprint / Parkour.
3.  **Motion Matching (The "Next Level")**:
    - _Traditional_: Blend Trees (Walk -> Run).
    - _AAA_: Motion Matching. The system looks at your velocity and trajectory and picks the _perfect_ frame from 1000s of animations.
    - _Indie Reality_: Motion Matching is expensive (data-wise). For us, **High-Quality Blend Trees** with **Inverse Kinematics (IK)** for foot placement is the sweet spot.

## 4. Implementation Roadmap

### Phase 1: The Foundation (HSM)

- Create `BaseState` class.
- Refactor `PlayerController` to use `StateMachine`.
- Implement `Grounded` and `Airborne` states.

### Phase 2: The Combat Layer

- Create `CombatManager`.
- Implement `InputBuffer`.
- Implement `TargetScanner` (SphereCast to find enemies).

### Phase 3: The Juice

- Add **HitStop** (freeze frame on impact).
- Add **Camera Shake**.
- Add **Cinemachine Impulse** for heavy hits.

## 5. Resources & Assets

- **Animancer**: A better API for playing animations than Unity's default Animator. Highly recommended for code-driven combat.
- **DOTween**: Essential for procedural animation (sliding to targets).

## 6. Realistic Timeline & Complexity 🗓️

Building a "Spider-Man + Arkham" system is a massive undertaking. Here is a realistic breakdown for a solo dev + AI pair:

| Phase                | Feature Set                                                    | Estimated Time |
| :------------------- | :------------------------------------------------------------- | :------------- |
| **1. The Skeleton**  | State Machine (HSM), Input Handling, Basic Movement (Walk/Run) | **2-3 Days**   |
| **2. The Traversal** | Wall Run, Mantle, Slide, Vault (Physics & Detection)           | **1-2 Weeks**  |
| **3. The Combat**    | Basic Attack, Dodge, Target Detection, Hit Reactions           | **1-2 Weeks**  |
| **4. The Polish**    | VFX, Sound, Camera Shake, Smooth Animations                    | **Ongoing**    |

**Verdict**: We can get a _playable prototype_ in **1 week**. A _polished feel_ will take **1 month**.

## 7. Controller Layout Strategy 🎮

"How do we fit all this on a gamepad?" -> **Context is King.**

We don't need 20 buttons. We need buttons that do different things based on **State**.

| Button            | Grounded                      | In Air              | Near Wall        | Combat            |
| :---------------- | :---------------------------- | :------------------ | :--------------- | :---------------- |
| **South (A/X)**   | Jump                          | Double Jump / Swing | Wall Run / Climb | Dodge / Evade     |
| **East (B/O)**    | Crouch / Slide                | Air Dash            | Drop Ledge       | Counter Attack    |
| **West (X/Sq)**   | Attack                        | Air Attack          | -                | Attack            |
| **North (Y/Tri)** | Interact                      | Zip to Point        | -                | Special Takedown  |
| **L1 / LB**       | **Ability Wheel** (Slow Time) | **Ability Wheel**   | -                | **Ability Wheel** |
| **R2 / RT**       | Sprint                        | Swing / Dive        | Run Up Wall      | Gadget Use        |

**The Rewind Ability**:

- Perfect for the **Ability Wheel** (L1).
- Hold L1 -> Time slows -> Select "Rewind" with Stick -> Release to trigger.
- Or simply **Hold L1 + R1** for "Ultimate" rewind.

**Advice**: Start **SLOW**.

1.  Build the **State Machine** first. It's the brain.
2.  Add **Sprint/Slide**.
3.  Add **Wall Run**.
4.  _Then_ add Combat.
    If we rush, we get spaghetti. If we build the HSM right, adding a new move is just adding a new script.
