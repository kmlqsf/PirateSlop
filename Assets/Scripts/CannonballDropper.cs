using UnityEngine;

public class CannonballDropper : MonoBehaviour
{
    public GameObject cannonballPrefab;
    public float dropInterval = 30f;
    private float timer = 0f;

    void Start()
    {
        timer = dropInterval;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= dropInterval)
        {
            timer = 0f;
            DropCannonball();
        }
    }

    void DropCannonball()
    {
        if (cannonballPrefab != null)
        {
            GameObject ball = Instantiate(cannonballPrefab, transform.position + Vector3.down * 0.5f, Quaternion.identity);
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }
        else
        {
            Debug.LogWarning("CannonballDropper: No cannonball prefab assigned!");
        }
    }
}
