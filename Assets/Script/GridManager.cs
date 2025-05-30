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
    private HashSet<string> savedUserUrls = new HashSet<string>();  // saved new user URLs loaded from PlayerPrefs
    private HashSet<string> knownUrls = new HashSet<string>();       // all URLs processed or saved (to avoid duplicates)
    private Queue<string> imageQueue = new Queue<string>();
    private bool isProcessingImage = false;
    private List<GameObject> spawnedImages = new List<GameObject>();

    private int lastRowCount = 0;

    void Start()
    {
        // Set GridLayoutGroup fixed columns to 9
        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = 9;
        gridLayoutGroup.spacing = new Vector2(spacing, spacing);

        LoadSavedNewUserUrls();
        StartCoroutine(InstantiateSavedImages());

        InvokeRepeating(nameof(CheckForUpdates), 5f, 10f);  // start checking for new users after 5 sec delay
    }

    // Load only saved new user URLs from PlayerPrefs
    void LoadSavedNewUserUrls()
    {
        savedUserUrls.Clear();
        knownUrls.Clear();

        string savedUrlsString = PlayerPrefs.GetString("SavedNewUserUrls", "");
        if (!string.IsNullOrEmpty(savedUrlsString))
        {
            string[] urls = savedUrlsString.Split(';');
            foreach (string url in urls)
            {
                if (!string.IsNullOrEmpty(url))
                {
                    savedUserUrls.Add(url);
                    knownUrls.Add(url);
                }
            }
        }
    }

    // Save current savedUserUrls to PlayerPrefs (called after adding new user)
    void SaveNewUserUrls()
    {
        PlayerPrefs.SetString("SavedNewUserUrls", string.Join(";", savedUserUrls));
        PlayerPrefs.Save();
    }

    // Instantiate saved new user images instantly (no animation)
    IEnumerator InstantiateSavedImages()
    {
        foreach (string url in savedUserUrls)
        {
            yield return StartCoroutine(DownloadAndAddImage(url, animate: false));
        }
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

        int newUserCount = 0;
        foreach (PhotoEntry photo in photos)
        {
            if (!knownUrls.Contains(photo.url))
            {
                knownUrls.Add(photo.url);
                imageQueue.Enqueue(photo.url);
                savedUserUrls.Add(photo.url);  // Add new user url to saved set
                newUserCount++;
            }
        }

        if (newUserCount > 0)
        {
            Debug.Log($"New users detected: {newUserCount}. Starting to download images.");
            SaveNewUserUrls(); // Save the updated list immediately
        }
        else
        {
            Debug.Log("No new users found.");
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
            yield return DownloadAndAddImage(url, animate: true);
        }

        isProcessingImage = false;
    }

    IEnumerator DownloadAndAddImage(string url, bool animate)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to download image: " + url);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);

        GameObject imageGO = Instantiate(imagePrefab, canvasTransform);
        RectTransform rect = imageGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        imageGO.transform.localScale = animate ? Vector3.zero : Vector3.one;

        RawImage rawImg = imageGO.GetComponent<RawImage>();
        rawImg.texture = texture;

        if (animate)
        {
            yield return imageGO.transform.DOScale(1f, 0.6f).SetEase(Ease.OutBack).WaitForCompletion();
            yield return new WaitForSeconds(1f);

            Vector3 targetWorldPos = GetNextGridPosition(spawnedImages.Count);

            yield return DOTween.Sequence()
                .Join(imageGO.transform.DOMove(targetWorldPos, 0.6f).SetEase(Ease.InOutQuad))
                .Join(imageGO.transform.DOScale(1f, 0.6f).SetEase(Ease.OutBack))
                .WaitForCompletion();

            imageGO.transform.SetParent(imageGridParent);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
        }
        else
        {
            // Directly add to grid without animation
            imageGO.transform.SetParent(imageGridParent);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
        }

        spawnedImages.Add(imageGO);

        // Adjust grid size if needed
        int columns = gridLayoutGroup.constraintCount;
        if (spawnedImages.Count >= 26)
        {
            int currentRowCount = Mathf.CeilToInt((float)spawnedImages.Count / columns);
            if (currentRowCount > lastRowCount)
            {
                UpdateGridCellSize(currentRowCount);
                lastRowCount = currentRowCount;
            }
        }
    }

    Vector3 GetNextGridPosition(int index)
    {
        int columns = gridLayoutGroup.constraintCount;
        int row = index / columns;
        int column = index % columns;

        Vector2 cellSize = gridLayoutGroup.cellSize;
        Vector2 spacingVec = gridLayoutGroup.spacing;

        float x = (cellSize.x + spacingVec.x) * column + cellSize.x / 2f;
        float y = -((cellSize.y + spacingVec.y) * row + cellSize.y / 2f);

        Vector3 localPos = new Vector3(x, y, 0f);
        RectTransform gridRect = imageGridParent.GetComponent<RectTransform>();
        Vector3 worldPos = gridRect.TransformPoint(localPos);

        return worldPos;
    }

    void UpdateGridCellSize(int rowCount)
    {
        int columns = gridLayoutGroup.constraintCount;

        float cellWidth = (maxWidth - spacing * (columns - 1)) / columns;
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
