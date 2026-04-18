using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class GameStartTrigger : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Only objects with this tag can start the game.")]
    public string playerTag = "Player";

    [Header("References")]
    public PhysicsLaneSpawner laneSpawner;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip grandmaScreamSfx;

    public AudioSource musicSource;
    public float musicStartDelay = 0f;

    [Header("Timing")]
    [Tooltip("Delay before hazard spawning begins after the trigger is hit.")]
    public float spawnStartDelay = 0f;

    [Header("Extra Events")]
    public UnityEvent onGameStart;

    private bool hasStarted = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasStarted) return;
        if (!other.CompareTag(playerTag)) return;

        hasStarted = true;
        StartCoroutine(BeginGameSequence());
    }

    IEnumerator BeginGameSequence()
    {
        if (sfxSource != null && grandmaScreamSfx != null)
        {
            sfxSource.PlayOneShot(grandmaScreamSfx);
        }

        onGameStart?.Invoke();

        if (musicSource != null)
        {
            if (musicStartDelay > 0f)
            {
                yield return new WaitForSeconds(musicStartDelay);
            }

            musicSource.Play();
        }

        if (spawnStartDelay > 0f)
        {
            yield return new WaitForSeconds(spawnStartDelay);
        }

        if (laneSpawner != null)
        {
            laneSpawner.StartSpawning();
        }

        // Optional: disable trigger after use
        gameObject.SetActive(false);
    }
}