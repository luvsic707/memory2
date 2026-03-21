using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallControl : MonoBehaviour
{
    public Rigidbody[] balls;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            foreach(Rigidbody rb in balls)
            {
                Vector3 randomDir = Random.onUnitSphere * Random.Range(10,30);
                randomDir.y = 0;
                rb.linearVelocity = randomDir;
                rb.AddForce(transform.up * Random.Range(100, 1000));
            }
        }
    }
}
