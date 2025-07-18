using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("Transition Settings")]
    [SerializeField] private GameObject transitionCanvas;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoDisplay;
    [SerializeField] private Image fadePanel; [Header("Sleep Transition")]
    [SerializeField] private VideoClip sleepTransitionVideo;
    [SerializeField] private Sprite[] sleepAnimationSprites;
    [SerializeField] private float spriteAnimationSpeed = 0.2f;

    [Header("Restaurant Transition")]
    [SerializeField] private VideoClip restaurantTransitionVideo;
    [SerializeField] private Sprite[] restaurantAnimationSprites;

    [Header("Day Management")]
    [SerializeField] private int currentDay = 1;
    [SerializeField] private int maxDays = 7;

    // Store names to find references in new scenes
    private string transitionCanvasName = "Transition_Canvas"; private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure this GameObject stays active across scenes
        gameObject.SetActive(true);

        // Store the names of UI objects for later reference finding
        if (transitionCanvas != null)
            transitionCanvasName = transitionCanvas.name;

        // Setup video player if video is assigned
        if (sleepTransitionVideo != null)
        {
            SetupVideoPlayer();
        }

        // Ensure transition canvas is initially hidden
        if (transitionCanvas != null)
            transitionCanvas.SetActive(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-find UI references in the new scene
        StartCoroutine(RefreshUIReferences());
    }
    private IEnumerator RefreshUIReferences()
    {
        // Wait a frame to ensure all scene objects are loaded
        yield return null;

        // Find transition canvas by name (including inactive objects)
        if (transitionCanvas == null)
        {
            // Search for the canvas by name first
            GameObject foundCanvas = GameObject.Find(transitionCanvasName);

            if (foundCanvas == null)
            {
                // Search through all inactive objects too
                Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                foreach (Transform t in allTransforms)
                {
                    if (t.gameObject.scene.IsValid() && // Only scene objects, not prefabs
                        (t.name == "Transition_Canvas" ||
                         t.name == "TransitionCanvas" ||
                         t.name == "Transition Canvas"))
                    {
                        foundCanvas = t.gameObject;
                        break;
                    }
                }
            }

            if (foundCanvas != null)
            {
                transitionCanvas = foundCanvas;
                Debug.Log($"Found transition canvas: {transitionCanvas.name}");
            }
            else
            {
                Debug.LogError("Transition canvas not found in scene! Make sure 'Transition_Canvas' exists in the scene.");
                yield break;
            }
        }

        // Find fade panel and video display within the transition canvas
        if (transitionCanvas != null)
        {
            // Temporarily activate the canvas to search for children
            bool wasActive = transitionCanvas.activeSelf;
            transitionCanvas.SetActive(true);

            // Find fade panel
            if (fadePanel == null)
            {
                Image[] images = transitionCanvas.GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    if (img.name.ToLower().Contains("fade") ||
                        img.name.ToLower().Contains("black") ||
                        img.name.ToLower().Contains("panel"))
                    {
                        fadePanel = img;
                        Debug.Log($"Found fade panel: {fadePanel.name}");
                        break;
                    }
                }

                if (fadePanel == null)
                {
                    Debug.LogWarning("Fade panel not found in transition canvas!");
                }
            }

            // Find video display
            if (videoDisplay == null)
            {
                RawImage[] rawImages = transitionCanvas.GetComponentsInChildren<RawImage>(true);
                foreach (RawImage rawImg in rawImages)
                {
                    if (rawImg.name.ToLower().Contains("video") ||
                        rawImg.name.ToLower().Contains("display"))
                    {
                        videoDisplay = rawImg;
                        Debug.Log($"Found video display: {videoDisplay.name}");
                        break;
                    }
                }

                // If not found by name, use the first RawImage
                if (videoDisplay == null && rawImages.Length > 0)
                {
                    videoDisplay = rawImages[0];
                    Debug.Log($"Using first RawImage as video display: {videoDisplay.name}");
                }

                if (videoDisplay == null)
                {
                    Debug.LogWarning("Video display not found in transition canvas!");
                }
            }

            // Restore the original active state
            transitionCanvas.SetActive(wasActive);
        }

        // Re-setup video player with found references
        if (sleepTransitionVideo != null)
        {
            SetupVideoPlayer();
        }
    }

    private void SetupVideoPlayer()
    {
        if (videoPlayer == null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        // Configure video player settings for better performance
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true; // Allow frame skipping to prevent lag
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;

        // Audio settings
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.SetDirectAudioMute(0, false);

        // Create render texture if needed
        if (videoPlayer.targetTexture == null)
        {
            RenderTexture renderTexture = new RenderTexture(1920, 1080, 24);
            renderTexture.useMipMap = false; // Disable mipmaps for better performance
            videoPlayer.targetTexture = renderTexture;
        }

        // Assign render texture to video display if available
        if (videoDisplay != null && videoPlayer.targetTexture != null)
        {
            videoDisplay.texture = videoPlayer.targetTexture;
            Debug.Log("Video player setup complete with video display");
        }
        else
        {
            Debug.LogWarning("Video display not available for video player setup");
        }
    }
    public void StartSleepTransition()
    {
        Debug.Log("StartSleepTransition called");

        // Ensure this GameObject is active before starting coroutines
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("SceneTransitionManager GameObject was inactive! Reactivating...");
            gameObject.SetActive(true);
        }

        // Ensure we have valid references before starting transition
        if (transitionCanvas == null)
        {
            Debug.LogError("Cannot start transition - transition canvas is missing! Refreshing references...");
            StartCoroutine(RefreshUIReferencesAndStartTransition());
            return;
        }// Check if player qualifies for restaurant transition
        bool useRestaurantTransition = false;
        GameObject restaurantManagerGO = GameObject.Find("RestaurantManager");
        if (restaurantManagerGO != null)
        {
            var restaurantManager = restaurantManagerGO.GetComponent<MonoBehaviour>();
            if (restaurantManager != null)
            {
                try
                {
                    useRestaurantTransition = (bool)restaurantManager.GetType().GetMethod("QualifiesForRestaurantTransition").Invoke(restaurantManager, null);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to call QualifiesForRestaurantTransition: {e.Message}");
                }
            }
            Debug.Log($"Restaurant transition qualified: {useRestaurantTransition}");
        }

        StartCoroutine(SleepTransitionCoroutine(useRestaurantTransition));
    }
    private IEnumerator DelayedStartTransition()
    {
        // Wait for UI references to be refreshed
        yield return new WaitForSeconds(0.5f);

        if (transitionCanvas != null)
        {            // Check restaurant transition here too
            bool useRestaurantTransition = false;
            GameObject restaurantManagerGO = GameObject.Find("RestaurantManager");
            if (restaurantManagerGO != null)
            {
                var restaurantManager = restaurantManagerGO.GetComponent<MonoBehaviour>();
                if (restaurantManager != null)
                {
                    try
                    {
                        useRestaurantTransition = (bool)restaurantManager.GetType().GetMethod("QualifiesForRestaurantTransition").Invoke(restaurantManager, null);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to call QualifiesForRestaurantTransition: {e.Message}");
                    }
                }
            }
            StartCoroutine(SleepTransitionCoroutine(useRestaurantTransition));
        }
        else
        {
            Debug.LogError("Still no transition canvas found after refresh!");
        }
    }
    private IEnumerator SleepTransitionCoroutine(bool useRestaurantTransition = false)
    {
        Debug.Log("Starting sleep transition...");

        // Activate transition canvas
        if (transitionCanvas != null)
        {
            transitionCanvas.SetActive(true);
            Debug.Log("Transition canvas activated");
        }
        else
        {
            Debug.LogError("Transition canvas is null! Cannot proceed with transition.");
            // Still proceed with scene loading as fallback
            LoadNextDayScene();
            yield break;
        }

        // Disable player movement
        DisablePlayerMovement();

        // Choose transition type (video or sprite animation)
        VideoClip videoToPlay = useRestaurantTransition && restaurantTransitionVideo != null ? restaurantTransitionVideo : sleepTransitionVideo;
        Sprite[] spritesToPlay = useRestaurantTransition && restaurantAnimationSprites != null && restaurantAnimationSprites.Length > 0 ? restaurantAnimationSprites : sleepAnimationSprites;

        if (videoToPlay != null && videoPlayer != null && videoDisplay != null)
        {
            string transitionType = useRestaurantTransition ? "restaurant video" : "sleep video";
            Debug.Log($"Playing {transitionType} transition");
            yield return StartCoroutine(PlayVideoTransition(videoToPlay));
        }
        else if (spritesToPlay != null && spritesToPlay.Length > 0)
        {
            string transitionType = useRestaurantTransition ? "restaurant sprite animation" : "sleep sprite animation";
            Debug.Log($"Playing {transitionType} transition");
            yield return StartCoroutine(PlaySpriteAnimation(spritesToPlay));
        }
        else if (fadePanel != null)
        {
            Debug.Log("Playing simple fade transition");
            yield return StartCoroutine(SimpleFadeTransition());
        }
        else
        {
            Debug.LogWarning("No transition elements available! Proceeding with direct scene load.");
            yield return new WaitForSeconds(1f); // Brief pause for user experience
        }

        Debug.Log("Transition complete, loading next day scene...");

        // Load next day scene
        LoadNextDayScene();
    }

    private IEnumerator PlayVideoTransition(VideoClip videoClip)
    {
        if (videoDisplay != null)
        {
            videoDisplay.gameObject.SetActive(true);
            Debug.Log("Video display activated");
        }
        else
        {
            Debug.LogError("Video display is null! Cannot play video transition.");
            yield break;
        }        // Set the video clip
        videoPlayer.clip = videoClip;

        // Prepare the video
        videoPlayer.Prepare();

        // Wait for video to be prepared
        while (!videoPlayer.isPrepared)
        {
            Debug.Log("Preparing video...");
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log($"Video prepared. Duration: {videoPlayer.clip.length} seconds");

        // Play the video
        videoPlayer.Play();

        // Wait for video to actually start playing
        while (!videoPlayer.isPlaying)
        {
            yield return null;
        }

        Debug.Log("Video started playing");

        // Better video completion detection
        float videoDuration = (float)videoPlayer.clip.length;
        float startTime = Time.time;

        // Wait for video to complete using multiple checks
        while (videoPlayer.isPlaying && (Time.time - startTime) < videoDuration + 0.5f)
        {
            yield return null;
        }

        // Additional check: wait until we're near the end of the video
        while (videoPlayer.time < videoDuration - 0.1f && videoPlayer.isPlaying)
        {
            yield return null;
        }

        Debug.Log("Video finished playing");

        // Smooth transition: fade out video while stopping it
        if (videoDisplay != null)
        {
            yield return StartCoroutine(FadeOutVideo());
        }

        // Stop and hide video
        videoPlayer.Stop();
        if (videoDisplay != null)
        {
            videoDisplay.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeOutVideo()
    {
        if (videoDisplay == null) yield break;

        float fadeTime = 1f;
        float elapsedTime = 0f;
        Color startColor = videoDisplay.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            videoDisplay.color = Color.Lerp(startColor, endColor, elapsedTime / fadeTime);
            yield return null;
        }

        // Reset color for next use
        videoDisplay.color = startColor;
    }
    private IEnumerator PlaySpriteAnimation(Sprite[] sprites)
    {
        Image animationImage = transitionCanvas.GetComponentInChildren<Image>();
        if (animationImage == null)
        {
            Debug.LogError("No Image component found in transition canvas for sprite animation");
            yield break;
        }

        // Play sprite animation
        foreach (Sprite sprite in sprites)
        {
            animationImage.sprite = sprite;
            yield return new WaitForSeconds(spriteAnimationSpeed);
        }

        // Fade out
        float fadeTime = 1f;
        float elapsedTime = 0f;
        Color startColor = animationImage.color;

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeTime);
            animationImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
    }

    private IEnumerator SimpleFadeTransition()
    {
        Debug.Log("Starting simple fade transition");
        // Fade to black
        yield return StartCoroutine(FadeToBlack());

        // Wait a moment
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator FadeToBlack()
    {
        if (fadePanel == null)
        {
            Debug.LogError("Fade panel is null! Please assign it in the inspector or ensure it exists in the scene.");
            yield break;
        }

        Debug.Log("Fading to black");

        float fadeTime = 1f;
        float elapsedTime = 0f;
        Color startColor = new Color(0, 0, 0, 0);
        Color endColor = new Color(0, 0, 0, 1);

        fadePanel.gameObject.SetActive(true);

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            fadePanel.color = Color.Lerp(startColor, endColor, elapsedTime / fadeTime);
            yield return null;
        }

        Debug.Log("Fade to black complete");
    }

    public IEnumerator FadeFromBlack()
    {
        if (fadePanel == null) yield break;

        float fadeTime = 1f;
        float elapsedTime = 0f;
        Color startColor = new Color(0, 0, 0, 1);
        Color endColor = new Color(0, 0, 0, 0);

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            fadePanel.color = Color.Lerp(startColor, endColor, elapsedTime / fadeTime);
            yield return null;
        }

        fadePanel.gameObject.SetActive(false);
        if (transitionCanvas != null)
        {
            transitionCanvas.SetActive(false);
        }
    }
    private void LoadNextDayScene()
    {
        Debug.Log("=== STARTING NEXT DAY SCENE LOAD ===");
        StartCoroutine(SaveAndLoadNextDay());
    }
    private IEnumerator SaveAndLoadNextDay()
    {
        Debug.Log("=== SAVING DATA BEFORE SCENE TRANSITION ===");        // Reset hasBeenSaved flag to ensure tile data can be saved
        TileManager tileManager = FindObjectOfType<TileManager>();
        if (tileManager != null)
        {
            // Force reset the saved flag to ensure we can save again
            tileManager.ResetSaveFlag();
            Debug.Log("Reset hasBeenSaved flag to ensure tile data can be saved");
        }        // Force save tile data BEFORE any scene operations
        SaveCurrentSceneData();

        // Debug: Check what was actually saved
        TileManager debugTileManager = FindObjectOfType<TileManager>();
        if (debugTileManager != null)
        {
            debugTileManager.DebugCurrentSaveData();
        }

        // Wait a bit to ensure save operations complete
        yield return new WaitForSeconds(0.5f);        // Verify the save was written
        if (GameManager.instance?.currentSaveData != null)
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (GameManager.instance.currentSaveData.HasTileDataForScene(currentScene))
            {
                var tileData = GameManager.instance.currentSaveData.GetTileDataForScene(currentScene);
                int harvestCount = 0;
                foreach (var tile in tileData.tiles)
                {
                    if (tile.tileName.Contains("Harvest"))
                    {
                        harvestCount++;
                    }
                }
                Debug.Log($"VERIFICATION: Save data contains {harvestCount} harvest tiles for {currentScene}");
            }
            else
            {
                Debug.LogWarning($"No tile data found for current scene {currentScene} in save data!");
            }
        }
        currentDay++;        // Reset restaurant tracking for new day
        GameObject restaurantManagerGO = GameObject.Find("RestaurantManager");
        if (restaurantManagerGO != null)
        {
            var restaurantManager = restaurantManagerGO.GetComponent<MonoBehaviour>();
            if (restaurantManager != null)
            {
                restaurantManager.SendMessage("StartNewDay", SendMessageOptions.DontRequireReceiver);
            }
        }

        if (currentDay > maxDays)
        {
            Debug.Log("Game completed! Checking ending...");
            // Check for game ending with EndingManager
            GameObject endingManagerGO = GameObject.Find("EndingManager");
            if (endingManagerGO != null)
            {
                var endingManager = endingManagerGO.GetComponent<MonoBehaviour>();
                if (endingManager != null)
                {
                    endingManager.SendMessage("CheckForGameEnding", currentDay, SendMessageOptions.DontRequireReceiver);
                }
            }
            else
            {
                Debug.LogWarning("EndingManager not found! Loading main menu as fallback.");
                SceneManager.LoadScene(0);
            }
        }
        else
        {
            string nextSceneName = "Day" + currentDay;
            Debug.Log($"Loading scene: {nextSceneName}");
            yield return StartCoroutine(LoadSceneAsync(nextSceneName));
        }
    }// Add this method to ensure data is saved
    private void SaveCurrentSceneData()
    {
        try
        {
            Debug.Log("=== SAVING ALL GAME DATA BEFORE SCENE TRANSITION ===");

            // Save tile data FIRST with extra safety checks
            TileManager tileManager = FindObjectOfType<TileManager>();
            if (tileManager != null)
            {
                Debug.Log("Found TileManager, saving tile data...");
                tileManager.SaveTileDataForCurrentScene();
            }
            else if (GameManager.instance?.tileManager != null)
            {
                Debug.Log("Using GameManager TileManager reference...");
                GameManager.instance.tileManager.SaveTileDataForCurrentScene();
            }
            else
            {
                Debug.LogWarning("No TileManager found - tile data will not be saved!");
            }

            // Save player inventory
            Player player = FindObjectOfType<Player>();
            if (player != null)
            {
                Debug.Log("Found Player, saving inventory data...");
                player.SaveInventoryData();
            }
            else if (GameManager.instance?.player != null)
            {
                Debug.Log("Using GameManager Player reference...");
                GameManager.instance.player.SaveInventoryData();
            }
            else
            {
                Debug.LogWarning("No Player found - inventory data will not be saved!");
            }

            // Save currency data explicitly
            if (CurrencyManager.instance != null && GameManager.instance != null)
            {
                int currentCurrency = CurrencyManager.instance.GetCurrentCurrency();
                GameManager.instance.currentSaveData.playerCurrency = currentCurrency;
                Debug.Log($"Saved currency amount: {currentCurrency}");
            }

            // CRITICAL: Save the entire game state to file through GameManager
            if (GameManager.instance != null)
            {
                Debug.Log("Saving complete game state to file...");
                GameManager.instance.SaveGame();
                Debug.Log("Game state saved to file successfully!");
            }
            else
            {
                Debug.LogError("GameManager.instance is null - cannot save game to file!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving scene data: {e.Message}");
        }
    }
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        // Check if scene exists in build settings
        bool sceneExists = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
#if UNITY_EDITOR
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameInBuild == sceneName)
            {
                sceneExists = true;
                break;
            }
#else
            // In build, just try to load and handle the exception
            sceneExists = true;
            break;
#endif
        }

        if (!sceneExists)
        {
            Debug.LogError($"Scene '{sceneName}' not found in build settings!");
            yield break;
        }

        // Start loading the scene asynchronously
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // Wait for the scene to be 90% loaded
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        Debug.Log("Scene loaded, activating...");

        // Activate the scene
        asyncLoad.allowSceneActivation = true;

        // Wait for scene to be fully loaded
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Debug.Log("Scene activation complete");
    }

    private void DisablePlayerMovement()
    {
        // Disable player movement during transition
        Movement playerMovement = FindObjectOfType<Movement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        // Also disable player script
        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            player.enabled = false;
        }
    }

    private void EnablePlayerMovement()
    {
        // Re-enable player movement after transition
        Movement playerMovement = FindObjectOfType<Movement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        // Also enable player script
        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            player.enabled = true;
        }
    }

    private void Start()
    {
        EnablePlayerMovement();

        // Delay GameManager reference refresh to avoid null reference errors
        StartCoroutine(DelayedGameManagerRefresh());

        // If we just loaded a new scene and there's a fade panel, fade from black
        if (fadePanel != null && fadePanel.color.a > 0.5f)
        {
            StartCoroutine(FadeFromBlack());
        }
    }

    private IEnumerator DelayedGameManagerRefresh()
    {
        // Wait to ensure all objects are properly initialized
        yield return new WaitForSeconds(0.3f);

        // Refresh GameManager references after scene load
        if (GameManager.instance != null)
        {
            GameManager.instance.RefreshManagerReferences();
        }
    }

    // Public methods for external use
    public int GetCurrentDay() => currentDay;
    public void SetCurrentDay(int day) => currentDay = day;    /// <summary>
                                                               /// Force refresh of UI references - useful for debugging or when canvas is lost
                                                               /// </summary>
    [ContextMenu("Force Refresh UI References")]
    public void ForceRefreshUIReferences()
    {
        Debug.Log("Forcing refresh of UI references...");
        transitionCanvas = null;
        fadePanel = null;
        videoDisplay = null;
        StartCoroutine(RefreshUIReferences());
    }
    private IEnumerator RefreshUIReferencesAndStartTransition()
    {
        yield return StartCoroutine(RefreshUIReferences());

        // Try starting transition again after refresh
        if (transitionCanvas != null)
        {
            StartSleepTransition();
        }
        else
        {
            Debug.LogError("Failed to find transition canvas after refresh!");
        }
    }
}
