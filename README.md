# Project V

> A Unity third-person action game built with scalable architecture principles.

## 🎮 Features

- **🔧 Portable Input System**: Command Pattern-based input handling with combo support
- **🏃 Modular Movement System**: Strategy Pattern with pluggable abilities
  - Jump Handler: Buffering, coyote time, double/triple jump
  - Slide Handler: Surface-aware sliding physics
  - Dash Handler: Charge-based dash system
  - **Mantle Handler**: Parkour ledge grab & climbing (NEW!)
- **🧗 Parkour System**: Raycast-based ledge detection with arc motion
- **📹 Cinemachine Integration**: Custom orbital camera with smooth mouse/gamepad input
- **⏪ Time Rewind Mechanic**: (In progress)
- **🎨 Hierarchical State Machine**: For animation and state tracking

## 📂 Project Structure

```
Assets/Project/
├── Scripts/
│   ├── Controllers/          # Player controller, input handler, config
│   ├── Input System Scripts/ # Reusable input framework
│   ├── Movement/             # Movement modules & handlers
│   ├── Character/States/     # HSM state machine
│   └── Time/                 # Time rewind system
├── Docs/
│   ├── Movement/             # Movement System Guide (updated Dec 2024)
│   ├── Input/                # Input System Guide
│   ├── Design/               # Game design docs
│   └── Development/          # Code standards, KCC reference
└── ScriptableObjects/        # Config files (PlayerMovementConfig)
```

## 🛠️ Tech Stack

- **Unity 6000.0.28f1**
- **Kinematic Character Controller** (KCC)
- **Unity Input System** (New)
- **Cinemachine 3**

## 📝 Code Standards

We follow strict C# conventions with automated formatting via **CSharpier**:

```bash
# Format all scripts
dotnet csharpier "Assets/Project/Scripts"
```

See [CODING_STANDARDS.md](Assets/Project/Docs/Development/CODING_STANDARDS.md) for details.

## 🚀 Getting Started

1. Clone the repo
2. Open in Unity 6000.0.28f1+
3. Install dependencies (KCC, Cinemachine 3)
4. Open `MainScene`
5. Press Play!

## 🎯 Roadmap

**Completed**:

- [x] Movement System refactor (Handler Pattern)
- [x] Mantle/Ledge Grab system
- [x] Slide mechanics
- [x] Charge-based dash

**Next Up**:

- [ ] Shimmying (left/right while hanging)
- [ ] Wall climbing (Assassin's Creed style)
- [ ] Wall run module
- [ ] Combat system
- [ ] Time rewind polish

## 📖 Documentation

Full documentation available in [`Assets/Project/Docs/`](Assets/Project/Docs/):

- **Movement System**: [MOVEMENT_SYSTEM_GUIDE.md](Assets/Project/Docs/Movement/MOVEMENT_SYSTEM_GUIDE.md) - Modular architecture, handlers, mantle system
- **Input System**: [INPUT_SYSTEM_GUIDE.md](Assets/Project/Docs/Input/INPUT_SYSTEM_GUIDE.md) - Command pattern, combos, device handling
- **Code Standards**: [CODING_STANDARDS.md](Assets/Project/Docs/Development/CODING_STANDARDS.md) - C# conventions, formatting
- **KCC Reference**: [KCCDocumentation2025.md](Assets/Project/Docs/Development/KCCDocumentation2025.md) - Character controller API

---

Built with ❤️ by Anmol Verma
