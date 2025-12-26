using System.Collections;
using UnityEngine;

public enum FruitType
{
    // Regular Fruits
    Apple,
    Banana,
    Blueberry,
    Fruit,
    Grapes,
    Orange,
    Pear,
    StrawBerry,

    // Special - Bomb Fruits
    Special,
    Bomb
}

public class Fruit : MonoBehaviour
{
    public FruitType fruitType = FruitType.Apple;
    public bool IsSpecialSpawned;

    [Header("-------- Grid Position --------")]
    public int x;
    public int y;

    [Header("-------- States --------")]
    public bool IsMatched;

    [Header("-------- Movement --------")]
    private Vector2 targetPosition;
    private float movementSpeed = 15f;

    [Header("-------- Vibration Effect --------")]
    private Coroutine vibrationCoroutine;
    private Vector3 vibrationOffset = Vector3.zero;

    [Header("-------- Other --------")]
    [SerializeField] GameObject destroyEffect;
    [SerializeField] GameObject borderEffect; // Effect played when a fruit is selected



    private void Update()
    {
        Vector2 newPosition = Vector2.MoveTowards(transform.position, targetPosition, movementSpeed * Time.deltaTime);
        transform.position = newPosition + (Vector2)vibrationOffset;
    }


    public void Create(int gridX, int gridY, bool IsSpecialFruit = false, bool IsBombFruit = false)
    {
        AssignGridPosition(gridX, gridY);
        transform.position = new Vector2(gridX, gridY + GameManager.instance.yOffset);
        targetPosition = new Vector2(gridX, gridY);       
    }

    public void AssignGridPosition(int gridX, int gridY)
    {
        x = gridX;
        y = gridY;
        targetPosition = new Vector2(gridX, gridY);

        //MoveToGridTarget
    }

    public bool IsMoving()
    {
        return Vector2.Distance((Vector2)transform.position - (Vector2)vibrationOffset, targetPosition) > 0.01f; 
    }

    public void SetSelected(bool isSelected)
    {
        

        if (borderEffect != null)
        {
            borderEffect.SetActive(isSelected);
        }

        if (isSelected)
        {          
            vibrationCoroutine ??= StartCoroutine(VibrationEffect());
        }
        else
        {           
            if (vibrationCoroutine != null)
            {
                StopCoroutine(vibrationCoroutine);
                vibrationCoroutine = null;
            }
            vibrationOffset = Vector2.zero;
        }

    }

    IEnumerator VibrationEffect()
    {
        float vibrationForce = 0.012f;
        float vibrationSpeed = 50f;

        while (true)
        {
            float t = Mathf.Sin(Time.time * vibrationSpeed) * vibrationForce;
            vibrationOffset = new Vector3(t, 0, 0); // Horizontal shake only
            yield return null;
        }
    }

    public IEnumerator DestroyAnimaton()
    {
        float time = 0.3f;
        float elapsedTime = 0f;
        Vector3 originalScale = transform.localScale;
       

        GameManager.instance.PlayDestroyEffect(transform.position);

        while (elapsedTime < time) // While the animation is still running
        {
            if (this == null) yield break;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / time;

            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);          
            yield return null;
        }

        Destroy(gameObject);
    }


}
