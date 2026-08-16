#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class DiagNPCAgent
{
    [MenuItem("VR Agent/Diagnose NPCAgent")]
    public static void Run()
    {
        var go = GameObject.Find("NPCAgent");
        if (go == null) { Debug.LogError("[DIAG] NPCAgent not found"); return; }
        var src = go.GetComponent<AudioSource>();
        var npc = go.GetComponent<NPCClient>();
        Debug.Log($"[DIAG] play={Application.isPlaying} npc!=null={npc!=null} src.isPlaying={src.isPlaying} clip={(src.clip!=null?src.clip.name:"null")} spatialBlend={src.spatialBlend}");
        var t = typeof(NPCClient);
        var qf = t.GetField("audioQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (qf != null) {
            var q = qf.GetValue(npc) as System.Collections.ICollection;
            Debug.Log($"[DIAG] audioQueue.Count={q?.Count}");
        }
        var connF = t.GetField("connectionEstablished", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var edlF = t.GetField("expectedDataLength", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var tbrF = t.GetField("totalBytesRead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var lpfF = t.GetField("lastPartFlag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Debug.Log($"[DIAG] connectionEstablished={connF?.GetValue(npc)} expectedDataLength={edlF?.GetValue(npc)} totalBytesRead={tbrF?.GetValue(npc)} lastPartFlag={lpfF?.GetValue(npc)}");
    }
}
#endif
