# 🏃 Movement System Guide

> **Last Updated**: December 2024 - Major refactor to modular architecture

Welcome to the **Antigravity Movement System**! This guide explains our modular, scalable architecture for player movement.

---

## 📚 Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [The Three Pillars](#the-three-pillars)
3. [How It Works](#how-it-works)
4. [Adding New Abilities](#adding-new-abilities)
5. [Controls](#controls)
6. [Configuration](#configuration)
7. [Design Patterns](#design-patterns)
8. [Change Log](#change-log)

---

## 🏗️ Architecture Overview

We follow the **Strategy Pattern** - movement modes (jump, wallrun, combat) are separate modules that can be swapped at runtime.

```
┌──────────────────────────┐
│   PlayerController       │  ← Thin coordinator
│   - Routes input         │
│   - Delegates physics    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ PlayerMovementSystem     │  ← Traffic controller
│ - Manages modules        │
│ - Switches active module │
└────────────┬─────────────┘
             │
     ┌───────┼───────┐
     ▼       ▼       ▼
┌─────────┐ ┌─────────┐ ┌─────────┐
│Default  │ │WallRun  │ │Combat   │  ← Physics modules
│Movement │ │Movement │ │Movement │
└─────────┘ └─────────┘ └─────────┘
```

### Why This Architecture?

**Before** (Monolithic):

- 523 lines in PlayerController
- 145-line UpdateVelocity method
- Adding wallrun = modifying core physics (high risk!)

**After** (Modular):

- 240 lines in PlayerController (54% reduction)
- 3-line UpdateVelocity (just delegates)
- Adding wallrun = create new file (zero risk!)

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

#### `DefaultMovement` Module

Contains ALL default physics:

- Ground movement (stable surfaces)
- Air movement (acceleration, drag, gravity)
- Jump system (single, double, wall, buffering, coyote)
- Crouch/uncrouch with collision detection

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

### 2. Delegation Pattern

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

**Changed**:

- `PlayerController`: 523 → 240 lines (54% reduction!)
- `UpdateVelocity`: 145 → 3 lines (delegates to system)

**Result**:

- Adding new abilities now takes 30 min vs 2 hours
- Zero risk to existing movement
- Industry-standard architecture

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
