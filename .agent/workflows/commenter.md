---
description: Enhance codebase comments and structure for maximum clarity.
---

# Commenter Workflow 📝

This workflow focuses on making the codebase understandable to **anyone**, regardless of their familiarity with the project. It emphasizes clear XML documentation, helpful tooltips, and logical code organization.

## 1. Analyze Context 🔍

- **Identify Target**: Determine which files need attention. Ideally, focus on files modified in the current session or specified by the user.
- **Read**: Use `view_file` to read the entire content of the target files.

## 2. Enhance Documentation 📚

For each file, step through and ensure the following:

- **Class Headers**: Every class MUST have a `<summary>` describing its _responsibility_ and _role_ in the system.
- **Method Headers**: Public methods MUST have XML documentation (`/// <summary>`, `/// <param>`, `/// <returns>`).
- **Inspector Fields**: All `[SerializeField]` or public fields MUST have a `[Tooltip("...")]` explaining their effect in the Editor.
- **Inline Comments**: Explain _complex logic_ or _math_. Do NOT comment obvious code (e.g., `i++ // increment i`).

## 3. Structural Organization 🏗️

- **Regions**: Organize code into logical `#region` blocks:
  - `Inspector Fields` / `Settings`
  - `Dependencies` / `References`
  - `State` / `Private Variables`
  - `Unity Lifecycle` (Awake, Start, Update)
  - `Public API`
  - `Internal Logic` / `Helpers`
- **Ordering**: Keep `public` members above `private` members generally, or grouped by functionality.

## 4. Verification ✅

- **Readability Check**: Read the code again. Is it clear? Are variable names self-explanatory?
- **Format**: Run the code formatter (`dotnet csharpier`) if available.

## Example Output

```csharp
/// <summary>
/// Handles player movement physics.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Maximum speed in m/s.")]
    [SerializeField] private float _moveSpeed = 10f;

    // ...
}
```
