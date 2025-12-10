# Legacy Undo System

> [!NOTE]
> This system was removed from the active codebase to reduce complexity, as it was not needed for the current game genre. This document preserves the logic for future reference.

The Input System originally included a lightweight, global Undo system designed for UX toggles, menus, and debugging. It allowed actions to push an "Undo" operation onto a stack, which could then be reversed.

## Components

### 1. InputHistoryService

A `MonoBehaviour` singleton that maintained a `LinkedList<IUndoableAction>` of executed actions.

- **`Push(IUndoableAction action)`**: Adds an action to the history. Trims the list if it exceeds `_maxEntries`.
- **`UndoLast()`**: Pops the last action and calls its `Undo()` method.

### 2. UndoableButtonCommand

An implementation of `IInputCommand` that wrapped a standard action with an undo callback.

```csharp
// Example Usage
var command = new UndoableButtonCommand(
    onPerformed: () => DeleteItem(item),
    undoAction: () => RestoreItem(item),
    description: "Delete Item"
);
```

When executed, it would:

1.  Perform the `onPerformed` action immediately.
2.  Push a `DelegateUndoableAction` (containing the `undoAction`) to the `InputHistoryService`.

## Integration

To restore this system:

1.  Recreate `InputHistoryService.cs` (Singleton, DontDestroyOnLoad).
2.  Recreate `UndoableButtonCommand.cs` (IInputCommand implementation).
3.  Bind actions using `UndoableButtonCommand` in your Input Binder.
