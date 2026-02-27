using UnityEngine;
using UnityEngine.UI;
using TMPro; // Essential for the stats UI
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FixEndingUI : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Fix All UI")]
    public static void FixLayout()
    {
        Debug.Log("--- STARTING COMPREHENSIVE UI FIX ---");

        // ----------------------------------------------------
        // 1. KILL THE IMPOSTER (Remove GlobalUIManager from GlobalManager)
        // ----------------------------------------------------
        GameObject globalMgr = GameObject.Find("GlobalManager");
        if (globalMgr != null)
        {
            var imposter = globalMgr.GetComponent<GlobalUIManager>();
            if (imposter != null)
            {
                Debug.LogWarning("Found DUPLICATE GlobalUIManager on GlobalManager. Destroying it!");
                Undo.DestroyObjectImmediate(imposter);
            }
        }

        // ----------------------------------------------------
        // 2. FIX MENTAL DASHBOARD (The one missing its soul)
        // ----------------------------------------------------
        GameObject mentalObject = GameObject.Find("Mental_Dashboard");
        if (mentalObject == null) mentalObject = GameObject.Find("MentalStatsUI");
        
        if (mentalObject != null)
        {
            // A. Ensure Script is Attached
            var statsScript = mentalObject.GetComponent<MentalStatsUI>();
            if (statsScript == null)
            {
                Debug.Log("Adding missing MentalStatsUI component...");
                statsScript = Undo.AddComponent<MentalStatsUI>(mentalObject);
            }

            // B. Ensure CanvasGroup
            var cg = mentalObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = Undo.AddComponent<CanvasGroup>(mentalObject);
            cg.alpha = 1f;

            // C. Auto-Wire References (The Magic Part)
            // We look for children with specific names
            var texts = mentalObject.GetComponentsInChildren<TextMeshProUGUI>(true);
            
            Undo.RecordObject(statsScript, "Wire UI References");
            
            foreach (var t in texts)
            {
                string n = t.name.ToLower();
                if (n.Contains("entropy") && statsScript.entropyText == null) 
                    statsScript.entropyText = t;
                if (n.Contains("psyche") && statsScript.psycheText == null) 
                    statsScript.psycheText = t;
                if (n.Contains("debt") && statsScript.debtText == null) 
                    statsScript.debtText = t;
            }
            Debug.Log($"Wired Mental Refs: Ent={statsScript.entropyText!=null}, Psy={statsScript.psycheText!=null}, Debt={statsScript.debtText!=null}");
            
            // D. Fix Layout
            RectTransform rt = mentalObject.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
            rt.localScale = Vector3.one;
            
            mentalObject.SetActive(true);
        }
        else
        {
            Debug.LogError("Could not find 'Mental_Dashboard' object!");
        }

        // ----------------------------------------------------
        // 3. FIX ENDING PANEL
        // ----------------------------------------------------
        GameObject endingPanel = GameObject.Find("EndingPanel");
        if (endingPanel != null)
        {
            // ... (Same as before)
            RectTransform rt = endingPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            
            Image img = endingPanel.GetComponent<Image>();
            if (img == null) img = endingPanel.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.95f);
            
            // Auto-hide
            endingPanel.SetActive(false);
            Debug.Log("EndingPanel configured and hidden.");
        }
    }
#endif
}
