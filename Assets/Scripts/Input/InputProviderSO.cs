using UnityEngine;

/// <summary>
/// Base ScriptableObject for input providers
/// </summary>
public abstract class InputProviderSO : ScriptableObject
{
    public abstract bool JumpRequested { get; }
    public abstract bool TurnRequested { get; }
    public abstract void ConsumeJump();
    public abstract void ConsumeTurn();
    public abstract void Initialize();
    public abstract void Cleanup();
}