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
        // Set GridLayoutGroup fixed columns to 9
        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = 9;
        gridLayoutGroup.spacing = new Vector2(spacing, spacing);

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

            // 1. Show new image in center with scale animation
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

            // 2. Tween move image smoothly to its grid position
            int nextIndex = spawnedImages.Count; // Next index in grid
            Vector3 targetWorldPos = GetNextGridPosition(nextIndex);

            // Tween position and scale simultaneously (scale remains 1 here but just in case)
            yield return DOTween.Sequence()
                .Join(centerImage.transform.DOMove(targetWorldPos, 0.6f).SetEase(Ease.InOutQuad))
                .Join(centerImage.transform.DOScale(1f, 0.6f).SetEase(Ease.OutBack))
                .WaitForCompletion();

            // 3. Now set parent to grid layout and reset transform for proper layout
            centerImage.transform.SetParent(imageGridParent);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;

            spawnedImages.Add(centerImage);

            // Call UpdateGridCellSize every 20 images added
            if (spawnedImages.Count % 20 == 0)
            {
                UpdateGridCellSize();
            }
        }

        isProcessingImage = false;
    }

    Vector3 GetNextGridPosition(int index)
    {
        int columns = gridLayoutGroup.constraintCount;
        int row = index / columns;
        int column = index % columns;

        Vector2 cellSize = gridLayoutGroup.cellSize;
        Vector2 spacing = gridLayoutGroup.spacing;

        // Calculate local position relative to grid layout's pivot (usually top-left corner)
        float x = (cellSize.x + spacing.x) * column + cellSize.x / 2f;
        float y = -((cellSize.y + spacing.y) * row + cellSize.y / 2f);

        Vector3 localPos = new Vector3(x, y, 0f);
        RectTransform gridRect = imageGridParent.GetComponent<RectTransform>();
        Vector3 worldPos = gridRect.TransformPoint(localPos);

        return worldPos;
    }

    void UpdateGridCellSize()
    {
        int totalImages = spawnedImages.Count;
        if (totalImages == 0) return;

        int columns = 9; // Fixed columns
        int rows = Mathf.CeilToInt((float)totalImages / columns);

        float cellWidth = (maxWidth - spacing * (columns - 1)) / columns;
        float cellHeight = (maxHeight - spacing * (rows - 1)) / rows;

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
