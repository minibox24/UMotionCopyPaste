/*
 * UMotionCopyPaste v1.0
 * discord @minibox._.
 */

using System.Collections.Generic;
using UMotionEditor.API;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public class UMotionCopyPaste : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    private VisualTreeAsset ItemAsset;

    private List<(string, string)> storage = new();

    private TextField textField;
    private Button copyButton;
    private ListView listView;

    static UMotionCopyPaste()
    {
        PoseEditor.AddButton(PoseEditor.FoldoutCategory.Tools, "Open CopyPasteTool", "복붙툴을 엽니다", OpenWindow);
    }

    public static void OpenWindow()
    {
        UMotionCopyPaste wnd = GetWindow<UMotionCopyPaste>();
        wnd.titleContent = new GUIContent("UMotionCopyPaste");
    }

    public void CreateGUI()
    {
        ReadStorage();

        VisualElement root = rootVisualElement;

        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);

        listView = root.Q<ListView>("CopyList");
        textField = root.Q<TextField>("NameField");
        copyButton = root.Q<Button>("CopyButton");

        copyButton.clickable.clicked += CopyButtonClicked;
        copyButton.SetEnabled(false);

        textField.RegisterValueChangedCallback((evt) =>
        {
            copyButton.SetEnabled(evt.newValue != "");
        });

        textField.RegisterCallback<KeyDownEvent>((evt) =>
        {
            if (evt.keyCode == KeyCode.Return)
            {
                CopyButtonClicked();
            }
        });

        ItemAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Minibox/UMotionCopyPaste/Editor/UMotionCopyPasteItem.uxml");

        MakeListView();
    }

    void WriteStorage()
    {
        string data = "";

        foreach ((string, string) d in storage)
        {
            data += d.Item1 + "\n";
            data += d.Item2 + "\n\n";
        }

        System.IO.File.WriteAllText("Assets/_Minibox/UMotionCopyPaste/Editor/UMotionCopyPasteData.txt", data);
    }

    void ReadStorage()
    {
        if (!System.IO.File.Exists("Assets/_Minibox/UMotionCopyPaste/Editor/UMotionCopyPasteData.txt"))
        {
            return;
        }

        string data = System.IO.File.ReadAllText("Assets/_Minibox/UMotionCopyPaste/Editor/UMotionCopyPasteData.txt");

        string[] datas = data.Split("\n\n");
        foreach (string item in datas)
        {
            if (item == "")
            {
                continue;
            }

            string[] items = item.Split("\n");
            storage.Add((items[0], items[1]));
        }
    }

    void CopyButtonClicked()
    {
        string data = SerializeSelectedTransforms();
        storage.Add((textField.value, data));

        textField.value = "";

        WriteStorage();

        listView.Rebuild();
    }

    void MakeListView()
    {
        listView.itemsSource = storage;
        listView.makeItem = () => ItemAsset.CloneTree();
        listView.bindItem = (element, index) =>
        {
            element.Q<Label>("Title").text = storage[index].Item1;
            element.Q<Label>("Description").text = GetDescription(storage[index].Item2);

            element.Q<Button>("Delete").clicked += () =>
            {
                bool yes = EditorUtility.DisplayDialog("지우기", "정말 지울까용", "넹", "아뇨");
                if (yes)
                {
                    storage.RemoveAt(index);
                    WriteStorage();

                    listView.Rebuild();
                }
            };

            element.Q<Button>("Paste").clicked += () =>
            {
                PasteTextTransforms(storage[index].Item2);
            };
        };
    }

    string GetDescription(string data)
    {
        string[] keys = data.Split(';');
        string desctiprion = $"[{keys.Length - 1}] ";

        for (int i = 0; i < keys.Length - 1; i++)
        {
            string[] values = keys[i].Split(':');
            string name = values[0].Split("/")[^1];
            desctiprion += name;

            if (i != keys.Length - 2)
            {
                desctiprion += ", ";
            }
        }

        return desctiprion;
    }

    string SerializeSelectedTransforms()
    {
        List<Transform> transforms = new();
        PoseEditor.GetAllTransforms(transforms);

        List<Transform> selectedTransforms = new();
        PoseEditor.GetSelectedTransforms(selectedTransforms);

        if (selectedTransforms.Count == 0)
        {
            throw new System.Exception("복사할 게 없어용 ㅜㅜ");
        }

        Dictionary<string, Transform> transformsMap = new();
        Transform hips = GetHips(selectedTransforms[0]);

        foreach (Transform selectedTransform in selectedTransforms)
        {
            transformsMap.Add(GetTransformName(selectedTransform, hips), selectedTransform);
        }

        return SerializeTransforms(transformsMap);
    }

    void PasteTextTransforms(string data)
    {
        Dictionary<string, (Vector3, Quaternion)> transformMap = DeserializeTransform(data);

        List<Transform> transforms = new List<Transform>();
        PoseEditor.GetAllTransforms(transforms);

        Transform hips = GetHips(transforms[0]);

        for (int i = 0; i < transforms.Count; i++)
        {
            string name = GetTransformName(transforms[i], hips);

            if (transformMap.ContainsKey(name))
            {
                Vector3 position = transformMap[name].Item1;
                Quaternion rotation = transformMap[name].Item2;

                PoseEditor.TrySetFkWorldPosition(transforms[i], position, "pastePos", true);
                PoseEditor.TrySetFkWorldRotation(transforms[i], rotation, "pasteRot", true);

                transformMap.Remove(name);
            }
        }
    }

    string GetTransformName(Transform transform, Transform hips)
    {
        if (transform.parent == null || hips == transform)
        {
            return transform.name;
        }
        else
        {
            return GetTransformName(transform.parent, hips) + "/" + transform.name;
        }
    }

    Transform GetHips(Transform transform)
    {
        bool ok = transform.TryGetComponent(out Animator animator);

        if (ok && animator.avatar.isHuman)
        {
            return animator.GetBoneTransform(HumanBodyBones.Hips);
        }

        else if (transform.parent == null)
        {
            return null;
        }
        else
        {
            return GetHips(transform.parent);
        }

    }

    string SerializeTransforms(Dictionary<string, Transform> transformsMap)
    {
        string data = "";
        foreach (KeyValuePair<string, Transform> entry in transformsMap)
        {
            data += entry.Key + ":" + SerializeTransform(entry.Value) + ";";
        }
        return data;
    }

    string SerializeTransform(Transform transform)
    {
        float rw = transform.rotation.w;
        float rx = transform.rotation.x;
        float ry = transform.rotation.y;
        float rz = transform.rotation.z;
        float px = transform.position.x;
        float py = transform.position.y;
        float pz = transform.position.z;

        return rw + "," + rx + "," + ry + "," + rz + "," + px + "," + py + "," + pz;
    }

    Dictionary<string, (Vector3, Quaternion)> DeserializeTransform(string data)
    {
        Dictionary<string, (Vector3, Quaternion)> transformsMap = new();

        string[] datas = data.Split(';');
        foreach (string d in datas)
        {
            if (d == "")
            {
                continue;
            }

            string[] values = d.Split(':');

            string key = values[0];
            string[] value = values[1].Split(',');

            float rw = float.Parse(value[0]);
            float rx = float.Parse(value[1]);
            float ry = float.Parse(value[2]);
            float rz = float.Parse(value[3]);
            float px = float.Parse(value[4]);
            float py = float.Parse(value[5]);
            float pz = float.Parse(value[6]);

            Vector3 position = new(px, py, pz);
            Quaternion rotation = new(rx, ry, rz, rw);

            transformsMap.Add(key, (position, rotation));
        }

        return transformsMap;
    }
}
