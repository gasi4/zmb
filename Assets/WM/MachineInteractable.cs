using UnityEngine;

public class MachineInteractable : MonoBehaviour
{
    public WashingMachineUI washingMachineUI;

    void Start()
    {
        if (washingMachineUI == null)
            washingMachineUI = GetComponent<WashingMachineUI>();
    }

    // Этот метод будем вызывать при нажатии E
    public void Interact()
    {
        Debug.Log("Interact с машиной!");

        if (washingMachineUI != null)
        {
            washingMachineUI.ToggleMenu();
        }
        else
        {
            Debug.LogError("WashingMachineUI не найден!");
        }
    }
}