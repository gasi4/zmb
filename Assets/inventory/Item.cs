using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemScriptableObject item;
    public int amount;
    public bool isClean = false;

    public void MakeClean()
    {
        isClean = true;

        // Ставим тег чистой вещи (если тег не создан в Tag Manager — просто игнор)
        try { gameObject.tag = "CleanThing"; } catch { }

        // Меняем цвет на белый для визуального отличия
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.white;
        }
    }

}