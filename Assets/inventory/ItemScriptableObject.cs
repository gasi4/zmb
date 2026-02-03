using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Cloth 
}

[CreateAssetMenu(menuName = "Inventory/Item")]
public class ItemScriptableObject : ScriptableObject
{
    public string ItemName;

    [Header("Prefabs")]
    public GameObject WorldPrefab; // ❗ для мира (дроп, сцена)
    public GameObject HandPrefab;  // ❗ для руки (визуал)

    public Sprite Icon;
    public int MaxAmount = 1;
}


