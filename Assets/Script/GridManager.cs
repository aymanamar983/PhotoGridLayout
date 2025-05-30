using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using DG.Tweening;

public class GridManager : MonoBehaviour
{
    public GameObject imagePrefab;
    public Transform imageGridParent;
    public GridLayoutGroup gridLayoutGroup;
    public RectTransform canvasTransform;

    public float maxWidth = 1920f;
    public float maxHeight = 1080f;
    public float spacing = 10f;
    public float animationDuration = 0.4f;

    private const string dataUrl = "https://abbinj3.sg-host.com/list.php";
    private HashSet<string> downloadedUrls = new HashSet<string>();
    private Queue<string> imageQueue = new Queue<string>();
    private bool isProcessingImage = false;

    void Start()
    {
        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = 9;
        gridLayoutGroup.spacing = new Vector2(spacing, spacing);

        // Start periodic checking but do NOT load images until new users appear
        InvokeRepeating(nameof(CheckForUpdates), 0f, 10f);
        
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

        bool newUserFound = false;

        foreach (PhotoEntry photo in photos)
        {
            string normalizedUrl = photo.url.Trim().ToLower();

            // Only enqueue if this URL wasn't downloaded before
            if (!downloadedUrls.Contains(normalizedUrl))
            {
                downloadedUrls.Add(normalizedUrl);
                imageQueue.Enqueue(photo.url);
                newUserFound = true;
            }
        }

        if (newUserFound)
        {
            Debug.Log("New user detected. Starting to download image.");
            if (!isProcessingImage)
            {
                StartCoroutine(ProcessNextImage());
            }
        }
        else
        {
            Debug.Log("No new users found.");
        }
    }

    IEnumerator ProcessNextImage()
    {
        isProcessingImage = true;

        while (imageQueue.Count > 0)
        {
            string url = imageQueue.Dequeue();

            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to download image: " + url);
                continue;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);

            GameObject newImageGO = Instantiate(imagePrefab, canvasTransform);
            RectTransform rect = newImageGO.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            newImageGO.transform.localScale = Vector3.zero;

            RawImage rawImg = newImageGO.GetComponent<RawImage>();
            rawImg.texture = texture;

            // Animate scale up in center
            yield return newImageGO.transform.DOScale(1f, 0.6f).SetEase(Ease.OutBack).WaitForCompletion();

            // Move to grid layout position
            newImageGO.transform.SetParent(imageGridParent, false);
            int index = imageGridParent.childCount - 1;
            Vector3 targetPos = GetGridPosition(index);

            yield return newImageGO.transform.DOMove(targetPos, 0.6f).SetEase(Ease.InOutQuad).WaitForCompletion();

            // Reset local position and scale to fit grid nicely
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;

            yield return new WaitForSeconds(0.5f); // small pause before next image
        }

        isProcessingImage = false;
    }

    Vector3 GetGridPosition(int index)
    {
        int columns = gridLayoutGroup.constraintCount;
        int row = index / columns;
        int column = index % columns;

        Vector2 cellSize = gridLayoutGroup.cellSize;
        Vector2 spacing = gridLayoutGroup.spacing;

        float x = (cellSize.x + spacing.x) * column + cellSize.x / 2f;
        float y = -((cellSize.y + spacing.y) * row + cellSize.y / 2f);

        Vector3 localPos = new Vector3(x, y, 0f);
        RectTransform gridRect = imageGridParent.GetComponent<RectTransform>();
        Vector3 worldPos = gridRect.TransformPoint(localPos);

        return worldPos;
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
