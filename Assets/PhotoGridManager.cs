using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using DG.Tweening;

public class PhotoGridManager : MonoBehaviour
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
    private List<GameObject> spawnedImages = new List<GameObject>();
    private Queue<string> imageQueue = new Queue<string>();
    private bool isProcessingImage = false;

    void Start()
    {
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

        foreach (PhotoEntry photo in photos)
        {
            if (!downloadedUrls.Contains(photo.url))
            {
                downloadedUrls.Add(photo.url);
                imageQueue.Enqueue(photo.url);
            }
        }

        if (!isProcessingImage && imageQueue.Count > 0)
        {
            StartCoroutine(ProcessNextImage());
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

            // 1. Show new image in center
            GameObject centerImage = Instantiate(imagePrefab, canvasTransform);
            RectTransform rect = centerImage.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            centerImage.transform.localScale = Vector3.zero;

            RawImage rawImg = centerImage.GetComponent<RawImage>();
            rawImg.texture = texture;

            // Animate scale up
            yield return centerImage.transform.DOScale(1f, 0.6f).SetEase(Ease.OutBack).WaitForCompletion();

            // Wait 5 seconds
            yield return new WaitForSeconds(5f);

            // 2. Move to grid layout
            GameObject gridImage = Instantiate(imagePrefab, imageGridParent);
            gridImage.GetComponent<RawImage>().texture = texture;
            spawnedImages.Add(gridImage);

            Destroy(centerImage);
            UpdateGridCellSize();
        }

        isProcessingImage = false;
    }

    void UpdateGridCellSize()
    {
        int totalImages = spawnedImages.Count;
        if (totalImages == 0) return;

        int maxColumns = Mathf.FloorToInt((maxWidth + spacing) / (300f + spacing));
        int columnCount = Mathf.Min(totalImages, maxColumns);
        int rowCount = Mathf.CeilToInt((float)totalImages / columnCount);

        float cellWidth = (maxWidth - spacing * (columnCount - 1)) / columnCount;
        float cellHeight = (maxHeight - spacing * (rowCount - 1)) / rowCount;

        Vector2 targetCellSize = new Vector2(cellWidth, cellHeight);

        DOTween.To(() => gridLayoutGroup.cellSize,
                   x => gridLayoutGroup.cellSize = x,
                   targetCellSize,
                   animationDuration);
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
