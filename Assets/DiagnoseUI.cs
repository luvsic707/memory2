using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DiagnoseUI : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("--- UI DIAGNOSTIC START ---");
        
        CheckObject("MentalStatsUI");
        CheckObject("Mental_Dashboard");
        CheckObject("EndingPanel");

        Debug.Log("--- UI DIAGNOSTIC END ---");
    }

    void CheckObject(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            Debug.LogError($"[Missing] GameObject '{name}' NOT FOUND in scene!");
            return;
        }

        Debug.Log($"[Found] '{name}': Active={obj.activeInHierarchy}, Layer={LayerMask.LayerToName(obj.layer)}");
        
        RectTransform rt = obj.GetComponent<RectTransform>();
        if(rt) Debug.Log($"   Rect: Pos={rt.position}, LocalPos={rt.localPosition}, Size={rt.rect.size}, Scale={rt.localScale}");

        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if(cg) Debug.Log($"   CanvasGroup: Alpha={cg.alpha}, BlocksRaycasts={cg.blocksRaycasts}");

        // Check Texts
        var texts = obj.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach(var t in texts)
        {
            Debug.Log($"   Text '{t.name}': Content='{t.text}', Color={t.color}, Visible={t.enabled}");
        }
        
        var legacyTexts = obj.GetComponentsInChildren<Text>(true);
        foreach(var t in legacyTexts)
        {
            Debug.Log($"   LegacyText '{t.name}': Content='{t.text}', Color={t.color}, Visible={t.enabled}");
        }
    }
}
