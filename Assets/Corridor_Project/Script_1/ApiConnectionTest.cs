using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ApiConnectionTest : MonoBehaviour
{
    // 测试用的公共 API 地址 (返回 JSON 数据)
    // Public API for testing (returns JSON)
    public string url = "https://jsonplaceholder.typicode.com/todos/1";

    void Start()
    {
        Debug.Log("开始 API 测试: " + url);
        StartCoroutine(GetRequest(url));
    }

    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // 发送请求并等待响应
            // Send request and wait for response
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("API Error: " + webRequest.error);
            }
            else
            {
                Debug.Log("API Success! Received: " + webRequest.downloadHandler.text);
            }
        }
    }
}
