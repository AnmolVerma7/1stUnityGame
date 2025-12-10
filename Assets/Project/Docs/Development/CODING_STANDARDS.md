# 🎨 Coding Standards & Formatting

> **Last Updated**: December 2024

## Quick Formatting

Format all scripts with one command:

```bash
dotnet csharpier "Assets/Project/Scripts"
```

> **Note**: Requires CSharpier 0.28.2 (compatible with .NET 6)
> Install: `dotnet tool install csharpier --version 0.28.2 -g`

---

## Naming Conventions

| Element                   | Convention    | Example                                  |
| ------------------------- | ------------- | ---------------------------------------- |
| Private fields            | `_camelCase`  | `private int _health;`                   |
| Serialized private fields | `_camelCase`  | `[SerializeField] private float _speed;` |
| Public fields             | `PascalCase`  | `public int Health;`                     |
| Properties                | `PascalCase`  | `public int Health { get; set; }`        |
| Methods                   | `PascalCase`  | `public void TakeDamage()`               |
| Parameters                | `camelCase`   | `void Heal(int amount)`                  |
| Local variables           | `camelCase`   | `var player = GetPlayer();`              |
| Constants                 | `PascalCase`  | `const float Gravity = 9.81f;`           |
| Interfaces                | `IPascalCase` | `interface IInputUser`                   |
| Enums                     | `PascalCase`  | `enum PlayerState { Idle, Walking }`     |

---

## Code Structure Template

```csharp
using UnityEngine;

namespace Antigravity.Feature
{
    /// <summary>
    /// Brief description of the class.
    /// </summary>
    public class MyClass : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [SerializeField] private Transform _target;

        [Header("Settings")]
        [SerializeField] private float _speed = 5f;

        #endregion

        #region Public Properties

        /// <summary>
        /// The current speed.
        /// </summary>
        public float Speed => _speed;

        #endregion

        #region Private Fields

        private bool _isActive;

        #endregion

        #region Unity Lifecycle

        private void Awake() { }
        private void Start() { }
        private void Update() { }

        #endregion

        #region Public Methods

        /// <summary>
        /// Activates the component.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
        }

        #endregion

        #region Private Methods

        private void DoSomething() { }

        #endregion
    }
}
```

---

## Best Practices

### ✅ Do

- Use `[SerializeField] private` instead of public fields when possible
- Add XML documentation (`/// <summary>`) to all public members
- Use `#region` blocks in files with 100+ lines
- Add `[Tooltip("...")]` for Inspector clarity
- Use `nameof()` instead of magic strings
- Extract magic numbers into constants

### ❌ Don't

- Leave unused `using` statements
- Use public fields without documentation
- Create methods longer than 30 lines (extract helpers)
- Nest more than 3 levels deep (early return instead)

---

## Project-Specific Markers

Some files use special markers in their documentation:

| Marker               | Meaning                                           |
| -------------------- | ------------------------------------------------- |
| `[PROJECT SPECIFIC]` | This file must be modified for each new project   |
| `[TEMPLATE]`         | Copy and customize for your game                  |
| `[BRIDGE]`           | Connects generic systems to project-specific code |

---

## Tools

### CSharpier (Recommended)

Automatic code formatting like Prettier for C#.

```bash
# Install (one-time, .NET 6 compatible version)
dotnet tool install csharpier --version 0.28.2 -g

# Add to PATH (one-time)
echo 'export PATH="$PATH:/Users/anmolverma/.dotnet/tools"' >> ~/.zprofile

# Format entire project
dotnet csharpier "Assets/Project/Scripts"
```

### VS Code Shortcuts

- `Shift+Option+F` - Format current file
- `Cmd+.` - Quick fixes and refactoring

### Rider/JetBrains

- `Cmd+Option+L` - Reformat file
- `Cmd+Shift+Option+L` - Reformat with options

---

## Checklist Before Committing

- [ ] Ran `dotnet csharpier "Assets/Project/Scripts"`
- [ ] All public members have XML documentation
- [ ] No compiler warnings
- [ ] No unused `using` statements
- [ ] Tested in Unity Editor
