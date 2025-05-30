using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ShareImage : MonoBehaviour
{
    private const string dataUrl = "https://abbinj3.sg-host.com/list.php";
    private HashSet<string> downloadedUrls = new HashSet<string>();

    void Start()
    {
        InvokeRepeating(nameof(CheckForUpdates), 0f, 10f); // every 10 seconds
    }

    void CheckForUpdates()
    {
        StartCoroutine(FetchPhotoList());
    }

    IEnumerator FetchPhotoList()
    {
        UnityWebRequest request = UnityWebRequest.Get(dataUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error fetching photo list: " + request.error);
            yield break;
        }

        string json = request.downloadHandler.text;
        PhotoEntry[] photos = JsonHelper.FromJson<PhotoEntry>(json);

        foreach (PhotoEntry photo in photos)
        {
            if (!downloadedUrls.Contains(photo.url))
            {
                downloadedUrls.Add(photo.url);
                StartCoroutine(DownloadImage(photo.url));
            }
        }
    }

    IEnumerator DownloadImage(string url)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            Debug.Log("Downloaded image: " + url);

            // Example: Apply to a quad (replace with your use case)
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.position = new Vector3(downloadedUrls.Count, 0, 0);
            quad.GetComponent<Renderer>().material.mainTexture = texture;
        }
        else
        {
            Debug.LogError("Failed to download image: " + url);
        }
    }

    [System.Serializable]
    public class PhotoEntry
    {
        public string name;
        public string email;
        public string phone;
        public string url;
    }

    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string wrapped = "{\"array\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return wrapper.array;
        }

        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }
}