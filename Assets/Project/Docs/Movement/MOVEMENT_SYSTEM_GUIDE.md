# 🏃 Movement System Guide

> > **Last Updated**: December 10, 2024 - Mantle System & Handler Pattern Documentation

Welcome to the **Antigravity Movement System**! This guide explains our modular, scalable architecture for player movement.

---

## 📚 Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [The Three Pillars](#the-three-pillars)
3. [How It Works](#how-it-works)
4. [Handler Pattern (Composition)](#-handler-pattern-composition)
5. [Mantle System (Parkour)](#-mantle-system-parkour)
6. [Adding New Abilities](#adding-new-abilities)
7. [Controls](#controls)
8. [Configuration](#configuration)
9. [Design Patterns](#design-patterns)
10. [Change Log](#change-log)

---

## 🏗️ Architecture Overview

We follow the **Strategy Pattern** - movement modes (jump, wallrun, combat) are separate modules that can be swapped at runtime.

```
┌──────────────────────────┐
│    PlayerKCCAdapter      │  ← Physics Bridge
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│   PlayerController       │  ← Manager (No Math!)
│   - Routes Input         │
│   - Coordinates Logic    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ PlayerMovementSystem     │  ← Traffic Controller
└────────────┬─────────────┘
             │
     ┌───────┼───────┐
     ▼       ▼       ▼
┌─────────┐ ┌─────────┐ ┌─────────┐
│Default  │ │NoClip   │ │WallRun  │  ← Logic Modules
│Movement │ │Movement │ │Movement │
└─────────┘ └─────────┘ └─────────┘
```

### Why This Architecture?

**Before** (Monolithic):

- 523 lines in PlayerController
- Mixed Input, Physics, and State logic
- High complexity

**After** (Logic-Free):

- ~140 lines effective logic
- **Zero** physics math in Controller
- `NoClip` logic extracted to module
- `Camera` logic extracted to static processor
- `KCC` boilerplate extracted to Adapter

---

## 🧱 The Three Pillars

### 1. Input Handling - `PlayerInputHandler.cs`

**Role**: The "Ears" - listens to Unity Input System

**Key Feature**: Separates Mouse Delta (pixels) from Gamepad Rate (analog) for consistent camera speed

**Public API**:

```csharp
Vector2 MoveInput      // WASD / Stick
Vector2 LookDelta      // Mouse pixels (use for camera)
Vector2 LookRate       // Gamepad -1 to 1 (use for camera)
bool JumpDown          // One-frame trigger
bool JumpHeld          // Held state
bool CrouchHeld        // Held state
```

**Execution Order**: `-100` (runs before everything else)

---

### 2. Configuration - `PlayerMovementConfig.cs`

**Role**: The "DNA" - ScriptableObject with tuning values

**Location**: `Assets/Project/ScriptableObjects/`

**Settings**:

- Ground Movement: `MaxStableMoveSpeed`, `StableMovementSharpness`
- Air Movement: `MaxAirMoveSpeed`, `AirAccelerationSpeed`, `Drag`
- Jumping: `JumpSpeed`, `AllowDoubleJump`, `AllowWallJump`
- **Jump Buffering**: `JumpPreGroundingGraceTime` (0.15s)
- **Coyote Time**: `JumpPostGroundingGraceTime` (0.1s)

---

### 3. Movement System - The Core

#### `IMovementModule` Interface

Contract that all movement modes implement:

```csharp
public interface IMovementModule
{
    void UpdatePhysics(ref Vector3 velocity, float deltaTime);
    void AfterUpdate(float deltaTime);
    void OnActivated();
    void OnDeactivated();
}
```

#### `PlayerMovementSystem` Manager

Manages which module is active:

```csharp
// Register modules
_movementSystem.RegisterModule(new DefaultMovement(...), isDefault: true);
_movementSystem.RegisterModule(new WallRunMovement(...));

// Switch modules
_movementSystem.ActivateModule<WallRunMovement>();
_movementSystem.ActivateDefaultModule();
```

#### `JumpHandler` Component

Encapsulates all jump logic (Composition Pattern):

- **Scalable**: Supports Triple/Quadruple jumps via counters
- **Classified**: Uses `JumpType` enum (Ground, Air, Wall, Coyote)
- **Event-Driven**: `OnJumpPerformed` event for easy Audio/VFX integration

#### `DefaultMovement` Module

Inherits from `MovementModuleBase` and composes `JumpHandler`:

- Ground movement (stable surfaces)
- Air movement (acceleration, drag, gravity)
- Rotation logic (look at camera)
- Crouch/uncrouch with collision detection
- Delegates jumping to `JumpHandler`

#### `NoClipMovement` Module

Handles debug flying mode. Isolated from main controller.

**Public API**:

```csharp
SetMoveInput(Vector3 moveVector)    // Pass WASD
RequestJump()                       // Jump pressed
OnWallHit(Vector3 wallNormal)       // Wall detected
```

---

## 🔄 How It Works

### Data Flow

```
1. User presses WASD
   ↓
2. PlayerInputHandler detects input
   → Sets MoveInput property
   ↓
3. PlayerController.Update()
   → Calculates camera-relative move vector
   → _defaultMovement.SetMoveInput(moveVector)
   ↓
4. KCC calls UpdateVelocity()
   → _movementSystem.UpdatePhysics(ref velocity, deltaTime)
       ↓
       PlayerMovementSystem
       → _activeModule.UpdatePhysics(ref velocity, deltaTime)
           ↓
           DefaultMovement
           → Ground/air physics
           → Jump logic
           → Gravity
```

### Key Insight

`PlayerController` **no longer knows** how movement works - it just:

1. Routes input to active module
2. Delegates physics to MovementSystem
3. Handles state transitions (Default ↔ NoClip)

---

## 🧩 Handler Pattern (Composition)

DefaultMovement uses **composition** to manage complex abilities. Instead of a 500+ line monolithic class, we extract features into focused handlers:

### Current Handlers

| Handler         | Purpose                             | File               |
| --------------- | ----------------------------------- | ------------------ |
| `JumpHandler`   | Jump logic (double, triple, coyote) | `JumpHandler.cs`   |
| `SlideHandler`  | Surface-aware sliding               | `SlideHandler.cs`  |
| `DashHandler`   | Charge-based dash system            | `DashHandler.cs`   |
| `MantleHandler` | Ledge grab & parkour                | `MantleHandler.cs` |

### Benefits

- ✅ **Single Responsibility** - Each handler has ONE job
- ✅ **Easy Testing** - Test handlers independently
- ✅ **Low Coupling** - Handlers don't know about each other
- ✅ **Scalable** - Add new handlers without bloating DefaultMovement

### Pattern Example

```csharp
public class DefaultMovement : IMovementModule
{
    // Composition: Inject handlers
    private readonly JumpHandler _jumpHandler;
    private readonly MantleHandler _mantleHandler;

    public DefaultMovement(...)
    {
        _jumpHandler = new JumpHandler(motor, config, input);
        _mantleHandler = new MantleHandler(motor, config);
    }

    public void UpdatePhysics(ref Vector3 velocity, float deltaTime)
    {
        // Mantle takes full control when active
        if (_mantleHandler.IsActive)
        {
            _mantleHandler.OverrideVelocity(ref velocity);
            return;
        }

        // Delegate to handlers
        _jumpHandler.ProcessJump(ref velocity, deltaTime);

        // Check for mantle after jump
        if (_jumpHandler.JumpConsumedThisUpdate && _mantleHandler.CanGrab())
        {
            _mantleHandler.TryGrab();
        }
    }
}
```

---

## 🧗 Mantle System (Parkour)

**NEW in December 2024**: Complete ledge grab and mantle system!

### Features

- **Raycast Detection** - No trigger colliders needed
- **Grab & Hang** - Jump near wall → Purple visual → Hang
- **Jump to Confirm** - Press jump while hanging → Smooth arc-based mantle
- **Configurable Layers** - Optimize detection with layer masks

### How It Works

```
1. Jump near wall → Detect ledge
2. Snap to hanging position (purple visual)
3. Press Jump → Confirm mantle
4. Arc motion: Pull UP first, then glide FORWARD
```

### Architecture

```
MantleHandler
├── State Machine: None → Grabbing → Hanging → Mantling
├── CanGrab() - Raycast-based detection
├── TryGrab() - Calculate positions
├── RequestMantle() - Jump confirmation
└── Update() - Arc-based interpolation
```

### Detection Algorithm

```csharp
bool CanGrab()
{
    1. Check in air (not grounded)
    2. Raycast forward for wall (within MaxGrabDistance)
    3. Validate wall angle (60°-120° = vertical)
    4. Raycast down from above wall for ledge top
    5. Validate ledge is walkable (<45° slope)
    6. Return true if all checks pass
}
```

### Configuration

In `PlayerMovementConfig`:

```csharp
[Header("Mantle")]
public LayerMask MantleLayers = -1;       // Ledge detection layers
public float MaxGrabDistance = 0.3f;      // Horizontal range
public float MinLedgeHeight = 1.0f;       // Min height
public float MaxLedgeHeight = 2.5f;       // Max height
public float MantleDuration = 0.55f;      // Animation time
public AnimationCurve MantleCurve;        // Motion smoothness
```

### Motion System

**Arc-Based Path** (Fortnite-style):

- **Vertical**: Pull up (0-70% of animation, accelerated)
- **Horizontal**: Glide forward (50-100%, delayed + smoothstep)
- **Result**: Natural "pull up, then climb over" feel

```csharp
// Vertical progress (completes early)
float verticalProgress = Mathf.Clamp01(curveValue * 1.4f);

// Horizontal progress (delayed start, smooth easing)
float horizontalRaw = Mathf.Clamp01((curveValue - 0.5f) / 0.5f);
float horizontalProgress = horizontalRaw * horizontalRaw * (3f - 2f * horizontalRaw);
```

### Input Flow

**Problem**: Jump input consumed before physics update

**Solution**: Route through `PlayerController.Update()`:

```csharp
// PlayerController.cs
if (InputHandler.JumpDown)
{
    if (_defaultMovement.IsMantling)
    {
        _defaultMovement.RequestMantleConfirm();  // Hanging → Mantle
    }
    else
    {
        _defaultMovement.RequestJump();           // Normal jump
    }
}
```

### Future Expansion

Mantle is the **foundation** for full parkour:

- **Shimmying** - Move left/right while hanging
- **Wall Climbing** - Vertical climbing (Assassin's Creed)
- **Vaulting** - Jump over obstacles
- **Corner Navigation** - Turn around corners

---

## 🚀 Adding New Abilities

### Example: Wall Run Module

#### Step 1: Create the Module

`Assets/Project/Scripts/Movement/WallRunMovement.cs`:

```csharp
using KinematicCharacterController;
using UnityEngine;

namespace Antigravity.Movement
{
    public class WallRunMovement : IMovementModule
    {
        private readonly KinematicCharacterMotor _motor;
        private readonly PlayerMovementConfig _config;
        private Vector3 _wallNormal;

        public WallRunMovement(
            KinematicCharacterMotor motor,
            PlayerMovementConfig config
        )
        {
            _motor = motor;
            _config = config;
        }

        public void OnActivated()
        {
            // Setup wall run state
        }

        public void OnDeactivated()
        {
            // Cleanup
        }

        public void UpdatePhysics(ref Vector3 velocity, float deltaTime)
        {
            // Wall run physics:
            // - Reduce gravity along wall direction
            // - Auto-move forward
            // - Handle jump off wall
        }

        public void AfterUpdate(float deltaTime) { }
    }
}
```

#### Step 2: Register in PlayerController

```csharp
private WallRunMovement _wallRunMovement;

private void Awake()
{
    // ... existing setup ...

    _wallRunMovement = new WallRunMovement(Motor, Config);
    _movementSystem.RegisterModule(_wallRunMovement);
}
```

#### Step 3: Activate from State

```csharp
// In WallRunState.EnterState()
_controller.MovementSystem.ActivateModule<WallRunMovement>();

// In WallRunState.ExitState()
_controller.MovementSystem.ActivateDefaultModule();
```

**Done!** Zero changes to existing movement code. 🎉

---

## 🎮 Controls

### Movement

- **WASD / Left Stick**: Move
- **Space / A Button**: Jump
  - Double jump (configurable)
  - Wall jump (configurable)
  - Jump buffering (0.15s before landing)
  - Coyote time (0.1s after leaving ledge)
- **C / B Button**: Crouch/Slide

### Camera

- **Mouse / Right Stick**: Look around
- Handled by `CinemachineInputBridge.cs`

### Time Powers

- **Hold Left Click / Right Trigger**: Rewind time

### Debug

- **N Key**: Toggle NoClip (fly through walls)
- **Escape**: Unlock cursor

---

## 🛠️ Configuration

### Creating a Movement Preset

1. Right-click in Project → **Antigravity** → **Player Movement Config**
2. Name it (e.g., `FastScoutConfig`)
3. Tweak values:
   - **For faster movement**: Increase `MaxStableMoveSpeed`
   - **For floaty jumps**: Reduce `Gravity.y` magnitude
   - **For tight control**: Increase `StableMovementSharpness`
   - **For easier jumps**: Increase `JumpPreGroundingGraceTime`

### Assigning to Player

1. Select Player GameObject
2. Find **Player Controller** component
3. Drag Config asset into **Config** slot

---

## 🎓 Design Patterns

### 1. Strategy Pattern

Movement modes are interchangeable strategies. The MovementSystem switches between them based on state.

**Real-world**: Like switching weapons in a game - each weapon is a complete combat strategy.

### 2. Composition Pattern

`DefaultMovement` doesn't implement jumping itself - it _composes_ a `JumpHandler`.
**Benefit**: Reusable components! `WallRunMovement` can also use `JumpHandler`.

### 3. Delegation Pattern

PlayerController doesn't do physics - it delegates to experts.

**Real-world**: CEO delegates to department heads rather than micromanaging.

### 3. Dependency Injection

Modules receive dependencies via constructor:

```csharp
public DefaultMovement(
    KinematicCharacterMotor motor,  // ← Injected
    PlayerMovementConfig config,    // ← Injected
    PlayerInputHandler input        // ← Injected
)
```

**Why**: Makes testing easy - inject mocks instead of real objects!

---

## 📝 Change Log

### [2024-12-09] Movement System Refactor ✨

**Major architectural change** - extracted monolithic physics into modular system

**Added**:

- `IMovementModule` interface
- `DefaultMovement` module (315 lines)
- `PlayerMovementSystem` manager (145 lines)

### [2024-12-10] Logic-Free Controller Refactor 🧘

**Complete Architecture Overhaul**:

- **PlayerController**: Stripped of ALL physics/math logic. Now purely coordinates components.
- **PlayerKCCAdapter**: Created to hide `ICharacterController` boilerplate.
- **NoClipMovement**: Extracted NoClip logic into its own module.
- **CameraInputProcessor**: Extracted camera-relative input math.
- **Result**: Controller is now a declarative manager.

### [2024-12-10] Mantle System & Handler Pattern

- **Added**: Complete parkour mantle system with ledge grab
- **Added**: MantleHandler with raycast-based detection
- **Added**: Arc-based motion (Fortnite-style pull up then climb over)
- **Added**: Configurable layer masks for optimization
- **Documented**: Handler Pattern (composition over inheritance)
- **Documented**: Mantle system architecture and usage
- Refactored: Input routing through PlayerController for proper timing
- Extracted: SlideHandler, DashHandler into separate classes

### [2024-12-06] Jump Handler Refactor

- Extracted Jump logic → `JumpHandler.cs`
- Refactored `DefaultMovement` to use Composition
- **Added**: Triple Jump support, JumpType enums, OnJump events

### [2024-12-04] Input & Config Extraction

- Extracted input handling → `PlayerInputHandler.cs`
- Extracted settings → `PlayerMovementConfig` ScriptableObject
- Fixed NoClip functionality
- Removed legacy code

### [2024-12-03] Jump Improvements

- Added jump buffering (pre-ground grace time)
- Added coyote time (post-ground grace time)
- Made configurable via ScriptableObject

---

## 🎯 Future Abilities (Easy to Add)

Each takes ~30 minutes and is its own file:

- **Dash**: Instant velocity burst
- **Wall Run**: Run along walls
- **Grapple Hook**: Pull toward point
- **Combat Mode**: Lock-on targeting movement
- **Glide**: Slow fall with directional control

---

## 📚 Further Reading

- [INPUT_SYSTEM_API.md](./INPUT_SYSTEM_API.md) - Input system deep dive
- [CODING_STANDARDS.md](./CODING_STANDARDS.md) - Code style guide
- [SCALABLE_ARCHITECTURE.md](../.gemini/antigravity/brain/*/SCALABLE_ARCHITECTURE.md) - Full architecture theory

---

**Pro Tip**: When adding a new movement ability, start by copying `DefaultMovement.cs` as a template. Keep what you need, delete what you don't!

_This guide evolves with the project. Update it when adding new abilities!_ 🚀
