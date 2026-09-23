using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

public class FinalDebugInput : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("=== FinalDebugInput Awake ===");
    }

    void Start()
    {
        Debug.Log("=== FinalDebugInput Start ===");
    }

    void Update()
    {
        // 1) Проверим клавиатуру
        if (Keyboard.current != null)
        {
            // Выведем только при нажатии пробела (чтобы не засорять)
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                Debug.Log("ПРОБЕЛ НАЖАТ! Скрипт работает.");
        }

        // 2) Проверим XR-контроллеры напрямую
        var leftHand = InputSystem.GetDevice<XRController>(CommonUsages.LeftHand);
        var rightHand = InputSystem.GetDevice<XRController>(CommonUsages.RightHand);

        if (leftHand != null)
        {
            foreach (InputControl control in leftHand.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                    Debug.Log($"Left Hand: {control.name} pressed");
            }
        }
        else
        {
            // Выводим раз в секунду, что контроллер не найден
            if (Time.frameCount % 60 == 0)
                Debug.Log("Left Hand controller not found");
        }

        if (rightHand != null)
        {
            foreach (InputControl control in rightHand.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                    Debug.Log($"Right Hand: {control.name} pressed");
            }
        }
        else
        {
            if (Time.frameCount % 60 == 0)
                Debug.Log("Right Hand controller not found");
        }
    }
}