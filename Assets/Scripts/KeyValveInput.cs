using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardValveInput : MonoBehaviour
{
    void Update()
    {
        // Keyboard fallback for testing: A/S/D map to valves 1/2/3.
        if (Keyboard.current == null)
        {
            return;
        }

        ValveInputState.SetKeyboardValve(0, Keyboard.current.aKey.isPressed);
        ValveInputState.SetKeyboardValve(1, Keyboard.current.sKey.isPressed);
        ValveInputState.SetKeyboardValve(2, Keyboard.current.dKey.isPressed);
    }
}
