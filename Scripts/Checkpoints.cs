using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Checkpoints : MonoBehaviour
{
    public List<GameObject> checkpoints;

    private void Awake()
    {
        if (checkpoints == null) {
            checkpoints = new List<GameObject>();
            foreach (Transform child in transform) {
                checkpoints.Add(child.gameObject);
            }
        }
    }
}
