using UnityEngine;
using System.Collections;

public class PhysicsLaneSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Lane
    {
        public string laneName;
        public Transform spawnPoint;
    }

    [Header("Hazard Prefabs")]
    public GameObject[] hazardPrefabs;

    [Header("4 Lanes")]
    public Lane[] lanes;

    [Header("Spawn Timing")]
    public float minSpawnDelay = 2f;
    public float maxSpawnDelay = 3f;

    [Header("Lane Control")]
    public bool avoidSameLaneTwice = true;
    [Range(0f, 1f)] public float sameLaneRepeatChance = 0.1f;

    [Header("Double Spawn")]
    [Range(0f, 1f)] public float doubleSpawnChance = 0.08f;

    [Header("Launch Force")]
    public float minLaunchForce = 12f;
    public float maxLaunchForce = 18f;

    [Header("Horizontal Spread")]
    [Tooltip("Left/right spread in degrees.")]
    public float coneAngle = 5f;

    [Header("Vertical Spread")]
    [Tooltip("Up/down throw angle variation in degrees.")]
    public float verticalConeAngle = 3f;

    [Header("Extra Upward Toss")]
    public float minUpwardBoost = 1f;
    public float maxUpwardBoost = 3f;

    [Header("Spin")]
    public float minTorque = 0f;
    public float maxTorque = 0.5f;

    [Header("Downhill Direction")]
    [Tooltip("Direction hazards should generally travel across the slope. Keep Y at 0.")]
    public Vector3 downhillDirection = new Vector3(0f, 0f, -1f);

    private int lastLaneIndex = -1;
    private bool spawning = true;

    void Start()
    {
        if (lanes == null || lanes.Length != 4)
        {
            Debug.LogWarning("PhysicsLaneSpawner works best with exactly 4 lanes.");
        }

        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (spawning)
        {
            SpawnHazard();
            float delay = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(delay);
        }
    }

    void SpawnHazard()
    {
        if (hazardPrefabs == null || hazardPrefabs.Length == 0)
        {
            Debug.LogWarning("No hazard prefabs assigned.");
            return;
        }

        if (lanes == null || lanes.Length == 0)
        {
            Debug.LogWarning("No lanes assigned.");
            return;
        }

        int firstLaneIndex = ChooseLaneIndex();
        SpawnSingleHazard(firstLaneIndex);

        lastLaneIndex = firstLaneIndex;

        bool shouldDoubleSpawn = lanes.Length > 1 && Random.value < doubleSpawnChance;

        if (shouldDoubleSpawn)
        {
            int secondLaneIndex = ChooseDifferentLaneIndex(firstLaneIndex);
            SpawnSingleHazard(secondLaneIndex);
        }
    }

    void SpawnSingleHazard(int laneIndex)
    {
        Lane lane = lanes[laneIndex];

        if (lane.spawnPoint == null)
        {
            Debug.LogWarning("Lane " + laneIndex + " is missing a spawn point.");
            return;
        }

        GameObject prefab = hazardPrefabs[Random.Range(0, hazardPrefabs.Length)];

        GameObject spawned = Instantiate(
            prefab,
            lane.spawnPoint.position,
            Quaternion.identity
        );

        PhysicsHazard hazard = spawned.GetComponent<PhysicsHazard>();
        if (hazard != null)
        {
            float launchForce = Random.Range(minLaunchForce, maxLaunchForce);
            float upwardBoost = Random.Range(minUpwardBoost, maxUpwardBoost);
            float torqueAmount = Random.Range(minTorque, maxTorque);

            hazard.Launch(
                downhillDirection,
                launchForce,
                coneAngle,
                verticalConeAngle,
                upwardBoost,
                torqueAmount
            );
        }
        else
        {
            Debug.LogWarning("Spawned hazard is missing PhysicsHazard script.");
        }
    }

    int ChooseLaneIndex()
    {
        if (lanes.Length == 1)
            return 0;

        int newIndex = Random.Range(0, lanes.Length);

        if (!avoidSameLaneTwice)
            return newIndex;

        if (lastLaneIndex == -1)
            return newIndex;

        if (Random.value < sameLaneRepeatChance)
            return newIndex;

        int safety = 0;
        while (newIndex == lastLaneIndex && safety < 20)
        {
            newIndex = Random.Range(0, lanes.Length);
            safety++;
        }

        return newIndex;
    }

    int ChooseDifferentLaneIndex(int excludedLaneIndex)
    {
        int newIndex = Random.Range(0, lanes.Length);

        int safety = 0;
        while (newIndex == excludedLaneIndex && safety < 20)
        {
            newIndex = Random.Range(0, lanes.Length);
            safety++;
        }

        return newIndex;
    }
}