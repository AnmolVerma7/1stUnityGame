# Project V

> A Unity third-person action game built with scalable architecture principles.

## 🎮 Features

- **🔧 Portable Input System**: Command Pattern-based input handling with combo support
- **🏃 Advanced Movement**: Jump buffering, coyote time, double jump, wall jump
- **📹 Cinemachine Integration**: Custom orbital camera with smooth mouse/gamepad input
- **⏪ Time Rewind Mechanic**: (In progress)
- **🎨 Hierarchical State Machine**: For animation and state tracking

## 📂 Project Structure

```
Assets/Project/
├── Scripts/
│   ├── Controllers/        # Player controller, input handler
│   ├── Input System Scripts/  # Reusable input framework
│   ├── Character/States/    # HSM state machine
│   └── Time/               # Time rewind system
├── Docs/
│   ├── CODING_STANDARDS.md
│   ├── INPUT_SYSTEM_API.md
│   └── SCALABLE_ARCHITECTURE.md
└── ScriptableObjects/      # Config files (PlayerMovementConfig)
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

See [CODING_STANDARDS.md](Assets/Project/Docs/CODING_STANDARDS.md) for details.

## 🚀 Getting Started

1. Clone the repo
2. Open in Unity 6000.0.28f1+
3. Install dependencies (KCC, Cinemachine 3)
4. Open `MainScene`
5. Press Play!

## 🎯 Roadmap

- [ ] Complete Movement System refactor
- [ ] Wall run module
- [ ] Combat system
- [ ] Grappling hook
- [ ] Time rewind polish

## 📖 Documentation

- **Input System**: See [INPUT_SYSTEM_API.md](Assets/Project/Docs/INPUT_SYSTEM_API.md)
- **Architecture**: See [SCALABLE_ARCHITECTURE.md](.gemini/antigravity/brain/*/SCALABLE_ARCHITECTURE.md)

---

Built with ❤️ by Anmol Verma
