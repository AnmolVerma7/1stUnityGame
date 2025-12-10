# Kinematic Character Controller (KCC) - Comprehensive Documentation

## Table of Contents

1. [System Overview](#system-overview)
2. [Architecture](#architecture)
3. [Core Components](#core-components)
   - [KinematicCharacterMotor](#kinematiccharactermotor)
   - [KinematicCharacterSystem](#kinematiccharactersystem)
   - [PhysicsMover](#physicsmover)
4. [Interfaces](#interfaces)
   - [ICharacterController](#icharactercontroller)
   - [IMoverController](#imovercontroller)
5. [Data Structures](#data-structures)
6. [Configuration](#configuration)
7. [Update Pipeline](#update-pipeline)
8. [Advanced Features](#advanced-features)
9. [Usage Guide](#usage-guide)
10. [Best Practices](#best-practices)
11. [Troubleshooting](#troubleshooting)

---

## System Overview

The Kinematic Character Controller (KCC) is a production-ready, physics-based character movement system for Unity. It provides stable, responsive character control while handling complex scenarios like:

- **Stable Ground Detection**: Multi-probe ground checking with configurable stability angles
- **Step Handling**: Automatic climbing of steps and stairs with configurable methods
- **Ledge Detection**: Detection and handling of ledges with distance-based stability
- **Moving Platform Support**: Full interaction with moving kinematic rigidbodies
- **Rigidbody Interaction**: Configurable pushing and collision with dynamic objects
- **Slope Handling**: Smooth movement on slopes with velocity projection
- **Collision Resolution**: Iterative overlap and collision solving
- **Interpolation**: Smooth visual movement between physics updates
- **Movement Constraints**: Optional planar movement constraints

### Key Design Principles

1. **Kinematic Movement**: Characters use kinematic rigidbodies with capsule colliders
2. **Custom Update Loop**: Managed by `KinematicCharacterSystem` for precise control
3. **Interface-Based Control**: Controllers implement `ICharacterController` for flexibility
4. **Separation of Concerns**: Movement logic separate from character behavior
5. **Performance Focused**: Optimized collision detection with configurable iteration limits

---

## Architecture

### System Components

```
┌─────────────────────────────────────────┐
│     KinematicCharacterSystem            │
│     (Singleton Manager)                 │
│  - Manages update loop                  │
│  - Handles interpolation                │
│  - Coordinates all motors/movers        │
└────────────┬────────────────────────────┘
             │
    ┌────────┴────────┐
    │                 │
┌───▼────────┐   ┌───▼──────┐
│  Character │   │  Physics │
│   Motors   │   │  Movers  │
└───┬────────┘   └───┬──────┘
    │                │
┌───▼────────┐   ┌───▼──────────┐
│ICharacter  │   │IMover        │
│Controller  │   │Controller    │
│(User Code) │   │(User Code)   │
└────────────┘   └──────────────┘
```

### Update Flow

```
FixedUpdate
├─ PreSimulationInterpolationUpdate
│  └─ Save pre-simulation poses
├─ PhysicsMover VelocityUpdate
│  └─ Calculate mover velocities
├─ Character UpdatePhase1
│  ├─ BeforeCharacterUpdate callback
│  ├─ Handle MovePosition calls
│  ├─ Resolve initial overlaps
│  ├─ Ground probing
│  ├─ PostGroundingUpdate callback
│  └─ Handle rigidbody attachment
├─ PhysicsMover Transform Update
│  └─ Apply mover positions
├─ Character UpdatePhase2
│  ├─ UpdateRotation callback
│  ├─ Handle MoveRotation calls
│  ├─ Resolve attached rigidbody overlaps
│  ├─ UpdateVelocity callback
│  ├─ Process movement
│  ├─ Handle rigidbody interactions
│  └─ AfterCharacterUpdate callback
└─ PostSimulationInterpolationUpdate
   └─ Prepare for interpolation

LateUpdate
└─ CustomInterpolationUpdate
   └─ Smooth interpolation between poses
```

---

## Core Components

### KinematicCharacterMotor

The primary component responsible for character movement, collision detection, and physics simulation.

#### Capsule Configuration

```csharp
// Capsule dimensions
[SerializeField] private float CapsuleRadius = 0.5f;
[SerializeField] private float CapsuleHeight = 2f;
[SerializeField] private float CapsuleYOffset = 1f;
[SerializeField] private PhysicsMaterial CapsulePhysicsMaterial;

// The capsule is automatically configured as:
// - Direction: Y-axis (1)
// - Kinematic rigidbody
// - No interpolation (handled by system)
```

**Important Notes:**
- Radius is clamped to maximum of `CapsuleHeight * 0.5`
- Height must be at least `(radius * 2) + 0.01` for valid geometry
- YOffset positions the capsule center relative to transform
- Character and all parents **must** have (1,1,1) scale

#### Grounding System

The grounding system uses a sophisticated multi-probe approach:

```csharp
// Primary settings
public float GroundDetectionExtraDistance = 0f;  // Extends detection range
public float MaxStableSlopeAngle = 60f;          // 0-89 degrees
public LayerMask StableGroundLayers = -1;        // What counts as ground
```

**Ground Detection Process:**

1. **Initial Probe**: Sweeps downward from character position
2. **Stability Check**: Evaluates slope angle against `MaxStableSlopeAngle`
3. **Ledge Detection** (if enabled):
   - Inner probe: Slightly inward from hit point
   - Outer probe: Slightly outward from hit point
   - Compares stability of both sides
4. **Step Detection** (if enabled):
   - Checks for valid step geometry
   - Validates step height against `MaxStepHeight`
5. **Ground Snapping**: Moves character down to ground if stable

**Grounding Status:**

```csharp
public struct CharacterGroundingReport
{
    public bool FoundAnyGround;              // Any ground detected
    public bool IsStableOnGround;            // Standing on stable ground
    public bool SnappingPrevented;           // Snapping was blocked
    public Vector3 GroundNormal;             // Primary ground normal
    public Vector3 InnerGroundNormal;        // Inner probe result
    public Vector3 OuterGroundNormal;        // Outer probe result
    public Collider GroundCollider;          // Ground collider reference
    public Vector3 GroundPoint;              // Contact point
}
```

#### Step Handling

Three methods for handling steps:

**None**: No step handling
- Character treats steps as walls
- Simplest, best performance
- Use when: No steps in environment

**Standard**: Basic step detection
- Detects steps at hit point
- Validates step height and geometry
- Good balance of features and performance
- Use when: Standard stair/step geometry

**Extra**: Enhanced step detection
- Additional checks for small/narrow steps
- Uses `MinRequiredStepDepth` parameter
- Handles edge cases better
- More expensive computationally
- Use when: Complex step geometry or small platforms

```csharp
public enum StepHandlingMethod
{
    None,
    Standard,
    Extra
}

public StepHandlingMethod StepHandling = StepHandlingMethod.Standard;
public float MaxStepHeight = 0.5f;
public float MinRequiredStepDepth = 0.1f;  // Extra mode only
public bool AllowSteppingWithoutStableGrounding = false;
```

**Step Detection Algorithm:**

1. Check if hit is vertical enough (< `CorrelationForVerticalObstruction`)
2. Cast from above step height downward
3. Validate hit matches stepped collider
4. Check for obstructions above step
5. Verify inner ground stability
6. If valid, move character to step top

#### Ledge Handling

Provides detailed ledge information for gameplay decisions:

```csharp
public bool LedgeAndDenivelationHandling = true;
public float MaxStableDistanceFromLedge = 0.5f;        // From capsule center
public float MaxVelocityForLedgeSnap = 0f;             // Speed limit for snapping
public float MaxStableDenivelationAngle = 180f;        // Slope angle change limit
```

**Ledge Detection:**

Uses secondary probes at `SecondaryProbesHorizontal` offset:
- **Inner Probe**: Toward character center
- **Outer Probe**: Away from character center

Ledge detected when one side is stable and other is not:

```csharp
public struct HitStabilityReport
{
    // Ledge information
    public bool LedgeDetected;
    public bool IsOnEmptySideOfLedge;
    public float DistanceFromLedge;
    public bool IsMovingTowardsEmptySideOfLedge;
    public Vector3 LedgeGroundNormal;
    public Vector3 LedgeRightDirection;
    public Vector3 LedgeFacingDirection;
}
```

**Denivelation** prevents snapping when slope changes too drastically:
- Compares current and previous ground normals
- Useful for "launching" off ramps or bumps

#### Rigidbody Interaction

Three interaction modes:

```csharp
public enum RigidbodyInteractionType
{
    None,           // Pass through rigidbodies
    Kinematic,      // Push with infinite force
    SimulatedDynamic// Push with simulated mass
}

public bool InteractiveRigidbodyHandling = true;
public RigidbodyInteractionType RigidbodyInteractionType;
public float SimulatedCharacterMass = 1f;
public bool PreserveAttachedRigidbodyMomentum = true;
```

**Interaction Process:**

1. **Attachment Detection**:
   - Automatically attaches to ground rigidbody if stable
   - Can be overridden with `AttachedRigidbodyOverride`

2. **Velocity Calculation**:
   - Linear velocity from rigidbody
   - Angular velocity converted to linear at character position
   - Combined into `AttachedRigidbodyVelocity`

3. **Momentum Preservation**:
   - When `PreserveAttachedRigidbodyMomentum` is true
   - Adds platform velocity to `BaseVelocity` on detachment
   - Allows jumping off moving platforms with momentum

4. **Collision Resolution**:
   ```csharp
   // Kinematic mode: Character wins all collisions
   if (RigidbodyInteractionType == Kinematic)
       characterToBodyMassRatio = 1f;
   
   // SimulatedDynamic: Mass-based resolution
   float characterToBodyMassRatio = 
       characterMass / (characterMass + hitBodyMass);
   ```

5. **Force Application**:
   - Character velocity adjusted by collision
   - Rigidbody gets force at impact point
   - Uses `AddForceAtPosition(velocityChange, hitPoint, ForceMode.VelocityChange)`

#### Movement Constraints

Optional planar constraint for 2.5D or restricted movement:

```csharp
public bool HasPlanarConstraint = false;
public Vector3 PlanarConstraintAxis = Vector3.forward;  // Plane normal
```

Effect:
- Projects all movement onto plane perpendicular to axis
- Applied after all other movement calculations
- Useful for side-scrollers or movement restrictions

#### Movement Resolution

The core movement algorithm uses iterative sweeps:

```csharp
public int MaxMovementIterations = 5;
public bool CheckMovementInitialOverlaps = true;
public bool KillVelocityWhenExceedMaxMovementIterations = true;
public bool KillRemainingMovementWhenExceedMaxMovementIterations = true;
```

**Movement Algorithm:**

```
For each movement iteration (up to MaxMovementIterations):
  1. If CheckMovementInitialOverlaps:
     - Check for overlaps at current position
     - Find most obstructing overlap
     - Use as hit if found
  
  2. Sweep capsule in movement direction
  3. If hit detected:
     a. Move to hit point (minus CollisionOffset)
     b. Evaluate hit stability
     c. Check for valid step
     d. If valid step found:
        - Move to step top
        - Project velocity onto ground
        - Continue
     e. Otherwise:
        - Calculate obstruction normal
        - Call OnMovementHit callback
        - Store rigidbody hit if applicable
        - Project velocity based on stability
  
  4. Update remaining movement
  5. Continue until movement exhausted or max iterations
```

**Velocity Projection:**

Depends on grounding status and hit stability:

```csharp
// On stable ground
if (GroundingStatus.IsStableOnGround && !MustUnground())
{
    if (stableOnHit)
        // Reorient to new surface
        velocity = GetDirectionTangentToSurface(velocity, obstructionNormal);
    else
        // Follow ground plane around obstacle
        velocity = GetDirectionTangentToSurface(velocity, obstructionUpAlongGround);
        velocity = ProjectOnPlane(velocity, obstructionNormal);
}
// In air
else
{
    if (stableOnHit)
        // Landing
        velocity = ProjectOnPlane(velocity, CharacterUp);
        velocity = GetDirectionTangentToSurface(velocity, obstructionNormal);
    else
        // Sliding along wall
        velocity = ProjectOnPlane(velocity, obstructionNormal);
}
```

**Crease Detection:**

When hitting two surfaces forming a blocking corner/crease:

```csharp
public enum MovementSweepState
{
    Initial,              // First sweep
    AfterFirstHit,        // One hit detected
    FoundBlockingCrease,  // Two surfaces blocking movement
    FoundBlockingCorner   // Complete blockage
}
```

Process:
1. After first hit, enters `AfterFirstHit` state
2. On second hit, evaluates if surfaces form blocking crease
3. Projects velocity along crease direction if valid
4. On third hit, enters `FoundBlockingCorner` - stops all movement

#### Collision Detection Methods

The motor provides several collision detection primitives:

**Overlap Detection:**

```csharp
int CharacterCollisionsOverlap(
    Vector3 position,
    Quaternion rotation,
    Collider[] overlappedColliders,
    float inflate = 0f,
    bool acceptOnlyStableGroundLayer = false)
```

- Uses `Physics.OverlapCapsuleNonAlloc`
- Filters invalid colliders
- Optional inflation for safety margins
- Can restrict to stable ground layers

**Sweep Detection:**

```csharp
int CharacterCollisionsSweep(
    Vector3 position,
    Quaternion rotation,
    Vector3 direction,
    float distance,
    out RaycastHit closestHit,
    RaycastHit[] hits,
    float inflate = 0f,
    bool acceptOnlyStableGroundLayer = false)
```

- Uses `Physics.CapsuleCastNonAlloc`
- Returns closest valid hit
- Backsteps by `SweepProbingBackstepDistance` to catch edge cases
- Filters and sorts all hits

**Raycast Detection:**

```csharp
int CharacterCollisionsRaycast(
    Vector3 position,
    Vector3 direction,
    float distance,
    out RaycastHit closestHit,
    RaycastHit[] hits,
    bool acceptOnlyStableGroundLayer = false)
```

- Used for secondary probes (ledges, steps)
- Simpler than sweeps, better performance
- Returns closest valid hit

#### Collision Filtering

All collision methods filter through:

```csharp
private bool CheckIfColliderValidForCollisions(Collider coll)
{
    // 1. Ignore self
    if (coll == Capsule) return false;
    
    // 2. Check if moving from attached rigidbody
    if (_isMovingFromAttachedRigidbody)
        if (coll.attachedRigidbody == _attachedRigidbody)
            return false;
    
    // 3. Rigidbody interaction type check
    if (RigidbodyInteractionType == Kinematic && 
        !coll.attachedRigidbody.isKinematic)
    {
        coll.attachedRigidbody.WakeUp();
        return false;
    }
    
    // 4. Custom validation
    return CharacterController.IsColliderValidForCollisions(coll);
}
```

#### Key Properties

**Read-Only State:**

```csharp
// Transform and positioning
public Transform Transform { get; }
public Vector3 TransientPosition { get; }      // Current simulation position
public Quaternion TransientRotation { get; }   // Current simulation rotation
public Vector3 CharacterUp { get; }            // Character's up direction
public Vector3 CharacterForward { get; }       // Character's forward
public Vector3 CharacterRight { get; }         // Character's right

// Velocity
public Vector3 Velocity { get; }               // Total velocity (base + attached)
public Vector3 BaseVelocity { get; set; }      // Character's own velocity
public Vector3 AttachedRigidbodyVelocity { get; } // From attached rigidbody

// Grounding
public CharacterGroundingReport GroundingStatus { get; }
public CharacterTransientGroundingReport LastGroundingStatus { get; }

// Attachment
public Rigidbody AttachedRigidbody { get; }
public Rigidbody AttachedRigidbodyOverride { get; set; }  // Manual override

// Collision detection
public LayerMask CollidableLayers { get; set; } // Built from collision matrix
public int OverlapsCount { get; }
public OverlapResult[] Overlaps { get; }
```

**Capsule Geometry Helpers:**

```csharp
public Vector3 CharacterTransformToCapsuleCenter { get; }
public Vector3 CharacterTransformToCapsuleBottom { get; }
public Vector3 CharacterTransformToCapsuleTop { get; }
public Vector3 CharacterTransformToCapsuleBottomHemi { get; }  // Hemisphere center
public Vector3 CharacterTransformToCapsuleTopHemi { get; }     // Hemisphere center
```

#### Key Methods

**Position/Rotation Control:**

```csharp
void SetPosition(Vector3 position, bool bypassInterpolation = true)
void SetRotation(Quaternion rotation, bool bypassInterpolation = true)
void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool bypassInterpolation = true)
```

- Sets transform, rigidbody, and simulation positions
- `bypassInterpolation = true`: Teleports (updates interpolation targets)
- `bypassInterpolation = false`: Smooth interpolation to target

**Queued Movement:**

```csharp
void MoveCharacter(Vector3 toPosition)
void RotateCharacter(Quaternion toRotation)
```

- Queues movement to be processed in next update
- Takes collision solving into account
- Preferred over direct position setting for gameplay movement

**Grounding Control:**

```csharp
void ForceUnground(float time = 0.1f)
bool MustUnground()
```

- `ForceUnground`: Prevents snapping to ground for duration
- Use when applying upward forces (jumping, launches)
- `MustUnground`: Checks if currently forced to be ungrounded

**Utility Methods:**

```csharp
Vector3 GetVelocityForMovePosition(Vector3 fromPosition, Vector3 toPosition, float deltaTime)
Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal)
void EvaluateHitStability(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, 
                          Vector3 atCharacterPosition, Quaternion atCharacterRotation, 
                          Vector3 withCharacterVelocity, ref HitStabilityReport stabilityReport)
```

**State Management:**

```csharp
KinematicCharacterMotorState GetState()
void ApplyState(KinematicCharacterMotorState state, bool bypassInterpolation = true)
```

- Save/restore complete motor state
- Useful for:
  - Networking/replication
  - Rewind systems
  - State recording/playback

**Configuration:**

```csharp
void SetCapsuleDimensions(float radius, float height, float yOffset)
void SetCapsuleCollisionsActivation(bool collisionsActive)
void SetMovementCollisionsSolvingActivation(bool movementCollisionsSolvingActive)
void SetGroundSolvingActivation(bool stabilitySolvingActive)
```

#### Performance Considerations

**Critical Constants** (carefully tuned, not exposed):

```csharp
const int MaxHitsBudget = 16;                    // Max hits per sweep
const int MaxCollisionBudget = 16;               // Max overlaps to check
const int MaxGroundingSweepIterations = 2;       // Ground probe iterations
const int MaxSteppingSweepIterations = 3;        // Step detection iterations
const int MaxRigidbodyOverlapsCount = 16;        // Max rigidbody hits stored
const float CollisionOffset = 0.01f;             // Collision safety margin
const float GroundProbeReboundDistance = 0.02f;  // Ground probe bounce
const float MinimumGroundProbingDistance = 0.005f;
const float SecondaryProbesVertical = 0.02f;     // Ledge probe offsets
const float SecondaryProbesHorizontal = 0.001f;
```

**Optimization Tips:**

1. **Reduce MaxMovementIterations** if simple geometry (default: 5)
2. **Disable features you don't need:**
   - `StepHandling = None` if no steps
   - `LedgeAndDenivelationHandling = false` if not needed
   - `InteractiveRigidbodyHandling = false` if no rigidbody interaction
3. **Use Standard step method** unless Extra needed
4. **Configure StableGroundLayers** properly to reduce collision checks
5. **CheckMovementInitialOverlaps = false** if sure no overlap situations
6. **Increase list capacities** on system to prevent runtime allocations

---

### KinematicCharacterSystem

Singleton manager that orchestrates all character motors and physics movers.

#### Initialization

```csharp
// Automatically created on first motor/mover registration
KinematicCharacterSystem.EnsureCreation();

// Access instance
KinematicCharacterSystem system = KinematicCharacterSystem.GetInstance();
```

Creates a `DontDestroyOnLoad` GameObject with the system component.

#### Registration

Motors and movers auto-register in `OnEnable`:

```csharp
// Motor registration
void OnEnable()
{
    KinematicCharacterSystem.EnsureCreation();
    KinematicCharacterSystem.RegisterCharacterMotor(this);
}

void OnDisable()
{
    KinematicCharacterSystem.UnregisterCharacterMotor(this);
}
```

Manual registration:

```csharp
KinematicCharacterSystem.RegisterCharacterMotor(motor);
KinematicCharacterSystem.RegisterPhysicsMover(mover);

// Pre-allocate capacity to avoid runtime allocations
KinematicCharacterSystem.SetCharacterMotorsCapacity(100);
KinematicCharacterSystem.SetPhysicsMoversCapacity(50);
```

#### Configuration

```csharp
// Access settings
KCCSettings settings = KinematicCharacterSystem.Settings;

settings.AutoSimulation = true;              // Automatic update in FixedUpdate
settings.Interpolate = true;                 // Enable visual interpolation
settings.MotorsListInitialCapacity = 100;    // Initial motor list size
settings.MoversListInitialCapacity = 100;    // Initial mover list size
```

#### Update Pipeline

**Automatic Mode** (`AutoSimulation = true`):

```csharp
void FixedUpdate()
{
    float deltaTime = Time.deltaTime;
    
    if (Settings.Interpolate)
        PreSimulationInterpolationUpdate(deltaTime);
    
    Simulate(deltaTime, CharacterMotors, PhysicsMovers);
    
    if (Settings.Interpolate)
        PostSimulationInterpolationUpdate(deltaTime);
}

void LateUpdate()
{
    if (Settings.Interpolate)
        CustomInterpolationUpdate();
}
```

**Manual Mode** (`AutoSimulation = false`):

```csharp
// You control when simulation happens
void MyCustomUpdate()
{
    float customDeltaTime = CalculateMyDeltaTime();
    
    KinematicCharacterSystem.Simulate(
        customDeltaTime,
        KinematicCharacterSystem.CharacterMotors,
        KinematicCharacterSystem.PhysicsMovers
    );
}
```

#### Interpolation System

**Purpose**: Smooth visual movement between physics updates

**Pre-Simulation** (`PreSimulationInterpolationUpdate`):

```csharp
// For each motor/mover:
1. Save current TransientPosition/Rotation to InitialTickPosition/Rotation
2. Set Transform to TransientPosition/Rotation
3. Set Rigidbody to TransientPosition/Rotation
```

**Post-Simulation** (`PostSimulationInterpolationUpdate`):

```csharp
// For each motor/mover:
1. Record start time and delta time
2. Reset Transform to InitialTickPosition/Rotation
3. For PhysicsMovers with MoveWithPhysics:
   - Reset Rigidbody to InitialTickPosition/Rotation
   - Call MovePosition/MoveRotation to TransientPosition/Rotation
4. For PhysicsMovers without MoveWithPhysics:
   - Set Rigidbody directly to TransientPosition/Rotation
```

**Visual Interpolation** (`CustomInterpolationUpdate` in LateUpdate):

```csharp
// Calculate interpolation factor based on time
float interpolationFactor = 
    (Time.time - startTime) / deltaTime;

// For each motor/mover:
Transform.position = Vector3.Lerp(
    InitialTickPosition,
    TransientPosition,
    interpolationFactor
);

Transform.rotation = Quaternion.Slerp(
    InitialTickRotation,
    TransientRotation,
    interpolationFactor
);
```

**Result**: Smooth 60fps+ visual updates even with 50fps physics

---

### PhysicsMover

Component for kinematic rigidbodies that interact with characters (platforms, elevators, doors).

#### Setup

```csharp
[RequireComponent(typeof(Rigidbody))]
public class PhysicsMover : MonoBehaviour
{
    public Rigidbody Rigidbody;              // Auto-configured
    public bool MoveWithPhysics = true;      // Use MovePosition vs direct
    public IMoverController MoverController; // Your movement logic
    
    // Runtime state
    public Vector3 Velocity { get; protected set; }
    public Vector3 AngularVelocity { get; protected set; }
    public Vector3 TransientPosition { get; }
    public Quaternion TransientRotation { get; }
}
```

**Auto-Configuration** (in `ValidateData`):

```csharp
Rigidbody.centerOfMass = Vector3.zero;
Rigidbody.maxAngularVelocity = Mathf.Infinity;
Rigidbody.maxDepenetrationVelocity = Mathf.Infinity;
Rigidbody.isKinematic = true;
Rigidbody.interpolation = RigidbodyInterpolation.None;
```

#### Velocity Calculation

Automatic in `VelocityUpdate`:

```csharp
public void VelocityUpdate(float deltaTime)
{
    // Save current position
    InitialSimulationPosition = TransientPosition;
    InitialSimulationRotation = TransientRotation;
    
    // Call controller for goal position/rotation
    MoverController.UpdateMovement(
        out _internalTransientPosition,
        out _internalTransientRotation,
        deltaTime
    );
    
    // Calculate velocities
    Velocity = (TransientPosition - InitialSimulationPosition) / deltaTime;
    
    // Angular velocity from rotation delta
    Quaternion rotationDelta = 
        TransientRotation * Quaternion.Inverse(InitialSimulationRotation);
    AngularVelocity = 
        (Mathf.Deg2Rad * rotationDelta.eulerAngles) / deltaTime;
}
```

#### Movement Methods

**MoveWithPhysics = true**:
- Uses `Rigidbody.MovePosition()` and `Rigidbody.MoveRotation()`
- Proper physics integration
- Can be blocked by other rigidbodies
- Recommended for most cases

**MoveWithPhysics = false**:
- Sets `Rigidbody.position` and `Rigidbody.rotation` directly
- Ignores physics
- Teleports through obstacles
- Use for deterministic movement

#### State Management

```csharp
PhysicsMoverState GetState()
void ApplyState(PhysicsMoverState state)

public struct PhysicsMoverState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 AngularVelocity;
}
```

#### Character Interaction

Characters automatically:
1. Detect PhysicsMovers through `GetInteractiveRigidbody()`
2. Calculate velocity at character position (linear + angular)
3. Move with platform
4. Preserve momentum on detachment (if `PreserveAttachedRigidbodyMomentum`)

**Velocity at Point Calculation:**

```csharp
void GetVelocityFromRigidbodyMovement(
    Rigidbody interactiveRigidbody, 
    Vector3 atPoint, 
    float deltaTime,
    out Vector3 linearVelocity, 
    out Vector3 angularVelocity)
{
    // Start with rigidbody velocities
    linearVelocity = rigidbody.velocity;
    angularVelocity = rigidbody.angularVelocity;
    
    // For kinematic, get from PhysicsMover
    if (rigidbody.isKinematic)
    {
        PhysicsMover mover = rigidbody.GetComponent<PhysicsMover>();
        if (mover)
        {
            linearVelocity = mover.Velocity;
            angularVelocity = mover.AngularVelocity;
        }
    }
    
    // Add angular velocity effect at point
    if (angularVelocity != Vector3.zero)
    {
        Vector3 centerOfRotation = 
            rigidbody.transform.TransformPoint(rigidbody.centerOfMass);
        Vector3 toPoint = atPoint - centerOfRotation;
        
        Quaternion rotation = 
            Quaternion.Euler(Mathf.Rad2Deg * angularVelocity * deltaTime);
        Vector3 newPoint = centerOfRotation + (rotation * toPoint);
        
        linearVelocity += (newPoint - atPoint) / deltaTime;
    }
}
```

---

## Interfaces

### ICharacterController

Primary interface for character control logic.

```csharp
public interface ICharacterController
{
    // Rotation control
    void UpdateRotation(ref Quaternion currentRotation, float deltaTime);
    
    // Velocity control
    void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
    
    // Lifecycle callbacks
    void BeforeCharacterUpdate(float deltaTime);
    void PostGroundingUpdate(float deltaTime);
    void AfterCharacterUpdate(float deltaTime);
    
    // Collision filtering
    bool IsColliderValidForCollisions(Collider coll);
    
    // Collision callbacks
    void OnGroundHit(Collider hitCollider, Vector3 hitNormal, 
                     Vector3 hitPoint, ref HitStabilityReport hitStabilityReport);
    void OnMovementHit(Collider hitCollider, Vector3 hitNormal, 
                       Vector3 hitPoint, ref HitStabilityReport hitStabilityReport);
    void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, 
                                    Vector3 hitPoint, Vector3 atCharacterPosition, 
                                    Quaternion atCharacterRotation, 
                                    ref HitStabilityReport hitStabilityReport);
    void OnDiscreteCollisionDetected(Collider hitCollider);
}
```

#### Method Details

**UpdateRotation**:
- Called early in UpdatePhase2
- Modify `currentRotation` to desired value
- Use `Motor.CharacterUp/Forward/Right` for current orientation
- Consider using smoothing for responsive feel

Example:
```csharp
public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
{
    if (_lookInputVector.sqrMagnitude > 0f)
    {
        // Smooth turn toward look direction
        Vector3 targetForward = Vector3.Slerp(
            Motor.CharacterForward,
            _lookInputVector,
            1f - Mathf.Exp(-20f * deltaTime)
        ).normalized;
        
        currentRotation = Quaternion.LookRotation(
            targetForward,
            Motor.CharacterUp
        );
    }
}
```

**UpdateVelocity**:
- Called after rotation, before movement
- Modify `currentVelocity` to desired value
- This is `BaseVelocity` - attached rigidbody velocity handled separately
- Handle jump, acceleration, friction here

Example:
```csharp
public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
{
    // Movement
    Vector3 targetVelocity = _moveInput * MaxSpeed;
    currentVelocity = Vector3.Lerp(
        currentVelocity,
        targetVelocity,
        1f - Mathf.Exp(-15f * deltaTime)
    );
    
    // Gravity
    if (!Motor.GroundingStatus.IsStableOnGround)
    {
        currentVelocity += Vector3.down * Gravity * deltaTime;
    }
    
    // Jump
    if (_jumpRequested && Motor.GroundingStatus.IsStableOnGround)
    {
        currentVelocity += Motor.CharacterUp * JumpSpeed;
        Motor.ForceUnground(0.1f);
        _jumpRequested = false;
    }
}
```

**BeforeCharacterUpdate**:
- Called at start of UpdatePhase1
- Good place to:
  - Process input
  - Update state machines
  - Reset per-frame flags

**PostGroundingUpdate**:
- Called after ground probing, before movement
- Good place to:
  - React to ground state changes (just landed, just left ground)
  - Adjust velocity based on grounding
  - Handle landing impacts

Example:
```csharp
public void PostGroundingUpdate(float deltaTime)
{
    // Just landed
    if (!Motor.LastGroundingStatus.IsStableOnGround && 
        Motor.GroundingStatus.IsStableOnGround)
    {
        OnLanded();
    }
    
    // Just left ground
    if (Motor.LastGroundingStatus.IsStableOnGround && 
        !Motor.GroundingStatus.IsStableOnGround)
    {
        OnLeftGround();
    }
}
```

**AfterCharacterUpdate**:
- Called at end of UpdatePhase2
- Good place to:
  - Update animations based on final state
  - Handle effects (footsteps, particles)
  - Update gameplay state

**IsColliderValidForCollisions**:
- Return false to ignore specific colliders
- Called for every potential collision
- Common uses:
  - Ignore triggers: `return !coll.isTrigger`
  - Ignore specific layers
  - Ignore tagged objects
  - Per-collider collision rules

Example:
```csharp
public bool IsColliderValidForCollisions(Collider coll)
{
    // Ignore triggers
    if (coll.isTrigger) return false;
    
    // Ignore certain tags
    if (coll.CompareTag("NoCharacterCollision")) return false;
    
    // Ignore projectiles
    if (coll.GetComponent<Projectile>()) return false;
    
    return true;
}
```

**OnGroundHit**:
- Called when ground probe detects surface
- Access `hitStabilityReport` for detailed info
- Can modify stability report if needed
- Use for:
  - Different movement on different surfaces
  - Sound/particle effects
  - Surface-specific gameplay

**OnMovementHit**:
- Called when movement sweep hits something
- Not called for ground (use OnGroundHit)
- Use for:
  - Wall run detection
  - Wall impact effects
  - Obstacle interaction

**ProcessHitStabilityReport**:
- Called after stability evaluation
- Can modify `hitStabilityReport.IsStable` to override
- Advanced use cases:
  - Custom stability rules
  - Gameplay-specific ground rules
  - Special surface handling

Example:
```csharp
public void ProcessHitStabilityReport(
    Collider hitCollider,
    Vector3 hitNormal, 
    Vector3 hitPoint,
    Vector3 atCharacterPosition,
    Quaternion atCharacterRotation,
    ref HitStabilityReport hitStabilityReport)
{
    // Make ice unstable unless moving slowly
    if (hitCollider.CompareTag("Ice"))
    {
        if (Motor.BaseVelocity.magnitude > 2f)
        {
            hitStabilityReport.IsStable = false;
        }
    }
}
```

**OnDiscreteCollisionDetected**:
- Only called if `DiscreteCollisionEvents = true`
- Detects overlaps not from movement (e.g., pushed into)
- Expensive - use sparingly
- Use for:
  - Damage from crushing
  - Special trigger detection

---

### IMoverController

Simple interface for physics mover control.

```csharp
public interface IMoverController
{
    void UpdateMovement(out Vector3 goalPosition, 
                       out Quaternion goalRotation, 
                       float deltaTime);
}
```

**Implementation Example:**

```csharp
public class RotatingPlatform : MonoBehaviour, IMoverController
{
    public PhysicsMover Mover;
    public float RotationSpeed = 90f; // degrees per second
    
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private float _currentAngle;
    
    void Start()
    {
        Mover.MoverController = this;
        _initialPosition = Mover.TransientPosition;
        _initialRotation = Mover.TransientRotation;
    }
    
    public void UpdateMovement(out Vector3 goalPosition, 
                              out Quaternion goalRotation, 
                              float deltaTime)
    {
        // Update angle
        _currentAngle += RotationSpeed * deltaTime;
        
        // Position doesn't change
        goalPosition = _initialPosition;
        
        // Rotate around up axis
        goalRotation = _initialRotation * 
                      Quaternion.Euler(0, _currentAngle, 0);
    }
}
```

---

## Data Structures

### State Structs

**KinematicCharacterMotorState**:
Complete motor state for save/load:

```csharp
[Serializable]
public struct KinematicCharacterMotorState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 BaseVelocity;
    public bool MustUnground;
    public float MustUngroundTime;
    public bool LastMovementIterationFoundAnyGround;
    public CharacterTransientGroundingReport GroundingStatus;
    public Rigidbody AttachedRigidbody;
    public Vector3 AttachedRigidbodyVelocity;
}
```

**PhysicsMoverState**:
Complete mover state:

```csharp
[Serializable]
public struct PhysicsMoverState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 AngularVelocity;
}
```

### Collision Results

**OverlapResult**:
```csharp
public struct OverlapResult
{
    public Vector3 Normal;        // Resolution direction
    public Collider Collider;     // Overlapping collider
}
```

**RigidbodyProjectionHit**:
Internal structure for processing rigidbody hits:

```csharp
public struct RigidbodyProjectionHit
{
    public Rigidbody Rigidbody;
    public Vector3 HitPoint;
    public Vector3 EffectiveHitNormal;    // Obstruction normal
    public Vector3 HitVelocity;           // Character velocity at hit
    public bool StableOnHit;
}
```

### Grounding Reports

**CharacterGroundingReport**:
Full grounding information including collider refs:

```csharp
public struct CharacterGroundingReport
{
    public bool FoundAnyGround;
    public bool IsStableOnGround;
    public bool SnappingPrevented;
    public Vector3 GroundNormal;
    public Vector3 InnerGroundNormal;
    public Vector3 OuterGroundNormal;
    public Collider GroundCollider;   // Can persist between frames
    public Vector3 GroundPoint;
}
```

**CharacterTransientGroundingReport**:
Simulation-safe grounding info (no Unity object refs):

```csharp
public struct CharacterTransientGroundingReport
{
    public bool FoundAnyGround;
    public bool IsStableOnGround;
    public bool SnappingPrevented;
    public Vector3 GroundNormal;
    public Vector3 InnerGroundNormal;
    public Vector3 OuterGroundNormal;
    // No Collider reference - safe for state saving
}
```

### Hit Stability Report

Complete stability and geometry information:

```csharp
public struct HitStabilityReport
{
    // Basic stability
    public bool IsStable;
    
    // Ground normals
    public bool FoundInnerNormal;
    public Vector3 InnerNormal;
    public bool FoundOuterNormal;
    public Vector3 OuterNormal;
    
    // Step detection
    public bool ValidStepDetected;
    public Collider SteppedCollider;
    
    // Ledge detection
    public bool LedgeDetected;
    public bool IsOnEmptySideOfLedge;
    public float DistanceFromLedge;
    public bool IsMovingTowardsEmptySideOfLedge;
    public Vector3 LedgeGroundNormal;
    public Vector3 LedgeRightDirection;
    public Vector3 LedgeFacingDirection;
}
```

---

## Configuration

### KCCSettings

ScriptableObject for global system configuration:

```csharp
[CreateAssetMenu]
public class KCCSettings : ScriptableObject
{
    [Tooltip("Automatic simulation in FixedUpdate")]
    public bool AutoSimulation = true;
    
    [Tooltip("Enable interpolation for smooth visuals")]
    public bool Interpolate = true;
    
    [Tooltip("Initial motor list capacity (prevents allocations)")]
    public int MotorsListInitialCapacity = 100;
    
    [Tooltip("Initial mover list capacity (prevents allocations)")]
    public int MoversListInitialCapacity = 100;
}
```

**Usage:**

```csharp
// Access at runtime
KCCSettings settings = KinematicCharacterSystem.Settings;
settings.AutoSimulation = false; // Switch to manual control

// Create custom settings asset
// 1. Right-click in Project
// 2. Create > KCCSettings
// 3. Assign to system if needed
```

### Recommended Settings by Use Case

**Standard Third-Person**:
```
AutoSimulation: true
Interpolate: true
MaxMovementIterations: 5
MaxDecollisionIterations: 1
StepHandling: Standard
MaxStepHeight: 0.5
LedgeAndDenivelationHandling: true
InteractiveRigidbodyHandling: true
RigidbodyInteractionType: SimulatedDynamic
```

**First-Person Shooter**:
```
AutoSimulation: true
Interpolate: true
MaxMovementIterations: 3
StepHandling: Standard
MaxStepHeight: 0.35
LedgeAndDenivelationHandling: false
InteractiveRigidbodyHandling: true
RigidbodyInteractionType: Kinematic
CheckMovementInitialOverlaps: true
```

**Platformer**:
```
AutoSimulation: true
Interpolate: true
MaxMovementIterations: 5
StepHandling: Standard or Extra
MaxStepHeight: 0.3
LedgeAndDenivelationHandling: true
MaxStableDistanceFromLedge: 0.4
InteractiveRigidbodyHandling: true
PreserveAttachedRigidbodyMomentum: true
```

**2.5D / Side-Scroller**:
```
AutoSimulation: true
Interpolate: true
HasPlanarConstraint: true
PlanarConstraintAxis: (0, 0, 1) or (1, 0, 0)
StepHandling: Standard
InteractiveRigidbodyHandling: true
```

---

## Update Pipeline

### Detailed Update Sequence

**FixedUpdate** (Physics timestep):

```
1. PreSimulationInterpolationUpdate (if interpolation enabled)
   └─ For each motor and mover:
      ├─ InitialTickPosition = TransientPosition
      ├─ InitialTickRotation = TransientRotation
      ├─ Transform.position = TransientPosition
      └─ Transform.rotation = TransientRotation

2. PhysicsMover VelocityUpdate
   └─ For each mover:
      ├─ Save InitialSimulationPosition/Rotation
      ├─ Call IMoverController.UpdateMovement()
      └─ Calculate Velocity and AngularVelocity from delta

3. Character UpdatePhase1
   └─ For each motor:
      ├─ NaN safety checks
      ├─ Clear per-update state
      ├─ Call ICharacterController.BeforeCharacterUpdate()
      ├─ Handle MovePosition if queued
      ├─ Resolve initial overlaps (up to MaxDecollisionIterations)
      ├─ Ground probing and snapping
      ├─ Call ICharacterController.PostGroundingUpdate()
      └─ Handle attached rigidbody
         ├─ Detect from ground
         ├─ Calculate velocities
         ├─ Preserve momentum if detaching
         └─ Move with attached rigidbody

4. PhysicsMover Transform Update
   └─ For each mover:
      ├─ Transform.position = TransientPosition
      ├─ Transform.rotation = TransientRotation
      ├─ Rigidbody.position = TransientPosition
      └─ Rigidbody.rotation = TransientRotation

5. Character UpdatePhase2
   └─ For each motor:
      ├─ Call ICharacterController.UpdateRotation()
      ├─ Handle MoveRotation if queued
      ├─ Resolve attached rigidbody overlaps
      ├─ Resolve rotation-caused overlaps
      ├─ Call ICharacterController.UpdateVelocity()
      ├─ Process movement with collision solving
      ├─ Handle rigidbody interactions
      ├─ Apply planar constraint
      ├─ Discrete collision detection (if enabled)
      ├─ Call ICharacterController.AfterCharacterUpdate()
      └─ Transform.position = TransientPosition

6. PostSimulationInterpolationUpdate (if interpolation enabled)
   └─ Record interpolation start time
   └─ For each motor:
      └─ Transform.position = InitialTickPosition
      └─ Transform.rotation = InitialTickRotation
   └─ For each mover:
      ├─ If MoveWithPhysics:
      │  ├─ Rigidbody.position = InitialTickPosition
      │  ├─ Rigidbody.rotation = InitialTickRotation
      │  ├─ Rigidbody.MovePosition(TransientPosition)
      │  └─ Rigidbody.MoveRotation(TransientRotation)
      └─ Else:
         ├─ Rigidbody.position = TransientPosition
         └─ Rigidbody.rotation = TransientRotation
```

**LateUpdate** (Per visual frame):

```
CustomInterpolationUpdate (if interpolation enabled)
└─ Calculate interpolation factor from time
└─ For each motor:
   ├─ Transform.position = Lerp(InitialTickPosition, TransientPosition, t)
   └─ Transform.rotation = Slerp(InitialTickRotation, TransientRotation, t)
└─ For each mover:
   ├─ Transform.position = Lerp(InitialTickPosition, TransientPosition, t)
   ├─ Transform.rotation = Slerp(InitialTickRotation, TransientRotation, t)
   ├─ PositionDeltaFromInterpolation = position - LatestInterpolationPosition
   ├─ RotationDeltaFromInterpolation = rotation * Inverse(LatestInterpolationRotation)
   └─ Update Latest values
```

---

## Advanced Features

### Custom Velocity Projection

Override how velocity responds to obstacles:

```csharp
public class CustomCharacterMotor : KinematicCharacterMotor
{
    public override void HandleVelocityProjection(
        ref Vector3 velocity,
        Vector3 obstructionNormal,
        bool stableOnHit)
    {
        // Custom behavior - e.g., wall running
        if (!stableOnHit && IsWallRunSurface(obstructionNormal))
        {
            // Project along wall instead of blocking
            Vector3 wallRight = Vector3.Cross(obstructionNormal, CharacterUp);
            velocity = Vector3.Project(velocity, wallRight);
            return;
        }
        
        // Default behavior for other cases
        base.HandleVelocityProjection(ref velocity, obstructionNormal, stableOnHit);
    }
}
```

### Custom Rigidbody Interaction

Override rigidbody push behavior:

```csharp
public class CustomCharacterMotor : KinematicCharacterMotor
{
    public override void HandleSimulatedRigidbodyInteraction(
        ref Vector3 processedVelocity,
        RigidbodyProjectionHit hit,
        float deltaTime)
    {
        // Custom physics - e.g., special object handling
        if (hit.Rigidbody.CompareTag("HeavyObject"))
        {
            // Slow down when pushing heavy objects
            float pushResistance = 0.5f;
            Vector3 pushDirection = Vector3.Project(
                processedVelocity,
                hit.EffectiveHitNormal
            );
            processedVelocity -= pushDirection * pushResistance;
        }
        
        base.HandleSimulatedRigidbodyInteraction(ref processedVelocity, hit, deltaTime);
    }
}
```

### State Synchronization

For networking or save systems:

```csharp
public class CharacterStateSync : MonoBehaviour
{
    public KinematicCharacterMotor Motor;
    
    // Save state
    public byte[] SerializeState()
    {
        KinematicCharacterMotorState state = Motor.GetState();
        // Serialize to bytes (use your serialization method)
        return SerializeToBytes(state);
    }
    
    // Restore state
    public void DeserializeState(byte[] data)
    {
        KinematicCharacterMotorState state = DeserializeFromBytes(data);
        Motor.ApplyState(state, bypassInterpolation: true);
    }
    
    // Networking example
    [ClientRpc]
    void RpcSyncState(byte[] stateData)
    {
        if (!isLocalPlayer)
        {
            DeserializeState(stateData);
        }
    }
}
```

### Advanced Ground Detection

Custom ground validation:

```csharp
public class CustomGroundController : MonoBehaviour, ICharacterController
{
    public void ProcessHitStabilityReport(
        Collider hitCollider,
        Vector3 hitNormal,
        Vector3 hitPoint,
        Vector3 atCharacterPosition,
        Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport)
    {
        // Example: Only stable on specific materials
        var terrainMaterial = hitCollider.GetComponent<TerrainMaterial>();
        if (terrainMaterial != null)
        {
            switch (terrainMaterial.Type)
            {
                case TerrainType.Ice:
                    // Ice is only stable when moving slowly
                    if (Motor.Velocity.magnitude > 5f)
                        hitStabilityReport.IsStable = false;
                    break;
                    
                case TerrainType.Mud:
                    // Mud slows you down but is stable
                    Motor.BaseVelocity *= 0.5f;
                    break;
                    
                case TerrainType.Bouncy:
                    // Bouncy surfaces are never stable
                    hitStabilityReport.IsStable = false;
                    break;
            }
        }
    }
}
```

### Swimming/Water Volumes

```csharp
public class SwimmingController : MonoBehaviour, ICharacterController
{
    private bool _isInWater;
    private float _waterSurfaceHeight;
    
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_isInWater)
        {
            // Apply water physics
            float waterDrag = 2f;
            currentVelocity *= (1f - waterDrag * deltaTime);
            
            // Buoyancy
            if (Motor.Transform.position.y < _waterSurfaceHeight)
            {
                currentVelocity += Vector3.up * 5f * deltaTime;
            }
            
            // Swimming controls
            Vector3 swimInput = GetSwimInput();
            currentVelocity += swimInput * 4f * deltaTime;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            _isInWater = true;
            _waterSurfaceHeight = other.bounds.max.y;
            Motor.SetGroundSolvingActivation(false); // Disable grounding
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            _isInWater = false;
            Motor.SetGroundSolvingActivation(true); // Re-enable grounding
        }
    }
}
```

---

## Usage Guide

### Basic Character Setup

**Step 1: Create Character GameObject**

```
GameObject
├─ KinematicCharacterMotor
├─ CapsuleCollider (auto-created)
└─ MyCharacterController (your script)
```

**Step 2: Implement ICharacterController**

```csharp
using UnityEngine;
using KinematicCharacterController;

public class MyCharacterController : MonoBehaviour, ICharacterController
{
    [Header("References")]
    public KinematicCharacterMotor Motor;
    
    [Header("Movement")]
    public float MaxSpeed = 10f;
    public float Acceleration = 50f;
    public float JumpSpeed = 10f;
    public float Gravity = 30f;
    
    private Vector3 _moveInput;
    private bool _jumpRequested;
    
    void Start()
    {
        Motor.CharacterController = this;
    }
    
    void Update()
    {
        // Handle input
        _moveInput = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;
        
        if (Input.GetButtonDown("Jump"))
        {
            _jumpRequested = true;
        }
    }
    
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // Rotate to face movement direction
        if (_moveInput.sqrMagnitude > 0f)
        {
            Vector3 targetForward = _moveInput;
            currentRotation = Quaternion.LookRotation(targetForward, Motor.CharacterUp);
        }
    }
    
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // Horizontal movement
        Vector3 targetVelocity = _moveInput * MaxSpeed;
        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            Acceleration * deltaTime
        );
        
        // Gravity
        if (!Motor.GroundingStatus.IsStableOnGround)
        {
            currentVelocity += Vector3.down * Gravity * deltaTime;
        }
        
        // Jump
        if (_jumpRequested && Motor.GroundingStatus.IsStableOnGround)
        {
            currentVelocity += Motor.CharacterUp * JumpSpeed;
            Motor.ForceUnground(0.1f);
            _jumpRequested = false;
        }
    }
    
    // Required interface methods (can be empty for simple cases)
    public void BeforeCharacterUpdate(float deltaTime) { }
    public void PostGroundingUpdate(float deltaTime) { }
    public void AfterCharacterUpdate(float deltaTime) { }
    
    public bool IsColliderValidForCollisions(Collider coll)
    {
        return !coll.isTrigger;
    }
    
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, 
                           Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, 
                             Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, 
                                         Vector3 hitPoint, Vector3 atCharacterPosition, 
                                         Quaternion atCharacterRotation, 
                                         ref HitStabilityReport hitStabilityReport) { }
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}
```

**Step 3: Configure Motor in Inspector**

```
Capsule Settings:
  Radius: 0.5
  Height: 2.0
  Y Offset: 1.0

Grounding Settings:
  Max Stable Slope Angle: 60
  Stable Ground Layers: Default, Ground

Step Settings:
  Step Handling: Standard
  Max Step Height: 0.5

Rigidbody Interaction:
  Interactive Rigidbody Handling: ✓
  Rigidbody Interaction Type: Simulated Dynamic
  Simulated Character Mass: 70
```

### Creating a Moving Platform

```csharp
using UnityEngine;
using KinematicCharacterController;

public class MovingPlatform : MonoBehaviour, IMoverController
{
    public PhysicsMover Mover;
    
    [Header("Movement")]
    public Transform[] Waypoints;
    public float Speed = 2f;
    public bool Loop = true;
    
    private int _currentWaypoint = 0;
    private Vector3 _currentPosition;
    
    void Start()
    {
        Mover.MoverController = this;
        _currentPosition = transform.position;
    }
    
    public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
    {
        if (Waypoints.Length == 0)
        {
            goalPosition = _currentPosition;
            goalRotation = transform.rotation;
            return;
        }
        
        // Move toward current waypoint
        Vector3 target = Waypoints[_currentWaypoint].position;
        _currentPosition = Vector3.MoveTowards(
            _currentPosition,
            target,
            Speed * deltaTime
        );
        
        // Check if reached waypoint
        if (Vector3.Distance(_currentPosition, target) < 0.01f)
        {
            _currentWaypoint++;
            
            if (_currentWaypoint >= Waypoints.Length)
            {
                if (Loop)
                    _currentWaypoint = 0;
                else
                    _currentWaypoint = Waypoints.Length - 1;
            }
        }
        
        goalPosition = _currentPosition;
        goalRotation = transform.rotation;
    }
}
```

### Common Patterns

**Smooth Camera Follow**:

```csharp
public class SmoothCamera : MonoBehaviour
{
    public Transform Target;
    public float Smoothing = 10f;
    public Vector3 Offset = new Vector3(0, 5, -10);
    
    void LateUpdate()
    {
        Vector3 targetPosition = Target.position + Offset;
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            1f - Mathf.Exp(-Smoothing * Time.deltaTime)
        );
        
        transform.LookAt(Target);
    }
}
```

**Sprint/Crouch**:

```csharp
public class PlayerController : MonoBehaviour, ICharacterController
{
    private bool _isCrouching;
    private bool _isSprinting;
    
    public float NormalHeight = 2f;
    public float CrouchHeight = 1f;
    public float NormalSpeed = 5f;
    public float SprintSpeed = 10f;
    
    void Update()
    {
        // Crouch
        if (Input.GetKeyDown(KeyCode.C))
        {
            _isCrouching = !_isCrouching;
            float targetHeight = _isCrouching ? CrouchHeight : NormalHeight;
            Motor.SetCapsuleDimensions(
                Motor.Capsule.radius,
                targetHeight,
                targetHeight * 0.5f
            );
        }
        
        // Sprint
        _isSprinting = Input.GetKey(KeyCode.LeftShift);
    }
    
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        float currentMaxSpeed = _isSprinting ? SprintSpeed : NormalSpeed;
        if (_isCrouching) currentMaxSpeed *= 0.5f;
        
        Vector3 targetVelocity = _moveInput * currentMaxSpeed;
        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            Acceleration * deltaTime
        );
    }
}
```

**Dash Ability**:

```csharp
public class DashAbility : MonoBehaviour
{
    public KinematicCharacterMotor Motor;
    public float DashSpeed = 20f;
    public float DashDuration = 0.2f;
    
    private float _dashTimeRemaining;
    private Vector3 _dashDirection;
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && _dashTimeRemaining <= 0f)
        {
            _dashDirection = GetDashDirection();
            _dashTimeRemaining = DashDuration;
            Motor.ForceUnground(DashDuration);
        }
        
        if (_dashTimeRemaining > 0f)
        {
            _dashTimeRemaining -= Time.deltaTime;
        }
    }
    
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_dashTimeRemaining > 0f)
        {
            // Override velocity with dash
            currentVelocity = _dashDirection * DashSpeed;
        }
        else
        {
            // Normal movement
            // ...
        }
    }
}
```

---

## Best Practices

### Performance Optimization

1. **Preallocate List Capacity**:
```csharp
void Awake()
{
    KinematicCharacterSystem.SetCharacterMotorsCapacity(50);
    KinematicCharacterSystem.SetPhysicsMoversCapacity(20);
}
```

2. **Disable Unused Features**:
```csharp
// If no steps in level
motor.StepHandling = StepHandlingMethod.None;

// If no ledges
motor.LedgeAndDenivelationHandling = false;

// If no rigidbody interaction
motor.InteractiveRigidbodyHandling = false;
```

3. **Reduce Iterations for Simple Geometry**:
```csharp
motor.MaxMovementIterations = 3; // Instead of 5
motor.MaxDecollisionIterations = 1; // Keep at 1 usually
```

4. **Layer Masking**:
```csharp
// Only check ground layers for stability
motor.StableGroundLayers = LayerMask.GetMask("Ground", "Platform");

// Build efficient collision layers in Physics Settings
```

5. **Avoid Expensive Callbacks**:
```csharp
// Don't enable unless needed
motor.DiscreteCollisionEvents = false;
```

### Debugging

**Visualize Grounding**:

```csharp
void OnDrawGizmos()
{
    if (!Motor) return;
    
    // Current position
    Gizmos.color = Color.blue;
    Gizmos.DrawWireSphere(Motor.TransientPosition, 0.1f);
    
    // Ground status
    if (Motor.GroundingStatus.FoundAnyGround)
    {
        Gizmos.color = Motor.GroundingStatus.IsStableOnGround 
            ? Color.green 
            : Color.yellow;
        
        Gizmos.DrawLine(
            Motor.GroundingStatus.GroundPoint,
            Motor.GroundingStatus.GroundPoint + Motor.GroundingStatus.GroundNormal
        );
    }
    
    // Velocity
    Gizmos.color = Color.red;
    Gizmos.DrawRay(Motor.TransientPosition, Motor.Velocity);
}
```

**Log State Changes**:

```csharp
public void PostGroundingUpdate(float deltaTime)
{
    bool wasGrounded = Motor.LastGroundingStatus.IsStableOnGround;
    bool isGrounded = Motor.GroundingStatus.IsStableOnGround;
    
    if (!wasGrounded && isGrounded)
    {
        Debug.Log($"Landed at {Time.time}");
    }
    else if (wasGrounded && !isGrounded)
    {
        Debug.Log($"Left ground at {Time.time}");
    }
}
```

### Common Pitfalls

1. **Scale Issues**:
```
❌ DON'T: Use non-(1,1,1) scale on character or parents
✓ DO: Keep all scales at (1,1,1), adjust capsule size instead
```

2. **Direct Position Modification**:
```csharp
// ❌ DON'T
transform.position = newPosition;

// ✓ DO
Motor.SetPosition(newPosition);
// or
Motor.MoveCharacter(newPosition);
```

3. **Forgetting ForceUnground**:
```csharp
// ❌ DON'T - Jump might get canceled by ground snapping
currentVelocity += Vector3.up * jumpSpeed;

// ✓ DO - Prevent ground snapping after jump
currentVelocity += Vector3.up * jumpSpeed;
Motor.ForceUnground(0.1f);
```

4. **Ignoring deltaTime**:
```csharp
// ❌ DON'T
currentVelocity += acceleration;

// ✓ DO
currentVelocity += acceleration * deltaTime;
```

5. **Modifying Wrong Properties**:
```csharp
// ❌ DON'T - This bypasses the motor
Motor.Rigidbody.velocity = newVelocity;

// ✓ DO - Modify through callbacks
public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
{
    currentVelocity = newVelocity;
}
```

---

## Troubleshooting

### Character Falls Through Ground

**Symptoms**: Character isn't stable on slopes or falls through floor

**Solutions**:
1. Check `MaxStableSlopeAngle` - increase if needed
2. Verify `StableGroundLayers` includes ground layer
3. Ensure ground colliders aren't triggers
4. Check Physics collision matrix
5. Increase `GroundDetectionExtraDistance` for fast movement

### Jittery Movement

**Symptoms**: Character stutters or vibrates

**Solutions**:
1. Enable interpolation: `KinematicCharacterSystem.Settings.Interpolate = true`
2. Reduce `MaxMovementIterations` if overshooting
3. Check for conflicting movement code (e.g., multiple velocity sources)
4. Verify deltaTime is being used correctly
5. Check for NaN values in velocity calculations

### Can't Climb Steps

**Symptoms**: Character stops at steps that should be climbable

**Solutions**:
1. Set `StepHandling` to `Standard` or `Extra`
2. Increase `MaxStepHeight`
3. For narrow steps, use `Extra` method and adjust `MinRequiredStepDepth`
4. Ensure steps are on `StableGroundLayers`
5. Check step geometry isn't too complex

### Sliding on Slopes

**Symptoms**: Character slides down slopes when stationary

**Solutions**:
1. Verify `IsStableOnNormal()` logic
2. Check `MaxStableSlopeAngle` setting
3. Implement friction in `UpdateVelocity`:
```csharp
if (Motor.GroundingStatus.IsStableOnGround)
{
    // Apply friction
    currentVelocity *= (1f - 5f * deltaTime);
}
```

### Platform Detachment Issues

**Symptoms**: Character doesn't move with platform or detaches unexpectedly

**Solutions**:
1. Ensure platform has `PhysicsMover` component
2. Check `InteractiveRigidbodyHandling = true`
3. Verify platform is on collidable layers
4. For custom attachment, use `AttachedRigidbodyOverride`
5. Check `PreserveAttachedRigidbodyMomentum` setting

### Stuck in Geometry

**Symptoms**: Character gets stuck in walls or overlapping geometry

**Solutions**:
1. Increase `MaxDecollisionIterations` (default is 1)
2. Enable `CheckMovementInitialOverlaps = true`
3. Reduce `CollisionOffset` if too large (advanced)
4. Check for invalid geometry (thin walls, etc.)
5. Implement overlap debugging:
```csharp
if (Motor.OverlapsCount > 0)
{
    Debug.LogWarning($"Overlapping {Motor.OverlapsCount} colliders");
    for (int i = 0; i < Motor.OverlapsCount; i++)
    {
        Debug.Log($"  - {Motor.Overlaps[i].Collider.name}");
    }
}
```

### Rigidbody Interaction Issues

**Symptoms**: Character doesn't push objects or pushes too hard/soft

**Solutions**:
1. Check `RigidbodyInteractionType` setting:
   - `Kinematic`: Infinite force (good for FPS)
   - `SimulatedDynamic`: Mass-based (good for physics puzzles)
2. Adjust `SimulatedCharacterMass`
3. Ensure rigidbodies aren't kinematic
4. Check rigidbody mass values are reasonable
5. Implement custom interaction if needed:
```csharp
public override void HandleSimulatedRigidbodyInteraction(
    ref Vector3 processedVelocity,
    RigidbodyProjectionHit hit,
    float deltaTime)
{
    // Custom push logic
}
```

### Performance Issues

**Symptoms**: Low framerate or hitching

**Solutions**:
1. Reduce `MaxMovementIterations`
2. Disable `CheckMovementInitialOverlaps`
3. Use `Standard` instead of `Extra` step handling
4. Disable `LedgeAndDenivelationHandling` if not needed
5. Optimize layer collision matrix
6. Profile with Unity Profiler to identify bottleneck:
   - `KinematicCharacterMotor.UpdatePhase1`
   - `KinematicCharacterMotor.UpdatePhase2`
   - Collision detection methods

---

## Summary

The Kinematic Character Controller provides a robust, production-ready solution for character movement in Unity. Key takeaways:

- **Stable and Reliable**: Handles complex scenarios like slopes, steps, and moving platforms
- **Flexible**: Interface-based design allows complete control over behavior
- **Performant**: Optimized collision detection with configurable iteration limits
- **Feature-Rich**: Ledge detection, rigidbody interaction, interpolation, and more
- **Well-Structured**: Clean separation between motor (physics) and controller (gameplay)

The system is designed to "just work" with sensible defaults while providing deep customization for advanced use cases. Start with the basic setup, then add features as needed.

For additional support, refer to the source code comments and the included example implementations.