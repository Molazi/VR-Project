using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;

public class DebugInputActions : MonoBehaviour
{
    void OnEnable()
    {
        InputSystem.onEvent += OnInputEvent;
    }

    void OnDisable()
    {
        InputSystem.onEvent -= OnInputEvent;
    }

    private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        // Фильтруем только XR-контроллеры, клавиатуру, мышь (чтобы не шумело)
        if (device is XRController || device is Mouse || device is Keyboard)
        {
            // Перебираем все контролы, изменённые в этом событии
            foreach (InputControl control in eventPtr.EnumerateControls(InputControlExtensions.Enumerate.IncludeNonLeafControls))
            {
                // Если контроль – кнопка и она нажата в этом кадре
                if (control is ButtonControl button && button.wasPressedThisFrame)
                {
                    Debug.Log($"Device: {device.displayName}, Button: {control.path}, Value: {button.ReadValue()}");
                }
            }
        }
    }
}