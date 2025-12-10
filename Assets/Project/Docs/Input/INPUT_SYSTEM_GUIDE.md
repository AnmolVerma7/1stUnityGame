# 🎮 Input System - Complete Guide

> **Comprehensive documentation** for the portable, command-based input system

---

## 📚 Quick Navigation

- [Architecture Overview](#architecture-overview)
- [Getting Started](#getting-started)
- [Core Components](#core-components)
- [Commands Reference](#commands-reference)
- [Advanced Features](#advanced-features)
- [Reusing in Projects](#reusing-in-other-projects)

---

## 🏗️ Architecture Overview

Our input system uses the **Command Pattern** to decouple input detection from game logic.

```
Unity Input System
       ↓
InputRouter (dispatcher)
       ↓
IInputCommand (strategy)
       ↓
Your Game Logic
```

**Benefits**:

- ✅ Fully portable across projects
- ✅ Device-agnostic (mouse/gamepad handled automatically)
- ✅ Combo support built-in
- ✅ Input buffering for responsive controls

---

## 🚀 Getting Started

### Basic Setup

```csharp
public class MyPlayer : MonoBehaviour, IInputUser
{
    public void RegisterInputs(InputBuilder builder)
    {
        // Simple button
        builder.Bind(builder.Actions.Jump)
               .Press(() => Debug.Log("Jump!"))
               .Register();

        // Continuous value
        builder.Bind(builder.Actions.Move)
               .To<Vector2>(value => _moveInput = value);
    }
}
```

The `InputRouter` auto-discovers `IInputUser` components and calls `RegisterInputs()`.

---

## 🧱 Core Components

### 📂 Core/

#### `InputRouter.cs` ⚙️

**[PROJECT SPECIFIC]** - Bridge to Unity's generated input class

The central hub that:

- Owns the `InputMap` (Unity-generated class)
- Manages action lifecycle (Enable/Disable)
- Dispatches events to registered commands

**To reuse in new project**:

1. Replace `InputMap` with your generated class name
2. Update `IPlayerActions` interface name
3. Update method bindings in constructor

---

#### `InputBufferService.cs` ⏱️

Singleton that buffers input actions when conditions aren't met.

**Example**: Jump pressed mid-air → buffers → executes on landing

```csharp
InputBufferService.Instance.BufferAction(
    action: DoJump,
    duration: 0.2f,
    condition: () => IsGrounded
);
```

**Why essential**: Makes controls feel responsive and forgiving.

---

#### `IInputCommand.cs` 📜

The contract all commands implement:

```csharp
public interface IInputCommand
{
    void Execute(InputAction.CallbackContext context);
}
```

---

#### `ILookRig.cs` / `ILookInputReceiver.cs` / `IMoveInputReceiver.cs` 🎥

Interfaces for components that receive input:

```csharp
public interface ILookInputReceiver
{
    void AddLookDelta(Vector2 delta);  // Mouse (pixels)
    void SetLookRate(Vector2 rate);    // Gamepad (normalized)
    void SetAimHeld(bool held);
}
```

**Why keep**: Enables swappable camera/vehicle systems without rewriting input logic.

---

### 📂 Commands/

The "verbs" - translate Unity events into game logic.

#### `ButtonCommand.cs` 🔘

Simple press/release handling:

```csharp
builder.Bind(builder.Actions.Jump)
       .Press(() => jumpRequested = true)
       .Release(() => jumpHeld = false)
       .Register();
```

**Callbacks**:

- `OnStarted` - Button down
- `OnPerformed` - Button held/up
- `OnCanceled` - Button up

---

#### `ValueCommand<T>.cs` 📊

Continuous data (float, Vector2):

```csharp
builder.Bind(builder.Actions.Move)
       .To<Vector2>(v => moveInput = v);
```

**Auto-zeroing**: Ensures values reset on cancel (fixing stick drift).

---

#### `LookInputCommand.cs` 🎯

**Smart camera input** - auto-detects device:

```csharp
router.Bind(
    Actions.Look,
    new LookInputCommand(
        delta => lookDelta += delta,    // Mouse
        rate => lookRate = rate          // Gamepad
    )
);
```

**Why important**: Solves "joystick snaps camera" bug by separating delta vs rate.

---

#### `HoldTapReleaseCommand.cs` ⏲️

Distinguishes tap from hold:

```csharp
builder.Bind(builder.Actions.Interact)
       .HoldOrTap(
           holdDuration: 0.5f,
           onTap: () => QuickInteract(),
           onHold: () => OpenMenu(),
           onRelease: () => CloseMenu()
       )
       .Register();
```

---

#### `ConditionalCommand.cs` ✅

Gate execution behind a check:

```csharp
builder.Bind(builder.Actions.Dash)
       .When(() => !isStunned && hasStamina)
       .Press(() => Dash())
       .Register();
```

---

#### `BufferedButtonCommand.cs` 🗃️

Auto-buffers if condition fails:

```csharp
builder.Bind(builder.Actions.Jump)
       .Buffer(
           windowTime: 0.2f,
           condition: () => Motor.IsGrounded,
           onExecute: () => Jump()
       )
       .Register();
```

**Result**: Jump 0.2s before landing = executes on landing!

---

#### `CompositeCommand.cs` 🔗

Chain multiple commands:

```csharp
new CompositeCommand(
    new ButtonCommand(...),
    new ConditionalCommand(...)
);
```

---

### 📂 Combos/

Fighting game-style input sequences.

#### `ComboRecognizer.cs` 🥋

Pattern matcher for button sequences:

```csharp
var combo = new ComboRecognizer.Combo(
    name: "Triple Slash",
    steps: new[] { ComboButton.Attack, ComboButton.Attack, ComboButton.Attack },
    maxStepDelay: 0.4f,
    onTriggered: () => ExecuteTripleSlash()
);

recognizer.AddCombo(combo);
recognizer.OnButtonPressed(ComboButton.Attack);
```

**Features**:

- Early buffer window (press next move before current finishes)
- Configurable timing windows
- Cancellable combos

---

#### `ComboTypes.cs` 🎯

**[PROJECT SPECIFIC]** - Defines your combo vocabulary

```csharp
public enum ComboButton
{
    Attack,
    Heavy,
    Dodge,
    Special
}
```

**To reuse**: Replace with your game's button names.

---

### 📂 Root Scripts

#### `InputBuilder.cs` 🏗️

**[PROJECT SPECIFIC]** - Fluent API for binding

Wraps complex setup into readable code:

```csharp
builder.Bind(Actions.Jump)  // What action?
       .Buffer(0.2f, IsGrounded, Jump)  // How?
       .Register();  // Done!
```

**To reuse**: Update `Actions` property type to match your `InputRouter`.

---

#### `GameInputContext.cs` 📦

**[TEMPLATE]** - State container for conditions

```csharp
public class GameInputContext
{
    public bool IsMenuOpen;
    public bool HasTarget;
    public bool IsStunned;
}
```

**Usage**:

```csharp
builder.Bind(Actions.Dodge)
       .When(() => !context.IsStunned)
       .Press(Dodge)
       .Register();
```

---

#### `IInputUser.cs` 👤

Interface for input consumers:

```csharp
public interface IInputUser
{
    void RegisterInputs(InputBuilder builder);
}
```

Implement this on Player, Vehicle, UI, etc.

---

## 🎓 Advanced Features

### Device Detection

`LookInputCommand` auto-detects:

```csharp
// Automatically routes to correct callback
new LookInputCommand(
    delta => ApplyMouseDelta(delta),
    rate => ApplyGamepadRate(rate)
);
```

**Devices detected**: Mouse, Pen, Pointer vs Gamepad, Joystick

---

### Input Buffering

**Problem**: Jump pressed 0.1s before landing → ignored  
**Solution**: Buffer it!

```csharp
.Buffer(
    windowTime: 0.2f,          // How long to remember
    condition: () => IsGrounded,  // When to execute
    onExecute: Jump            // What to do
)
```

---

### Hold vs Tap

```csharp
.HoldOrTap(
    holdDuration: 0.5f,
    onTap: QuickAction,
    onHold: ChargedAction,
    onRelease: Release
)
```

---

### Combo Sequences

```csharp
var fireball = new Combo(
    name: "Fireball",
    steps: new[] { Down, DownRight, Right, Attack },
    maxStepDelay: 0.3f,
    earlyBufferWindow: 0.1f,
    onTriggered: CastFireball
);
```

---

## ♻️ Reusing in Other Projects

### Step 1: Copy Core System

Copy these folders (no changes needed):

- `Core/` (except `InputRouter.cs`)
- `Commands/`

### Step 2: Update Project-Specific Files

#### `InputRouter.cs`

Replace Unity-generated references:

```csharp
// OLD (your project)
private InputMap _inputMap;

// NEW (new project)
private MyGameInputActions _inputMap;
```

Update all method bindings to match new action names.

---

#### `InputBuilder.cs`

Update `Actions` property:

```csharp
public MyGameInputActions.PlayerActions Actions => _router.Player;
```

---

#### `ComboTypes.cs` (Optional)

Define your game's combo buttons:

```csharp
public enum ComboButton
{
    LightAttack,
    HeavyAttack,
    Parry,
    Ability1
}
```

---

### Step 3: Create Your Input Asset

1. Create Input Actions asset in Unity
2. Generate C# class
3. Update `InputRouter` to use it

**Done!** Core system works unchanged.

---

## 📖 Example Implementations

### Complete Player Setup

```csharp
public class PlayerInputHandler : MonoBehaviour, IInputUser
{
    private Vector2 _moveInput;
    private bool _jumpPressed;

    public Vector2 MoveInput => _moveInput;
    public bool JumpPressed => _jumpPressed;

    public void RegisterInputs(InputBuilder builder)
    {
        // Movement
        builder.Bind(builder.Actions.Move)
               .To<Vector2>(v => _moveInput = v);

        // Jump with buffering
        builder.Bind(builder.Actions.Jump)
               .Buffer(0.2f, () => isGrounded, () => _jumpPressed = true)
               .Register();

        // Look (auto-device detection)
        var router = GetComponent<InputRouter>();
        router.Bind(
            builder.Actions.Look,
            new LookInputCommand(
                delta => _lookDelta += delta,
                rate => _lookRate = rate
            )
        );
    }

    private void LateUpdate()
    {
        // Reset one-frame triggers
        _jumpPressed = false;
        _lookDelta = Vector2.zero;
    }
}
```

---

## 🎯 Best Practices

### ✅ Do

- Use `IInputUser` for clean registration
- Reset one-frame triggers in `LateUpdate()`
- Use buffering for time-sensitive inputs
- Separate delta (mouse) from rate (gamepad) for camera
- Keep `GameInputContext` lightweight (just flags)

### ❌ Don't

- Directly reference `InputAction` in gameplay code
- Forget to `Register()` after binding
- Use magic numbers (create constants)
- Mix input detection with game logic

---

## 🔧 Troubleshooting

### Input Not Detected

1. Check `InputRouter` is enabled
2. Verify `RegisterInputs()` was called
3. Ensure you called `.Register()`
4. Check Script Execution Order

### Camera Snapping with Gamepad

Use `LookInputCommand` instead of `ValueCommand`:

```csharp
// ❌ Bad - will snap
builder.Bind(Actions.Look).To<Vector2>(v => look = v);

// ✅ Good - smooth
new LookInputCommand(delta => ..., rate => ...);
```

### Combo Not Triggering

- Check `maxStepDelay` isn't too short
- Verify button enum names match
- Ensure `OnButtonPressed()` is being called

---

## 📚 Related Docs

- [MOVEMENT_SYSTEM_GUIDE.md](../Movement/MOVEMENT_SYSTEM_GUIDE.md) - How movement uses this system
- [CODING_STANDARDS.md](../Development/CODING_STANDARDS.md) - Code style guide

---

**This system is battle-tested and production-ready.** Use it, extend it, make it yours! 🚀
