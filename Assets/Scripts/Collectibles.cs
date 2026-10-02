/*********************************************************************************************
 * COMPONENT OF: money prefab (Assets/edited object/money.prefab)
 * REQUIRED DEPENDENCIES: MoneyManager in the scene, Box Collider with Is Trigger on,
 *                        Player tagged "Player" with a Rigidbody, (optional) money particle
 *                        prefab and collect sound
 * DESCRIPTION: Makes the money float up and down and spin. When the Player touches it, it
 *              adds moneyValue to the MoneyManager, plays particles and a sound, then
 *              removes itself.
 * AUTHOR: Ben Xiao
 * VERSION: 1.0
 * VERSION 1.1: Add a behavior so that the Collectible rotates slowly about the y-axis.
 *********************************************************************************************/
using UnityEngine;

public class MoneyCollectible : MonoBehaviour
{
    [Header("Money")]
    public int moneyValue = 10;

    [Header("Floating")]
    public float floatHeight = 0.25f;
    public float floatSpeed = 2f;

    [Header("Spinning")]
    // How fast the money turns, in degrees per second. Set in the Inspector.
    // 90 = one full turn every 4 seconds. Lower = slower.
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
        // Update only calls other methods. Each method has one job.
        Float();
        Rotate();
    }

    // Moves the money up and down in a smooth wave around its starting height
    private void Float()
    {
        float newY =
            startPosition.y +
            Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        transform.position =
            new Vector3(
                transform.position.x,
                newY,
                transform.position.z
            );
    }

    // VERSION 1.1: Turns the money slowly around the y-axis (the up-and-down axis)
    private void Rotate()
    {
        // Vector3.up is the y-axis. spinSpeed * Time.deltaTime is how many degrees to turn
        // this frame, so the speed stays the same on fast and slow computers.
        // Space.World spins around the world's y-axis, even if the money model is tilted.
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
                FindAnyObjectByType<MoneyManager>();

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