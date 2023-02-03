using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElipticalMotion : MovingPlatform
{
    [SerializeField] public float h; // x offset
    [SerializeField] public float k; // y offset
    [SerializeField] [Min(0)] public float a; // x scale
    [SerializeField] [Min(0)] public float b; // y sacle
    [SerializeField] [Range(-1,1)] public int t; // -1 clockwise, 1 anti-clockwise


    float x, y;
    float prev_x, prev_y;


    public Vector3 moveVect;
    public Vector3 velocity;

    float rads;
    const float repUnit = 2 * Mathf.PI;


    void calculateVelocity()
    {
        rads += t * _speedScaler * Time.deltaTime; // Gives us r in the equation of an elipse
        x = a * Mathf.Cos(rads) + h;               // Gives next x position by, xScalar*radius + xCentre
        y = b * Mathf.Sin(rads) + k;               // Gives next y position by, yScalar*radius + yCentre

        dx = x - prev_x;                           // Gives dx
        dy = y - prev_y;                           // Gives dx

        prev_x = x;                                // makes previous x = x in time for next run
        prev_y = y;                                // makes previous y = y in time for next run

        moveVect = new Vector3(x, y);
    }

    private void Move()
    {
        velocity = gameObject.transform.position;
        gameObject.transform.position = moveVect;
        velocity -= gameObject.transform.position;
    }

    void Start()
    {
        h = gameObject.transform.position.x;
        k = gameObject.transform.position.y;
    }

    // Update is called once per frame
    void Update()
    {
        calculateVelocity();
        Move();
    }
}
