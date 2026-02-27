using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmergencyGUI : MonoBehaviour
{
    private void OnGUI()
    {
        GUI.skin.label.fontSize = 24;
        GUI.color = Color.yellow;
        GUILayout.BeginArea(new Rect(10, 10, 600, 500), GUI.skin.box);
        GUILayout.Label("--- EMERGENCY UI DEBUG ---");

        Check("Mental_Dashboard");
        Check("MentalStatsUI");
        Check("EndingPanel");
        
        GUILayout.EndArea();
    }

    void Check(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            GUILayout.Label($"[MISSING] {name}");
        }
        else
        {
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            string alpha = cg ? cg.alpha.ToString("F2") : "No CG";
            
            RectTransform rt = obj.GetComponent<RectTransform>();
            string scale = rt ? rt.localScale.ToString() : "No RT";
            
            string text = "";
            var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) text = tmp.text;
            else {
                var legacy = obj.GetComponentInChildren<Text>();
                if (legacy) text = legacy.text;
            }

            GUILayout.Label($"[FOUND] {name}\n Active: {obj.activeInHierarchy}\n Alpha: {alpha}\n Scale: {scale}\n Text: {text}");
        }
    }
}
