using UnityEngine;

public class MoneyCollectible : MonoBehaviour
{
    [Header("Money")]
    public int moneyValue = 10;

    [Header("Floating")]
    public float floatHeight = 0.25f;
    public float floatSpeed = 2f;

    [Header("Spinning")]
    public float spinSpeed = 90f;

    [Header("Effects")]
    public ParticleSystem collectParticles;
    public AudioClip collectSound;

    private Vector3 startPosition;
    private bool collected = false;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // Float up and down
        float newY =
            startPosition.y +
            Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        transform.position =
            new Vector3(
                transform.position.x,
                newY,
                transform.position.z
            );

        // Spin
        transform.Rotate(
            Vector3.up,
            spinSpeed * Time.deltaTime,
            Space.World
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        if (other.CompareTag("Player"))
        {
            collected = true;

            // Add money
            MoneyManager moneyManager =
                FindFirstObjectByType<MoneyManager>();

            if (moneyManager != null)
            {
                moneyManager.AddMoney(moneyValue);
            }

            // Particles
            if (collectParticles != null)
            {
                ParticleSystem particles =
                    Instantiate(
                        collectParticles,
                        transform.position,
                        Quaternion.identity
                    );

                particles.Play();

                Destroy(
                    particles.gameObject,
                    particles.main.duration + 1f
                );
            }

            // Sound
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(
                    collectSound,
                    transform.position
                );
            }

            // Remove money
            Destroy(gameObject);
        }
    }
}