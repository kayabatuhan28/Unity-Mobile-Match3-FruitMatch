using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        originalPosition = transform.localPosition;
    }

    public void Shake(float ShakeTime = 0.12f, float ShakeForce = 0.5f)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = StartCoroutine(StartShake(ShakeTime, ShakeForce));
    }

    IEnumerator StartShake(float ShakeTime, float ShakeForce)
    {
        float elapsedTime = 0f;

        while (elapsedTime < ShakeTime)
        {
            float offsetX = Random.Range(-1f, 1f) * ShakeForce;

            transform.localPosition =
                originalPosition + new Vector3(offsetX, 0f, 0f);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition;
        shakeCoroutine = null;
    }

}
