using System;
using System.Collections;
using UnityEngine;

public class PlayerUpgrades : MonoBehaviour
{
    private playerController pc;

    public event Action OnExplosion;
    private float debugRadius = 0f;
    private bool showGizmo = false;
    void Awake()
    {
        pc = GetComponent<playerController>();
    }

    public void Upgrade(UpgradeType upgradeType, float amount = 0f)
    {
        switch (upgradeType)
        {
            case UpgradeType.Speed: pc.ModifySpeed(amount); break;
            case UpgradeType.JumpHeight: pc.ModifyJumpHeight(amount); break;
            case UpgradeType.Health: /*pc.health += amount;*/ break;
            case UpgradeType.Damage: /*pc.damage += amount;*/ break;
            case UpgradeType.Explosion: OnExplosion?.Invoke(); break;
        }
    }

    public void TriggerExplosion(float radius = 5f, float minForce = 10f, float maxForce = -1f)
    {
        // if maxForce wasn't specified, default to minForce = constant force
        float resolvedMaxForce = maxForce < 0 ? minForce : maxForce;

        Collider[] hits = Physics.OverlapSphere(pc.gameObject.transform.position, radius);
        foreach (Collider hit in hits)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb == null) continue;

            float distance = Vector3.Distance(pc.gameObject.transform.position, hit.transform.position);
            float t = distance / radius; // 0 = center, 1 = edge

            // scales from maxForce at center to minForce at edge
            float force = Mathf.Lerp(resolvedMaxForce, minForce, t);

            Vector3 dir = hit.transform.position - transform.position;
            rb.AddForce(dir.normalized * force, ForceMode.Impulse);
            StartCoroutine(ShowGizmo(radius));
        }
        GetComponent<PlayerDeath>().Die();
    }
    IEnumerator ShowGizmo(float radius)
    {
        debugRadius = radius;
        showGizmo = true;
        yield return new WaitForSeconds(1f);
        showGizmo = false;
    }

    void OnDrawGizmos()
    {
        if (!showGizmo) return;
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f); // orange, semi transparent
        Gizmos.DrawWireSphere(pc.gameObject.transform.position, debugRadius);
        Gizmos.DrawSphere(pc.gameObject.transform.position, debugRadius); // filled for visibility
    }
}