using UnityEngine;

[CreateAssetMenu(fileName = "KeyboardInput", menuName = "Input/Keyboard Input")]
public class KeyboardInputSO : InputProviderSO
{
    [Header("Key Bindings")]
    public KeyCode jumpKey = KeyCode.J;
    public KeyCode turnKey = KeyCode.F;

    private bool jumpRequested;
    private bool turnRequested;

    public override bool JumpRequested => jumpRequested;
    public override bool TurnRequested => turnRequested;

    public override void Initialize()
    {
        jumpRequested = false;
        turnRequested = false;
    }

    public override void Cleanup()
    {
        jumpRequested = false;
        turnRequested = false;
    }

    public void Update()
    {
        if (Input.GetKeyDown(jumpKey))
            jumpRequested = true;

        if (Input.GetKeyDown(turnKey))
            turnRequested = true;
    }

    public override void ConsumeJump()
    {
        jumpRequested = false;
    }

    public override void ConsumeTurn()
    {
        turnRequested = false;
    }
}