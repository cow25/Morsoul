using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject spawnPoint = GameObject.FindWithTag("PlayerSpawn");
        if (spawnPoint != null)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = spawnPoint.transform.position;
            transform.rotation = spawnPoint.transform.rotation;
            if (cc != null) cc.enabled = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
