using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameMode
{
    MoveLimited,      
    TimeLimited
}

public enum WinCondition
{
    ReachTargetScore,
    CollectAllFruits,
    ScoreAndCollectAllFruits
}

[System.Serializable]
public class FruitCollectionGoal
{
    public FruitType fruitType;
    public Sprite fruitIcon;
    public int requiredCount;
    [HideInInspector]public bool IsCompleted = false;
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    InputAction touchAction;
    [SerializeField] CameraShake cameraShake;
    private GameObject[,] AllFruit;
    private Fruit selectedFruit;
    private bool isGameFinish, isFruitDragging, isShuffling;

    [Header("-------- Main Settings & Logic -------- ")]
    public float yOffset = 5f;
    [SerializeField] int Width = 8;
    [SerializeField] int Height = 14;
    // Randomly spawn fruit prefabs on the grid at game start
    [SerializeField] GameObject[] fruitsObject;
    [SerializeField] GameObject specialFruit;
    [SerializeField] GameObject bombFruit;
    [SerializeField] GameObject fruitExplosionEffect;

    [Header("-------- Effect Sound -------- ")]
    [SerializeField] AudioSource soundSource;
    [SerializeField] AudioClip destroySfx_Match3; // Match-3 destroy sound
    [SerializeField] AudioClip destroySfx_Match4; // Match-4 destroy sound

    [Header("-------- Explosion Effects -------- ")]
    [SerializeField] GameObject[] explosionPrefabs;
    private Queue<GameObject> explosionPool;

    [Header("-------- Game Mode -------- ")]
    public GameMode _GameMode = GameMode.TimeLimited;
    [SerializeField] int targetScore = 1000;
    [SerializeField] float TimeLimit = 60;
    [SerializeField] int MoveLimit = 30;

    private float remainingTime;
    private int remainingMoves;
    private int currentScore;

    [Header("-------- Win Condition -------- ")]
    public WinCondition _WinCondition = WinCondition.ReachTargetScore;
    public List<FruitCollectionGoal> _FruitCollectionGoal = new();
    [SerializeField] private Transform objectivesPanel;
    [SerializeField] private GameObject fruitObjectiveUIPrefab;
    private Dictionary<FruitType, int> collectedFruitCounts = new();
    private Dictionary<FruitType, TextMeshProUGUI> fruitObjectiveTexts = new();

    [Header("-------- UI -------- ")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI targetScoreText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private TextMeshProUGUI timeOrMoveLabelText; // Text elements for timer and move counter

    [SerializeField] private TextMeshProUGUI shuffleRemainingText;
    [SerializeField] private TextMeshProUGUI specialPowerCountText;

    [SerializeField] private Button shuffleButton;
    [SerializeField] private Button specialPowerButton;

    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    [SerializeField] private GameObject scorePanel;
    [SerializeField] private GameObject fruitObjectivesPanel;

    [SerializeField] private TextMeshProUGUI winGoldRewardText;
    [SerializeField] private TextMeshProUGUI loseGoldRewardText;



    private int specialPowerCount; // How many special powers the player can use
    private int shuffleCount; // How many times the player can shuffle the board

    private void Awake()
    {
        explosionPool = new Queue<GameObject>();
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        specialPowerCount = PlayerPrefs.GetInt("SpecialPowerCount");
        shuffleCount = PlayerPrefs.GetInt("ShuffleCount");
    }

    void Start()
    {
        touchAction = InputSystem.actions.FindAction("Carry");
        touchAction.performed += x => TouchControl();        
        touchAction.Enable();
        
        AllFruit = new GameObject[Width, Height];
        CreateFruitGrid();
        CreateDestroyEffectPool();
        StartCoroutine(CheckFirstMatch());

        InitializeGameplayUI();
        UpdateTargetFruitsUI();
    }

    // ------ Initial settings, initial operations and grid creation ------
    void CreateFruitGrid()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                PlaceFruitsOnGrid(x,y,y + yOffset);
            }
        }
    }
    void PlaceFruitsOnGrid(int gridX, int gridY, float PlaceYOffset)
    {
        int randomIndex = Random.Range(0, fruitsObject.Length);
        Vector2 PlacePosition = new Vector2(gridX, PlaceYOffset);

        GameObject placedFruit = Instantiate(fruitsObject[randomIndex], PlacePosition, Quaternion.identity, transform);
        Fruit fruit = placedFruit.GetComponent<Fruit>();
        fruit.Create(gridX, gridY);

        AllFruit[gridX, gridY] = placedFruit;
    }


    // ------ Touch, Select Fruit ------
    void TouchControl()
    {
        Vector2 screenPositiion = Touchscreen.current.position.ReadValue();
        Vector2 pos = Camera.main.ScreenToWorldPoint(screenPositiion);

        var hit = Physics2D.OverlapPoint(pos);
        if (hit != null && hit.TryGetComponent(out Fruit fruit))
        {
            OnClickFruit(fruit);
        }
    }
    void OnClickFruit(Fruit clickedFruit)
    {
        if (isGameFinish || isFruitDragging) return;

        if (clickedFruit.fruitType == FruitType.Special)
        {
            ActivateSpecialFruit(clickedFruit.x, clickedFruit.y);
            Destroy(clickedFruit.gameObject);
            AllFruit[clickedFruit.x, clickedFruit.y] = null;
            return;
        }
        
        if (clickedFruit.fruitType == FruitType.Bomb)
        {
            ActivateBombFruit(clickedFruit.x, clickedFruit.y);
            Destroy(clickedFruit.gameObject);
            AllFruit[clickedFruit.x, clickedFruit.y] = null;
            return;
        }

        if (selectedFruit == null)
        {
            selectedFruit = clickedFruit;
            selectedFruit.SetSelected(true);
            return;
        }

        // Cancel selection when clicking the same fruit again 
        if (selectedFruit == clickedFruit)
        {
            selectedFruit.SetSelected(false);
            selectedFruit = null;
            return;
        }

        // Check for matches after swap, revert if none found
        if (IsValidSwap(selectedFruit, clickedFruit))
        {
            StartCoroutine(CheckCanSwap(selectedFruit, clickedFruit));
        }
        else
        {
            selectedFruit.SetSelected(false);
            selectedFruit = clickedFruit;
            selectedFruit.SetSelected(true);         
        }
       
    }


    // ------ Swap Check ------
    bool IsValidSwap(Fruit a, Fruit b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1); // True when fruits are adjacent, false otherwise
    }
    // Swap adjacent fruits and revert if no match occurs
    IEnumerator CheckCanSwap(Fruit a, Fruit b)
    {
        isFruitDragging = true;
        SwapFruit(a, b);

        while (a.IsMoving() || b.IsMoving())
        {
            yield return null;
        }

        if (a.fruitType == FruitType.Special)
        {
            ActivateSpecialFruit(a.x, a.y);
            Destroy(a.gameObject);
            AllFruit[a.x, a.y] = null;
        }
        else if (b.fruitType == FruitType.Special)
        {
            ActivateSpecialFruit(b.x, b.y);
            Destroy(b.gameObject);
            AllFruit[b.x, b.y] = null;
        }
        else if (FindMatches())
        {
            ClearMatches();
        }
        else
        {
            // Swap back if no match occurs
            SwapFruit(a, b);
            while (a.IsMoving() || b.IsMoving())
            {
                yield return null;
            }
        }

        // Update Move Limit
        if (_GameMode == GameMode.MoveLimited && !isGameFinish)
        {
            remainingMoves--;
            movesText.text = remainingMoves.ToString();
            if (remainingMoves <= 0)
            {
                OnGameLost();
                yield break;
            }
        }


        if (selectedFruit != null)
        {
            selectedFruit.SetSelected(false);
            selectedFruit = null;
        }

        isFruitDragging = false;
       
        yield break;

    }
    void SwapFruit(Fruit a, Fruit b)
    {
        int ax = a.x;
        int ay = a.y;
        int bx = b.x;
        int by = b.y;

        AllFruit[ax, ay] = b.gameObject;
        AllFruit[bx, by] = a.gameObject;

        a.AssignGridPosition(bx, by);
        b.AssignGridPosition(ax, ay);
    }



    // ------ Explosion Mechanics / Row, Column ------

    // Selecting a special fruit clears all fruits in its row and column
    void ActivateSpecialFruit(int gridX, int gridY)
    {       
        cameraShake.Shake(0.12f, 0.5f);

        // Mark the full row of the special fruit
        for (int i = 0; i < Width; i++)
        {
            if (AllFruit[i, gridY] != null)
            {
                AllFruit[i, gridY].GetComponent<Fruit>().IsMatched = true;
            }
        }

        // Mark the full column of the special fruit
        for (int i = 0; i < Height; i++)
        {
            if (AllFruit[gridX, i] != null)
            {
                AllFruit[gridX, i].GetComponent<Fruit>().IsMatched = true;
            }
        }

        ClearMatches();
    }
    // Bomb destroys all nearby fruits in a square pattern when selected
    void ActivateBombFruit(int gridX, int gridY)
    {
        for (int ax = -1; ax <= 1; ax++) // Check one cell to the left and right
        {
            for (int ay = -1; ay <= 1; ay++) // Check one cell above and below
            {
                int nx = gridX + ax;
                int ny = gridY + ay;

                // Check grid boundaries for corner cases
                if (IsOnGridEdge(nx, ny) && AllFruit[nx, ny] != null)
                {
                    AllFruit[nx, ny].GetComponent<Fruit>().IsMatched = true;
                   
                }

            }
        }

        ClearMatches();
    }
    bool IsOnGridEdge(int gridX, int gridY)
    {
        return gridX >= 0 && gridX < Width && gridY >= 0 && gridY < Height;
    }


    // ------ Check match, validate swap, revert if invalid ------
    bool FindMatches()
    {
        bool isFindMatch = false;
        
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                GameObject currentFruit = AllFruit[x, y];
                if (currentFruit == null) continue;

                FruitType currentFruitType = currentFruit.GetComponent<Fruit>().fruitType;

                // Horizontal 5-match check excluding the current fruit
                if (x <= Width - 5 && HasMatchingFruitType(currentFruitType, x, y, 1, 0, 4))
                {
                    PlaceBombFruit(x + 2, y);
                    MarkMatchedFruits(x, y, 1, 0, 5);
                    isFindMatch = true;
                    continue;
                }

                // 5-match vertical check excluding the current fruit
                if (y <= Height - 5 && HasMatchingFruitType(currentFruitType, x, y, 0, 1, 4))
                {
                    PlaceBombFruit(x, y + 2);
                    MarkMatchedFruits(x, y, 0, 1, 5);
                    isFindMatch = true;
                    continue;
                }

                // Horizontal 4-match check
                if (x <= Width - 4 && HasMatchingFruitType(currentFruitType, x, y, 1, 0, 3))
                {
                    PlaceSpecialFruit(x + 1, y);
                    MarkMatchesInRange(x, y, 1, 0, 4, 1);
                    isFindMatch = true;
                    continue;
                }

                // Vertical 4-match check
                if (y <= Height - 4 && HasMatchingFruitType(currentFruitType, x, y, 0, 1, 3))
                {
                    PlaceSpecialFruit(x, y + 1);
                    MarkMatchesInRange(x, y, 0, 1, 4, 1);
                    isFindMatch = true;
                    continue;
                }

                // Horizontal 3-match check
                if (x <= Width - 3 && HasMatchingFruitType(currentFruitType, x, y, 1, 0, 2))
                {
                    MarkMatchedFruits(x, y, 1, 0, 3);
                    isFindMatch = true;                   
                }

                // Vertical 3-match check
                if (y <= Height - 3 && HasMatchingFruitType(currentFruitType, x, y, 0, 1, 2))
                {
                    MarkMatchedFruits(x, y, 0, 1, 3);
                    isFindMatch = true;
                }

            }
        }

        return isFindMatch;
    }

    // MatchDistance defines the check length; dx and dy define the check direction
    bool HasMatchingFruitType(FruitType currentFruitType, int x, int y, int dx, int dy, int MatchDistance)
    {
        for (int i = 1; i <= MatchDistance; i++)
        {
            int nx = x + dx * i;
            int ny = y + dy * i;

            if (!IsOnGridEdge(nx, ny)) return false;

            GameObject f = AllFruit[nx, ny];
            if (f == null) return false;

            if (f.GetComponent<Fruit>().fruitType != currentFruitType)
                return false;
        }
        return true;
    }

    // Check for 5 matches
    void PlaceBombFruit(int x, int y)
    {
        Vector3 pos = AllFruit[x, y].transform.position;
        Destroy(AllFruit[x, y]);
        GameObject bombGameObject = Instantiate(bombFruit, pos, Quaternion.identity, transform);
        Fruit f = bombGameObject.GetComponent<Fruit>();
        f.Create(x, y);
        f.IsSpecialSpawned = true; // Protect
        AllFruit[x, y] = bombGameObject;
    }
    void MarkMatchedFruits(int x, int y, int dx, int dy, int MatchDistance)
    {
        for (int i = 0; i < MatchDistance; i++)
        {
            int nx = x + dx * i;
            int ny = y + dy * i;

            if (!IsOnGridEdge(nx, ny)) continue;

            GameObject go = AllFruit[nx, ny];
            if (go == null) continue;

            go.GetComponent<Fruit>().IsMatched = true;
        }
    }

    // Check for 4 matches
    void PlaceSpecialFruit(int x, int y)
    {
        Vector3 pos = AllFruit[x, y].transform.position;
        Destroy(AllFruit[x, y]);
        GameObject SpecialFruitObject = Instantiate(specialFruit, pos, Quaternion.identity, transform);
        Fruit f = SpecialFruitObject.GetComponent<Fruit>();
        f.Create(x, y);
        f.IsSpecialSpawned = true; // Protect
        AllFruit[x, y] = SpecialFruitObject;
    }
    void MarkMatchesInRange(int x, int y, int dx, int dy, int MatchDistance, int ExternalIndex)
    {
        for (int i = 0; i < MatchDistance; i++)
        {         
            int nx = x + dx * i;
            int ny = y + dy * i;

            if (!IsOnGridEdge(nx, ny)) continue;

            GameObject go = AllFruit[nx, ny];
            if (go == null) continue;

            go.GetComponent<Fruit>().IsMatched = true;
        }
    }


    // ------ Refill the grid by spawning new fruits after explosions ------
    void ClearMatches()
    {
        StartCoroutine(StartClearMatches());
    }
    IEnumerator StartClearMatches()
    {
        List<Fruit> Match = new();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (AllFruit[x, y] != null)
                {
                    var f = AllFruit[x, y].GetComponent<Fruit>();
                    if (f.IsMatched && !f.IsSpecialSpawned)
                    {
                        Match.Add(f);
                        AllFruit[x, y] = null;
                    }
                    else
                    {
                        f.IsMatched = false; // Reset flags
                    }
                }
            }
        }

        int matchCount = Match.Count;
        if (matchCount == 4)
        {
            soundSource.PlayOneShot(destroySfx_Match4);
        }
        else if (matchCount >= 3)
        {
            soundSource.PlayOneShot(destroySfx_Match3);
        }

        UpdateScore(matchCount);

        // Clear matched fruits
        foreach (var f in Match)
        {
            StartCoroutine(f.DestroyAnimaton());

            if (_WinCondition != WinCondition.ReachTargetScore)
            {
                CheckAndUpdateTargetFruits(f.fruitType);
            }

        }
        yield return new WaitForSeconds(0.35f);

        // Refill empty cells with new fruits
        StartCoroutine(RefillGrid());

    }
    IEnumerator RefillGrid()
    {
        yield return new WaitForSeconds(0.1f);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (AllFruit[x, y] == null)
                {
                    PlaceFruitsOnGrid(x, y, y + yOffset); 
                }
            }
        }

        yield return new WaitForSeconds(1.1f);
        if (FindMatches())
        {
            ClearMatches();
        }
    }


    // ------ Trigger pooled explosion effect for matched fruits ------
    void CreateDestroyEffectPool()
    {
        foreach (var prefab in explosionPrefabs)
        {
            if (prefab == null) continue;

            GameObject effect = Instantiate(prefab);
            effect.SetActive(false);
            explosionPool.Enqueue(effect);
        }
    }
    public void PlayDestroyEffect(Vector3 pos)
    {
        if (fruitExplosionEffect == null)
        {
            Debug.LogError("Explosion prefab missing!");
            return;
        }

        GameObject effect = explosionPool.Count > 0
            ? explosionPool.Dequeue()
            : Instantiate(fruitExplosionEffect);

        effect.transform.position = pos;
        effect.SetActive(true);

        StartCoroutine(ReturnEffectToPool(effect));
    }
    IEnumerator ReturnEffectToPool(GameObject pooledEffect)
    {
        yield return new WaitForSeconds(1f);
        pooledEffect.SetActive(false);
        explosionPool.Enqueue(pooledEffect);
    }


    IEnumerator CheckFirstMatch()
    {
        yield return new WaitForSeconds(1.1f);
        if (FindMatches())
        {
            ClearMatches();
        }
    }

    // ------ Checks target fruit completion, updates UI and controls level progression, win condition ------   
    void InitializeGameplayUI()
    {
        shuffleRemainingText.text = shuffleCount.ToString();
        shuffleButton.interactable = shuffleCount > 0;

        specialPowerCountText.text = specialPowerCount.ToString();
        specialPowerButton.interactable = specialPowerCount > 0;

        bool IsScorePanelVisible = _WinCondition != WinCondition.CollectAllFruits;
        scorePanel.SetActive(IsScorePanelVisible);

        if (_GameMode == GameMode.TimeLimited)
        {
            timeText.gameObject.SetActive(true);
            movesText.gameObject.SetActive(false);
            timeOrMoveLabelText.gameObject.SetActive(true);
            timeOrMoveLabelText.text = "TIME";
            remainingTime = TimeLimit;
            StartCoroutine(StartCountdown());
        }
        else
        {
            movesText.gameObject.SetActive(true);
            timeText.gameObject.SetActive(false);
            timeOrMoveLabelText.gameObject.SetActive(true);
            timeOrMoveLabelText.text = "MOVE";
            remainingMoves = MoveLimit;
            movesText.text = remainingMoves.ToString();
        }
    }
    IEnumerator StartCountdown()
    {
        while (remainingTime > 0 && !isGameFinish)
        {
            remainingTime -= Time.deltaTime;
            timeText.text = "" + Mathf.CeilToInt(remainingTime);
            yield return null;
        }

        // Time ran out, level not completed
        if (!isGameFinish)
        {
            OnGameLost();
        }
    }

    void UpdateScore(int MatchCount)
    {
        currentScore += MatchCount * 50;
        scoreText.text = currentScore.ToString();

        bool hasWon = false;

        switch (_WinCondition)
        {
            case WinCondition.ReachTargetScore:
                hasWon = currentScore >= targetScore;
                break;
            case WinCondition.CollectAllFruits:
                hasWon = IsCollectFruitsComplete();
                break;
            case WinCondition.ScoreAndCollectAllFruits:
                hasWon = currentScore >= targetScore && IsCollectFruitsComplete(); 
                break;
        }

        if (hasWon)
        {
            OnGameWin();
        }
    }
    bool IsCollectFruitsComplete()
    {
        foreach(var targetFruit in _FruitCollectionGoal)
        {
            if (!targetFruit.IsCompleted)
            {
                return false;
            }        
        }
        return true;
    }


    void CheckAndUpdateTargetFruits(FruitType collectedFruitType)
    {
        if (!collectedFruitCounts.ContainsKey(collectedFruitType))
        {
            collectedFruitCounts[collectedFruitType] = 0;
        }

        FruitCollectionGoal targetFruit = _FruitCollectionGoal.Find(t => t.fruitType == collectedFruitType);

        if (targetFruit == null || targetFruit.IsCompleted) return;

        collectedFruitCounts[collectedFruitType]++;

        int currentTargetCount = collectedFruitCounts[collectedFruitType];
        int requiredTargetCount = targetFruit.requiredCount;

        fruitObjectiveTexts[collectedFruitType].text = $"{Mathf.Min(currentTargetCount, requiredTargetCount)} / {requiredTargetCount}";
        
        if (currentTargetCount >= requiredTargetCount && !targetFruit.IsCompleted)
        {
            targetFruit.IsCompleted = true;
            Transform obj = fruitObjectiveTexts[collectedFruitType].transform.parent;
            if (obj.TryGetComponent<Image>(out var bg))
            {
                bg.color = new Color(0.7f, 1f, 0.7f); // green
            }
        }

        bool fruitsComplete = IsCollectFruitsComplete();
        bool hasWon = false;
        if (_WinCondition == WinCondition.CollectAllFruits && fruitsComplete)
        {
            hasWon = true;
        }
        else if (_WinCondition == WinCondition.ScoreAndCollectAllFruits && fruitsComplete && currentScore >= targetScore)
        {
            hasWon = true;
        }

        if (!isGameFinish && hasWon)
        {
            OnGameWin();
        }

    }

    void UpdateTargetFruitsUI()
    {
        ClearTargetFruitsUI();

        if (_WinCondition == WinCondition.ReachTargetScore || _WinCondition == WinCondition.ScoreAndCollectAllFruits)
        {
            targetScoreText.text = targetScore.ToString();
            scorePanel.SetActive(true);
        }
        else 
        {
            scorePanel.SetActive(false);
        }

        if (_WinCondition == WinCondition.CollectAllFruits || _WinCondition == WinCondition.ScoreAndCollectAllFruits)
        {
            fruitObjectivesPanel.SetActive(true);
            foreach (var targetFruit in _FruitCollectionGoal)
            {
                collectedFruitCounts[targetFruit.fruitType] = 0;
                GameObject UIObj = Instantiate(fruitObjectiveUIPrefab, objectivesPanel);
                Image icon = UIObj.transform.Find("Icon").GetComponent<Image>();
                TextMeshProUGUI countText = UIObj.transform.Find("Count").GetComponent<TextMeshProUGUI>();
                icon.sprite = targetFruit.fruitIcon;
                countText.text = $"0 / {targetFruit.requiredCount}";
                fruitObjectiveTexts[targetFruit.fruitType] = countText;
            }
        }
        else
        {
            fruitObjectivesPanel.SetActive(false);
        }

    }

    void ClearTargetFruitsUI()
    {
        foreach(Transform child in objectivesPanel)
        {
            Destroy(child.gameObject);
        }

        fruitObjectiveTexts.Clear();
        collectedFruitCounts.Clear();
    }

   
    // Shuffle
    public void ShuffleFruit()
    {
        if (isShuffling || shuffleCount <= 0) return;

        StartCoroutine(StartShuffle());
    }
    IEnumerator StartShuffle()
    {
        isShuffling = true;
        shuffleCount--;
        shuffleRemainingText.text = shuffleCount.ToString();
        PlayerPrefs.SetInt("ShuffleCount", shuffleCount);
        shuffleButton.interactable = shuffleCount > 0;

        List<GameObject> FruitList = new();
        foreach(var item in AllFruit)
        {
            if (item != null)
            {
                FruitList.Add(item);
            }
        }

        for (int i = 0; i < FruitList.Count; i++)
        {
            int j = Random.Range(0, i + 1);
            (FruitList[i], FruitList[j]) = (FruitList[j], FruitList[i]);
        }

        int index = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var obj = FruitList[index];
                var fruit = obj.GetComponent<Fruit>();
                fruit.AssignGridPosition(x, y);
                AllFruit[x, y] = obj;
                index++;
            }
        }

        yield return new WaitForSeconds(0.3f);

        while (IsAnyFruitMoving()) yield return null;

        // After movement finishes
        if (FindMatches())
        {
            ClearMatches();
        }

        isShuffling = false;

    }
    bool IsAnyFruitMoving()
    {
        foreach(var obj in AllFruit)
        {
            if (obj.GetComponent<Fruit>().IsMoving())
            {
                return true;
            }         
        }
        return false;
    }

    
    // Special Power
    public void UseSpecialPower()
    {
        if (specialPowerCount <= 0 || isGameFinish) return;

        Dictionary<FruitType, List<Fruit>> currentFruits = new();

        foreach (var obj in AllFruit)
        {
            if (obj == null) continue;

            Fruit fruit = obj.GetComponent<Fruit>();
            FruitType fruitType = fruit.fruitType;

            if (!currentFruits.ContainsKey(fruitType))
            {
                currentFruits[fruitType] = new List<Fruit>();
            }

            currentFruits[fruitType].Add(fruit);
        }

        FruitType mostCommonFruitType = default;
        int maxCount = 0;

        foreach (var pair in currentFruits)
        {
            if (pair.Value.Count > maxCount)
            {
                maxCount = pair.Value.Count;
                mostCommonFruitType = pair.Key;
            }
        }

        foreach (Fruit fruit in currentFruits[mostCommonFruitType])
        {
            fruit.IsMatched = true;
        }
        specialPowerCount--;
        specialPowerCountText.text = specialPowerCount.ToString();
        PlayerPrefs.SetInt("SpecialPowerCount", specialPowerCount);
        specialPowerButton.interactable = specialPowerCount > 0;

        ClearMatches();

    }


    // Lose - Win
    void OnGameLost()
    {
        isGameFinish = true;
        losePanel.SetActive(true);
    }

    void OnGameWin()
    {
        isGameFinish = true;
        winPanel.SetActive(true);

        PlayerPrefs.SetInt("Level", SceneManager.GetActiveScene().buildIndex + 1);

        int goldReward = UnityEngine.Random.Range(12, 30);
        //int shuffleReward = UnityEngine.Random.Range(1, 3);
        //int specialPowerReward = UnityEngine.Random.Range(1, 2);

        //PlayerPrefs.SetInt("ShuffleCount", PlayerPrefs.GetInt("ShuffleCount") + shuffleReward);
        //PlayerPrefs.SetInt("SpecialPowerCount", PlayerPrefs.GetInt("SpecialPowerCount") + specialPowerReward);

        PlayerPrefs.SetInt("Gold", PlayerPrefs.GetInt("Gold") + goldReward);
        winGoldRewardText.text = "+" + goldReward.ToString();
       

    }

    public void NextLevel()
    {
        SceneManager.LoadScene(PlayerPrefs.GetInt("Level"));
    }

    public void RetryLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene(0);
    }



}
