using UnityEngine;
using UnityEngine.UI;

public class WashingMachineInteractable : MonoBehaviour
{
    [Header("UI Elements")]
    public Canvas machineCanvas;
    public Button closeButton;

    [Header("Настройки курсора")]
    public bool hideCursorOnClose = true; // Скрывать курсор при закрытии UI

    private bool wasCursorVisible; // Запоминаем состояние курсора
    private CursorLockMode previousLockState; // Запоминаем блокировку

    void Start()
    {
        if (machineCanvas != null)
            machineCanvas.enabled = false;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseUI);
    }

    public void OnMachineClicked()
    {
        Debug.Log("Открываем UI стиральной машины...");

        if (machineCanvas != null)
        {
            // Сохраняем состояние курсора до открытия UI
            wasCursorVisible = Cursor.visible;
            previousLockState = Cursor.lockState;

            // Включаем UI
            machineCanvas.enabled = true;

            // Разблокируем и показываем курсор для работы с UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void CloseUI()
    {
        if (machineCanvas != null)
        {
            machineCanvas.enabled = false;

            // Мгновенно скрываем курсор
            StartCoroutine(HideCursorAfterFrame());
        }
    }

    System.Collections.IEnumerator HideCursorAfterFrame()
    {
        yield return new WaitForEndOfFrame(); // Ждем конец кадра

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Курсор скрыт после закрытия UI");
    }

    void OnMouseDown()
    {
        OnMachineClicked();
    }

    // Дополнительно: закрытие по клавише Escape
    void Update()
    {
        if (machineCanvas != null && machineCanvas.enabled)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseUI();
            }
        }
    }
}