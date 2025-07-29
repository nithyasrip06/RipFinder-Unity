using UnityEngine;

public class TextSpawner : MonoBehaviour
{
    public GameObject floatingTextPrefab;

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            if (floatingTextPrefab == null)
            {
                Debug.LogError("[TextSpawner] No prefab assigned!");
                return;
            }
            // Spawn 1 meter in front of the user's view
            Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 1.0f;
            Quaternion rotation = Quaternion.LookRotation(spawnPos - Camera.main.transform.position);
            GameObject instance = Instantiate(floatingTextPrefab, spawnPos, rotation);
            Debug.Log("[TextSpawner] Spawned text at " + spawnPos);
        }
    }
}
