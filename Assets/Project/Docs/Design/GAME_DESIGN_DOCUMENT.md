# 🌌 Antigravity: Game Design Document

> **Status**: Living Document
> **Version**: 0.1 (Draft)

## 🎯 High Concept

**Antigravity** is a third-person action game that blends fluid, acrobatic movement with visceral, time-bending combat. Players control a character who can manipulate time to undo mistakes, extend combos, and traverse impossible environments.

---

## 🏛️ Core Pillars

### 1. ⏳ Chrono-Mastery (Time Manipulation)

- **Rewind**: The ability to reverse time for the player and objects. Used to correct traversal errors or dodge fatal attacks.
- **Stasis (Planned)**: Freezing objects in time to use as platforms or cover.
- **Acceleration (Planned)**: Speeding up local time for rapid attacks.

### 2. 🦇 Freeflow Combat (Arkham/Spider-Man Style)

- **Rhythm-Based**: Attacks are timed. Button mashing is discouraged; precision is rewarded.
- **Magnetism**: The character automatically slides/snaps to targets within range, making combat feel snappy and cinematic.
- **Combos**: Mixing light, heavy, and gadget attacks to build a "Flow Meter".
- **Crowd Control**: Managing large groups of enemies using agility and time powers.

### 3. 🏃 Fluid Traversal

- **Parkour**: Seamlessly vaulting over obstacles, wall-running, and climbing.
- **Momentum**: Speed is key. Maintaining momentum allows for longer jumps and harder hits.

---

## 🎮 Gameplay Systems

### Player Controller

- **Engine**: Kinematic Character Controller (KCC).
- **State Machine**: Hierarchical State Machine (HSM) managing states like `Idle`, `Run`, `Air`, `WallRun`, `Attack`.
- **Input**: Custom Event-Driven Input System (supports buffering and context).

### Combat System (In Development)

- **Input Buffering**: "Queuing" the next attack slightly before the current animation ends for smoothness.
- **Target Selection**: "Soft Lock" system that prioritizes enemies based on stick direction and distance.
- **Animation Cancellation**: Using Dodge or Time powers to cancel attack recovery frames.

---

## 🗺️ World & Level Design

- **Verticality**: Levels are designed with high verticality to encourage wall-running and aerial combat.
- **Time Puzzles**: Environmental puzzles that require rewinding falling debris to create paths.

---

## 📝 Roadmap & Todo

### Phase 1: Foundation (Current)

- [x] Robust Input System
- [x] Basic KCC Movement
- [x] Camera Control (Cinemachine 3)
- [x] Time Rewind Mechanic

### Phase 2: The Core Loop (Next)

- [ ] **HSM Implementation**: Refactoring controller into strict states.
- [ ] **Combat Prototype**: Basic 3-hit combo with target snapping.
- [ ] **Dummy Enemies**: Static targets to test magnetism.

### Phase 3: Polish

- [ ] Visual Effects (Time distortion, impact sparks).
- [ ] Dynamic Audio.
