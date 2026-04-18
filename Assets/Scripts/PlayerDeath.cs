using UnityEngine;

public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private Renderer capsuleRenderer;
    [SerializeField] private Material deadMaterial;
    
    private float tumbleSpeed;
    [SerializeField] private float fallSpeed = 5f;

    private bool isDead = false;
    private Vector3 tumbleAxis;

    public void Die()
    {
        tumbleSpeed = GetComponentInParent<playerController>().tumbleSpeed;
        if (isDead) return;
        isDead = true;

        // swap material
        capsuleRenderer.material = deadMaterial;

        // random tumble direction
        tumbleAxis = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;

        // disable player control
        GetComponent<playerController>().OnDisable();
    }

    void Update()
    {
        if (!isDead) return;

        // tumble
        transform.Rotate(tumbleAxis * tumbleSpeed * Time.deltaTime, Space.World);

        // fall
        controller.Move(Vector3.down * fallSpeed * Time.deltaTime);
    }
}