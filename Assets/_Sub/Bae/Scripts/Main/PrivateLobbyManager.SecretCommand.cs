using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public partial class PrivateLobbyManager
{
    private enum SecretCommandInput
    {
        Up,
        Down,
        Left,
        Right,
        B,
        A
    }

    private static readonly SecretCommandInput[] ExUnlockCommand =
    {
        SecretCommandInput.Up,
        SecretCommandInput.Up,
        SecretCommandInput.Down,
        SecretCommandInput.Down,
        SecretCommandInput.Left,
        SecretCommandInput.Right,
        SecretCommandInput.Left,
        SecretCommandInput.Right,
        SecretCommandInput.B,
        SecretCommandInput.A
    };

    private const float ExUnlockCommandTimeLimit = 5f;
    private static bool exStageUnlockedBySecretCommand;

    private int secretCommandIndex;
    private float secretCommandStartedAt = -1f;

    public static bool IsExStageUnlockedForCurrentPlayer()
    {
        if (exStageUnlockedBySecretCommand) return true;

        return GameSaveManager.Instance != null &&
               GameSaveManager.Instance.currentData.maxClearedChapter >= 6;
    }

    private bool IsExStageUnlocked()
    {
        return IsExStageUnlockedForCurrentPlayer();
    }

    private void UpdateSecretCommandInput()
    {
        if (exStageUnlockedBySecretCommand) return;

        if (!CanReceiveSecretCommandInput())
        {
            ResetSecretCommand();
            return;
        }

        bool hasDirectionalInput = TryGetSecretCommandDirectionalInput(out SecretCommandInput input);
        bool hasOtherInput = HasOtherSecretCommandInput();

        if (hasOtherInput || (secretCommandIndex > 0 && Time.unscaledTime - secretCommandStartedAt > ExUnlockCommandTimeLimit))
        {
            ResetSecretCommand();
            return;
        }

        if (!hasDirectionalInput) return;

        if (secretCommandIndex == 0)
        {
            secretCommandStartedAt = Time.unscaledTime;
        }

        if (input != ExUnlockCommand[secretCommandIndex])
        {
            ResetSecretCommand();

            if (input == ExUnlockCommand[0])
            {
                secretCommandIndex = 1;
                secretCommandStartedAt = Time.unscaledTime;
            }

            return;
        }

        secretCommandIndex++;

        if (secretCommandIndex < ExUnlockCommand.Length) return;

        exStageUnlockedBySecretCommand = true;
        ResetSecretCommand();
        UpdateChapterUI();
        TilemapPlayerBouncer.PlaySecretCommandSuccessEffect();
        Debug.Log("[PrivateLobbyManager] EX stage unlocked by secret command.");
    }

    private bool CanReceiveSecretCommandInput()
    {
        // [DEMO VERSION] 데모 버전에선 코나미 커맨드를 통한 EX 챕터 해금을 원천 차단합니다.
        return false;
    }

    private void ResetSecretCommand()
    {
        secretCommandIndex = 0;
        secretCommandStartedAt = -1f;
    }

    private bool TryGetSecretCommandDirectionalInput(out SecretCommandInput input)
    {
        input = SecretCommandInput.Up;
        int pressedCount = 0;

        if (Keyboard.current != null)
        {
            AddDirectionalInput(Keyboard.current.upArrowKey.wasPressedThisFrame, SecretCommandInput.Up, ref input, ref pressedCount);
            AddDirectionalInput(Keyboard.current.downArrowKey.wasPressedThisFrame, SecretCommandInput.Down, ref input, ref pressedCount);
            AddDirectionalInput(Keyboard.current.leftArrowKey.wasPressedThisFrame, SecretCommandInput.Left, ref input, ref pressedCount);
            AddDirectionalInput(Keyboard.current.rightArrowKey.wasPressedThisFrame, SecretCommandInput.Right, ref input, ref pressedCount);
            AddDirectionalInput(Keyboard.current.bKey.wasPressedThisFrame, SecretCommandInput.B, ref input, ref pressedCount);
            AddDirectionalInput(Keyboard.current.aKey.wasPressedThisFrame, SecretCommandInput.A, ref input, ref pressedCount);
        }

        if (Gamepad.current != null)
        {
            AddDirectionalInput(Gamepad.current.dpad.up.wasPressedThisFrame || Gamepad.current.leftStick.up.wasPressedThisFrame, SecretCommandInput.Up, ref input, ref pressedCount);
            AddDirectionalInput(Gamepad.current.dpad.down.wasPressedThisFrame || Gamepad.current.leftStick.down.wasPressedThisFrame, SecretCommandInput.Down, ref input, ref pressedCount);
            AddDirectionalInput(Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.left.wasPressedThisFrame, SecretCommandInput.Left, ref input, ref pressedCount);
            AddDirectionalInput(Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame, SecretCommandInput.Right, ref input, ref pressedCount);
            AddDirectionalInput(Gamepad.current.buttonEast.wasPressedThisFrame, SecretCommandInput.B, ref input, ref pressedCount);
            AddDirectionalInput(Gamepad.current.buttonSouth.wasPressedThisFrame, SecretCommandInput.A, ref input, ref pressedCount);
        }

        return pressedCount == 1;
    }

    private void AddDirectionalInput(bool pressed, SecretCommandInput direction, ref SecretCommandInput input, ref int pressedCount)
    {
        if (!pressed) return;

        input = direction;
        pressedCount++;
    }

    private bool HasOtherSecretCommandInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            bool arrowPressed = Keyboard.current.upArrowKey.wasPressedThisFrame ||
                                Keyboard.current.downArrowKey.wasPressedThisFrame ||
                                Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                                Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                                Keyboard.current.bKey.wasPressedThisFrame ||
                                Keyboard.current.aKey.wasPressedThisFrame;

            if (!arrowPressed) return true;
        }

        if (Gamepad.current == null) return false;

        foreach (InputControl control in Gamepad.current.allControls)
        {
            if (control is not ButtonControl button || !button.wasPressedThisFrame) continue;

            if (button == Gamepad.current.dpad.up ||
                button == Gamepad.current.dpad.down ||
                button == Gamepad.current.dpad.left ||
                button == Gamepad.current.dpad.right ||
                button == Gamepad.current.leftStick.up ||
                button == Gamepad.current.leftStick.down ||
                button == Gamepad.current.leftStick.left ||
                button == Gamepad.current.leftStick.right ||
                button == Gamepad.current.buttonEast ||
                button == Gamepad.current.buttonSouth)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
