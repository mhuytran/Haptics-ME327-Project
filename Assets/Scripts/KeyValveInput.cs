using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardValveInput : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        ValveInputState.SetKeyboardValve(0, Keyboard.current.aKey.isPressed);
        ValveInputState.SetKeyboardValve(1, Keyboard.current.sKey.isPressed);
        ValveInputState.SetKeyboardValve(2, Keyboard.current.dKey.isPressed);
    }
}