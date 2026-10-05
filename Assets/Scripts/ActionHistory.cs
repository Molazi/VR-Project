using System.Collections.Generic;

public interface IUndoableAction
{
    void Undo();
    void Redo();
}

public class ActionHistory
{
    private readonly Stack<IUndoableAction> undoStack = new();
    private readonly Stack<IUndoableAction> redoStack = new();

    public bool CanUndo => undoStack.Count > 0;
    public bool CanRedo => redoStack.Count > 0;

    public void Add(IUndoableAction action)
    {
        undoStack.Push(action);
        redoStack.Clear();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        IUndoableAction action = undoStack.Pop();
        action.Undo();
        redoStack.Push(action);
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        IUndoableAction action = redoStack.Pop();
        action.Redo();
        undoStack.Push(action);
    }

    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
    }
}