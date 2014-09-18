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
    public GameObject KaviPrefab;

    public int MaxBeers = 1;
    public int MaxKavis = 1;

    IEnumerator Destroyer (GameObject beer) {
        yield return new WaitForSeconds(5f);
        GameObject.Destroy(beer);
    }
        
    void Update () {
        Ensure(MaxBeers, "Beer", BeerPrefab);
        Ensure(MaxKavis, "Kavi", KaviPrefab);
    }

    void Ensure (int max, string tag, GameObject prefab) {
        GameObject[] candidates = GameObject.FindGameObjectsWithTag(tag);

        int count = 0;
        foreach (GameObject c in candidates) {
            if (c.renderer.enabled) {
                count++;
            } else {
                // this.StartCoroutine(Destroyer(beer));
            }
        }

        if (count < max) {
            GameObject beer = Instantiate(prefab) as GameObject;
            beer.transform.position = new Vector3(UnityEngine.Random.Range(-2, 4), UnityEngine.Random.Range(-6, -2), 0f);
        }
    }
}

