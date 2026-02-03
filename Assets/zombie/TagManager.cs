using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TagManager : MonoBehaviour
{
    [MenuItem("Tools/Create Game Tags")]
    static void CreateTags()
    {
#if UNITY_EDITOR
        // ��������� ��������� �����
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        // ������ ����� ������� �����
        string[] requiredTags = new string[]
        {
            "Player",
            "Zombie",
            "CleanClothes",
            "CleanThing",
            "DirtyClothes",
            "Interactable",
            "WashingMachine",
            "Item",
            "Untagged"
        };

        // ��������� ����������� ����
        foreach (string tag in requiredTags)
        {
            bool found = false;

            // ��������� ���������� �� ���
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(tag))
                {
                    found = true;
                    break;
                }
            }

            // ��������� ���� �� ������
            if (!found)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
                newTag.stringValue = tag;
                Debug.Log($"�������� ���: {tag}");
            }
        }

        tagManager.ApplyModifiedProperties();
        EditorUtility.DisplayDialog("���� �������", "��� ����������� ���� ��������� � ������", "OK");
#endif
    }

    void Start()
    {
        // ������������� ������� ���� ��� ������� (� ���������)
#if UNITY_EDITOR
        CreateTags();
#endif
    }
}