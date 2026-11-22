using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

// Normal play keeps the project's existing Input Manager bindings.
// Editor playtests use real Input System devices through the same gameplay entry points.
public static class GameInput
{
#if UNITY_EDITOR
    public static Keyboard TestKeyboard { get; private set; }
    public static Mouse TestMouse { get; private set; }

    public static void BeginTestInput()
    {
        if (TestKeyboard != null) throw new System.InvalidOperationException("Test input already active.");
        TestKeyboard = InputSystem.AddDevice<Keyboard>("LaundryQAKeyboard");
        TestMouse = InputSystem.AddDevice<Mouse>("LaundryQAMouse");
    }

    public static void EndTestInput()
    {
        if (TestKeyboard != null && TestKeyboard.added) InputSystem.RemoveDevice(TestKeyboard);
        if (TestMouse != null && TestMouse.added) InputSystem.RemoveDevice(TestMouse);
        TestKeyboard = null;
        TestMouse = null;
    }
#endif

    public static float GetAxisRaw(string axis)
    {
#if UNITY_EDITOR
        if (TestKeyboard != null)
        {
            if (axis == "Horizontal") return (TestKeyboard.dKey.isPressed ? 1f : 0f) - (TestKeyboard.aKey.isPressed ? 1f : 0f);
            if (axis == "Vertical") return (TestKeyboard.wKey.isPressed ? 1f : 0f) - (TestKeyboard.sKey.isPressed ? 1f : 0f);
        }
#endif
        return Input.GetAxisRaw(axis);
    }

    public static float GetAxis(string axis)
    {
#if UNITY_EDITOR
        if (TestMouse != null)
        {
            Vector2 delta = TestMouse.delta.ReadValue();
            if (axis == "Mouse X") return delta.x;
            if (axis == "Mouse Y") return delta.y;
        }
#endif
        return Input.GetAxis(axis);
    }

    public static bool GetKeyDown(KeyCode key)
    {
#if UNITY_EDITOR
        if (TestKeyboard != null)
        {
            if (key == KeyCode.E) return TestKeyboard.eKey.wasPressedThisFrame;
            if (key == KeyCode.Tab) return TestKeyboard.tabKey.wasPressedThisFrame;
            if (key == KeyCode.F1) return TestKeyboard.f1Key.wasPressedThisFrame;
            if (key == KeyCode.Escape) return TestKeyboard.escapeKey.wasPressedThisFrame;
            if (key == KeyCode.UpArrow) return TestKeyboard.upArrowKey.wasPressedThisFrame;
            if (key == KeyCode.DownArrow) return TestKeyboard.downArrowKey.wasPressedThisFrame;
            return false;
        }
#endif
        return Input.GetKeyDown(key);
    }
}
