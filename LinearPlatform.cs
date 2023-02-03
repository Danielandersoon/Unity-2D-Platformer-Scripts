using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinearPlatform : MovingPlatform
{

    public Vector3 _pos1; 
    public Vector3 _pos2;

    private float _xPrev;
    private float _yPrev;

    private float _xVel;
    private float _yVel;

    private float _xNextPos;
    private float _yNextPos;

    float t;

    private bool positive = true;

    private Vector3 _normalizedVect;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(_pos1, _pos2);
    }

    Vector3 calculateVelocity()
    {
        t += Time.deltaTime;

        var velocityVect = new Vector3(_xVel * Time.deltaTime, _yVel * Time.deltaTime);
        
        if (positive)
            return velocityVect;
        else
            return (velocityVect * -1);

    }

    void move(Vector3 moveVect)
    {
        transform.position += moveVect;
    }

    void Start()
    {
        t = 0;
        transform.position = _pos1;
        _xPrev = transform.position.x;
        _yPrev = transform.position.y;
        _normalizedVect = Vector3.Normalize(_pos2 - _pos1) * _speedScaler;
        _xVel = _normalizedVect.x;
        _yVel = _normalizedVect.y;
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.position.x >= _pos2.x)
            positive = false;
        if (transform.position.x <= _pos1.x)
            positive = true;
        
        _xPrev = transform.position.x;
        _yPrev = transform.position.y;

        move(calculateVelocity());

        dx = transform.position.x - _xPrev;
        dy = transform.position.y - _yPrev;
    }
}
