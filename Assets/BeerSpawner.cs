using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//
//
// FOR TESTING ONLY
[AddComponentMenu("Sims/Beer Spawner")]
public class BeerSpawner : MonoBehaviour
{
    public GameObject BeerPrefab;

    IEnumerator Destroyer (GameObject beer) {
        yield return new WaitForSeconds(5f);
        GameObject.Destroy(beer);
    }

    void Update () {
        GameObject[] candidates = GameObject.FindGameObjectsWithTag("Beer");

        int count = 0;
        foreach (GameObject beer in candidates) {
            if (beer.renderer.enabled) {
                count++;
            } else {
                // this.StartCoroutine(Destroyer(beer));
            }
        }

        if (count == 0) {
            Debug.Log("Out of beer");
            GameObject beer = Instantiate(BeerPrefab) as GameObject;
            beer.transform.position = new Vector3(UnityEngine.Random.Range(-2, 4), UnityEngine.Random.Range(-6, -2), 0f);
        }
    }
}

