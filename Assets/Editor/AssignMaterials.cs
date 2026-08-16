#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AssignMaterials
{
    [MenuItem("VR Agent/Assign Greybox Materials")]
    public static void Run()
    {
        var assignments = new (string obj, string mat)[]
        {
            ("Torso", "Agent"), ("Head", "Agent"), ("ArmL", "Agent"), ("ArmR", "Agent"),
            ("UpperLegL", "Agent"), ("UpperLegR", "Agent"),
            ("LowerLegL", "Agent"), ("LowerLegR", "Agent"),
            ("Jaw", "Jaw"),
            ("CouchSeat", "Couch"), ("CouchBack", "Couch"),
            ("CouchArmL", "Couch"), ("CouchArmR", "Couch"),
            ("CoffeeTableTop", "Table"),
            ("Rug", "Rug"),
            ("WallNorth", "Wall"), ("WallEast", "Wall"), ("WallWest", "Wall"),
            ("WallSouth", "Wall"), ("Ceiling", "Wall"),
            ("Floor", "Floor"),
            ("Window", "Window"),
        };

        int ok = 0, miss = 0;
        foreach (var (objName, matName) in assignments)
        {
            var go = GameObject.Find(objName);
            var matPath = "Assets/Materials/M_" + matName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (go == null || mat == null) { miss++; Debug.LogWarning($"Skip {objName}/{matPath}"); continue; }
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) { miss++; continue; }
            mr.sharedMaterial = mat;
            ok++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[AssignMaterials] assigned={ok} skipped={miss}");
    }
}
#endif
