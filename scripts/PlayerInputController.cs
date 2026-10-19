using System;
using Godot;

public partial class PlayerInputController : Node
{
    // I'm not sure if it's planned that the player can change keybindings
    // So these bindings are hardcoded (for now) -thom

    /// <summary>Cursor travel in pixels per second at full stick deflection.</summary>
    public const double DEFAULT_GAMEPAD_CURSOR_SPEED = 700.0;

    /// <summary>Stick displacement ignored before any movement happens.</summary>
    public const double DEFAULT_GAMEPAD_DEADZONE = 0.15;

    /// <summary>
    /// Fired when the mouse moves.
    /// <para>
    /// First parameter is relative movement (delta),
    /// second parameter is absolute screen position.
    /// </para>
    /// </summary>
    public event Action<Vector2, Vector2> OnMouseMove;

    public event Action OnTogglePaused;
    public event Action OnRestartPressed;
    public event Action OnToggleReplayViewerVisibility;
    public event Action OnToggleShowOrthonogalCamera;
    public event Action OnPauseOrSkipPressed;
    public event Action OnPauseOrSkipReleased;
    public event Action OnToggleFade;
    public event Action OnTogglePushback;
    public event Action<bool> OnLeftMouseButton;
    public bool IsEnabled { get; private set; } = true;

    // Gamepad input becomes the same relative mouse delta the mouse path produces, so every
    // cursor mode keeps working unchanged. The virtual pointer only exists to give absolute
    // input modes a coherent position alongside that delta.
    private Vector2 virtualPointer = Vector2.Zero;
    private bool virtualPointerInitialised = false;

    public override void _Process(double delta)
    {
        if (!IsEnabled)
            return;

        handleGamepadMovement((float)delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsEnabled)
            return;

        handleInput(@event);
    }

    private void handleInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion:
            case InputEventMouseButton { ButtonIndex: MouseButton.Left }:
                handleMouseInput(@event);
                break;
            case InputEventJoypadButton:
                handleGamepadButton((InputEventJoypadButton)@event);
                break;
            case InputEventKey { PhysicalKeycode: Key.Escape }:
            case InputEventKey { PhysicalKeycode: Key.F2 }:
            case InputEventKey { PhysicalKeycode: Key.F3 }:
            case InputEventKey { PhysicalKeycode: Key.Space }:
            case InputEventKey { PhysicalKeycode: Key.F }:
            case InputEventKey { PhysicalKeycode: Key.P }:
            case InputEventKey { PhysicalKeycode: Key.Quoteleft }:
                handleKeyboardInput(@event);
                break;
        }
    }

    private void handleMouseInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            // Adopt the real pointer position so switching from pad to mouse never jumps.
            virtualPointer = motion.Position;
            virtualPointerInitialised = true;

            OnMouseMove?.Invoke(motion.Relative, motion.Position);
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (!mouseButton.Pressed || mouseButton.DoubleClick)
                return;

            OnLeftMouseButton?.Invoke(mouseButton.Pressed);
        }
    }

    private void handleGamepadMovement(float delta)
    {
        var devices = Input.GetConnectedJoypads();

        if (devices.Count == 0)
            return;

        int device = devices[0];

        (double speed, double deadzone) = getGamepadTuning();

        if (speed <= 0)
            return;

        var stick = new Vector2(Input.GetJoyAxis(device, JoyAxis.LeftX), Input.GetJoyAxis(device, JoyAxis.LeftY));

        // Some players prefer the right stick; use whichever one is pushed harder.
        var rightStick = new Vector2(Input.GetJoyAxis(device, JoyAxis.RightX), Input.GetJoyAxis(device, JoyAxis.RightY));

        if (rightStick.LengthSquared() > stick.LengthSquared())
        {
            stick = rightStick;
        }

        Vector2 dpad = getDpadDirection(device);

        if (dpad.LengthSquared() > 0f)
        {
            stick = dpad;
        }

        Vector2 direction = applyDeadzone(stick, (float)deadzone);

        if (direction == Vector2.Zero)
            return;

        // Flatten the response near the centre so fine positioning stays possible.
        direction = new Vector2(
            Mathf.Sign(direction.X) * Mathf.Pow(Math.Abs(direction.X), 1.5f),
            Mathf.Sign(direction.Y) * Mathf.Pow(Math.Abs(direction.Y), 1.5f)
        );

        emitPointerDelta(direction * (float)speed * delta);
    }

    private void handleGamepadButton(InputEventJoypadButton button)
    {
        switch (button.ButtonIndex)
        {
            case JoyButton.Start:
                if (button.Pressed)
                {
                    OnTogglePaused?.Invoke();
                }
                break;

            // Face buttons map by position so Xbox and PlayStation pads feel the same.
            case JoyButton.A:
                if (button.Pressed)
                {
                    OnPauseOrSkipPressed?.Invoke();
                }
                else
                {
                    OnPauseOrSkipReleased?.Invoke();
                }
                break;

            case JoyButton.X:
                if (button.Pressed)
                {
                    OnTogglePushback?.Invoke();
                }
                break;

            case JoyButton.Y:
                if (button.Pressed)
                {
                    OnToggleFade?.Invoke();
                }
                break;

            case JoyButton.Back:
                if (button.Pressed)
                {
                    OnToggleShowOrthonogalCamera?.Invoke();
                }
                break;
        }
    }

    private static Vector2 getDpadDirection(int device)
    {
        var dpad = Vector2.Zero;

        if (Input.IsJoyButtonPressed(device, JoyButton.DpadLeft))
        {
            dpad.X -= 1f;
        }

        if (Input.IsJoyButtonPressed(device, JoyButton.DpadRight))
        {
            dpad.X += 1f;
        }

        if (Input.IsJoyButtonPressed(device, JoyButton.DpadUp))
        {
            dpad.Y -= 1f;
        }

        if (Input.IsJoyButtonPressed(device, JoyButton.DpadDown))
        {
            dpad.Y += 1f;
        }

        return dpad.LengthSquared() > 0f ? dpad.Normalized() : Vector2.Zero;
    }

    private static Vector2 applyDeadzone(Vector2 stick, float deadzone)
    {
        float length = stick.Length();

        if (length <= deadzone)
        {
            return Vector2.Zero;
        }

        // Rescale the remaining travel back to a full 0..1 range so the deadzone costs no
        // top speed.
        float scaled = Mathf.Min((length - deadzone) / Mathf.Max(1f - deadzone, 0.0001f), 1f);

        return (stick / length) * scaled;
    }

    private static (double speed, double deadzone) getGamepadTuning()
    {
        SettingsProfile settings = SettingsManager.Instance?.Settings;

        if (settings == null)
        {
            return (DEFAULT_GAMEPAD_CURSOR_SPEED, DEFAULT_GAMEPAD_DEADZONE);
        }

        double speed = settings.GamepadCursorSpeed?.Value ?? DEFAULT_GAMEPAD_CURSOR_SPEED;
        double deadzone = settings.GamepadDeadzone?.Value ?? DEFAULT_GAMEPAD_DEADZONE;

        return (speed, deadzone);
    }

    private void emitPointerDelta(Vector2 pixelDelta)
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

        if (!virtualPointerInitialised)
        {
            virtualPointer = viewportSize / 2f;
            virtualPointerInitialised = true;
        }

        virtualPointer = (virtualPointer + pixelDelta).Clamp(Vector2.Zero, viewportSize);

        OnMouseMove?.Invoke(pixelDelta, virtualPointer);
    }

    private void handleKeyboardInput(InputEvent @event)
    {
        var key = (InputEventKey)@event;

        // Functionality with pressing and releasing

        if (key.Echo)
            return;

        switch (key)
        {
            case { PhysicalKeycode: Key.Space }:
                if (key.Pressed)
                {
                    OnPauseOrSkipPressed?.Invoke();
                }
                else if (!key.Pressed)
                {
                    OnPauseOrSkipReleased?.Invoke();
                }
                break;
        }

        // Functionality with only pressing

        if (!key.Pressed || key.Echo)
            return;

        switch (key)
        {
            case { PhysicalKeycode: Key.Escape }:
                OnTogglePaused?.Invoke();
                break;
            case { PhysicalKeycode: Key.Quoteleft }:
                OnRestartPressed?.Invoke();
                break;
            case { PhysicalKeycode: Key.F2 }:
                OnToggleReplayViewerVisibility?.Invoke();
                break;
            case { PhysicalKeycode: Key.F3 }:
                OnToggleShowOrthonogalCamera?.Invoke();
                break;
            case { PhysicalKeycode: Key.F }:
                OnToggleFade?.Invoke();
                break;
            case { PhysicalKeycode: Key.P }:
                OnTogglePushback?.Invoke();
                break;
        }
    }

    public void ToggleState() => IsEnabled = !IsEnabled;
}
