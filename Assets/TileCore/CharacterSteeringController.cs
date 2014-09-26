﻿using UnityEngine;

public class CharacterSteeringController : BindingBehaviour
{
    public void Start()
    {
        this.debugArrowShader = new Material(Shader.Find("GUI/Text Shader"));
        this.Face(new Vector2(0,-1));
    }

    #region Fields and properties
    Material debugArrowShader;
    [Bind]
#pragma warning disable 649
    private Animator animator;
#pragma warning restore 649

    /// <summary>
    /// The current location we're steering to.  If null, then character is stopped.
    /// </summary>
    private Vector2? targetLocation;

    private float maxSpeed;

    public float MaxForce = 1000;

    /// <summary>
    /// The current position of the character.
    /// </summary>
    public Vector2 Position
    {
        get
        {
            return transform.position;
        }
    }
    #endregion

    #region Externally callable control routines
    /// <summary>
    /// Stops the character.
    /// </summary>
    public void Stop()
    {
        targetLocation = null;
        rigidbody2D.velocity = Vector2.zero;
    }

    /// <summary>
    /// Switches the target of the seek behavior to be the specified location.
    /// </summary>
    /// <param name="target">New target location for Seek behavior</param>
    /// <param name="speed">Speed at which to drive in this direction.</param>
    public void Seek(Vector2 target, float speed)
    {
        targetLocation = target;
        maxSpeed = speed;
    }

    /// <summary>
    /// Face the specified direction, without moving.
    /// </summary>
    /// <param name="direction">Vector in the direction to face</param>
    public void Face(Vector2 direction)
    {
        this.SwitchToFacingAnimation(this.NearestCardinalDirection(direction.normalized));
    }
    #endregion

    #region Steering behaviors
    Vector2 SeekSteering()
    {
        if (targetLocation == null)
            return Vector2.zero;
        var offset = targetLocation.Value - Position;
        var distanceToTarget = offset.magnitude;
        if (distanceToTarget < 0.05f)
            return Vector2.zero;
        return offset * MaxForce / distanceToTarget;
    }


    private Vector2 PredictedPosition(Component sprite, float time)
    {
        return (Vector2)sprite.transform.position + time * sprite.rigidbody2D.velocity;
    }


    /// <summary>
    /// Connection to physics system.
    /// 
    /// Computes force and passes it on to the rigidBody2D
    /// Implements force can velocity caps.
    /// </summary>
    public void FixedUpdate()
    {
        var seekSteering = this.SeekSteering();

        //var force = this.MaybeAdd(seekSteering, collisionAvoidanceSteering);
        var force = seekSteering;

        // Throttle force at MaxForce
        var fMag = force.magnitude;
        if (fMag > MaxForce)
            force *= MaxForce / fMag;

        var rb = rigidbody2D;
        var vel = rb.velocity;
        var currentSpeed = vel.magnitude;

        // Inhibit acceleration in current motion direction if already at max speed.
        if (currentSpeed >= maxSpeed)
        {
            var heading = vel/currentSpeed;
            var forceInDirectionOfHeading = Vector2.Dot(heading, force);
            // Don't allow acceleration in the direction of our current motion
            if (forceInDirectionOfHeading > 0)
            {
                force -= heading * forceInDirectionOfHeading;
            }
        }

        BlueVector = seekSteering * 0.1f;
        GreenVector = force*0.1f;

        rb.AddForce(force);
    }

    #endregion

    #region Animation control
    private string currentState = "";
    private Vector2 currentDirection;
    private SpriteRenderer mySpriteRenderer;

    /// <summary>
    /// Connection to the animation system
    /// 
    /// Determines direction and speed of motion, and plays appropriate animation accordingly
    /// </summary>
    public void Update()
    {
        this.UpdateWalkAnimation(this.rigidbody2D.velocity);
        if (mySpriteRenderer == null)
             mySpriteRenderer = GetComponent<SpriteRenderer>();
        TileMap.UpdateSortingOrder(mySpriteRenderer);
    }

    Vector3 NearestCardinalDirection(Vector2 direction)
    {
        var result = direction;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            result.y = 0;
        else
            result.x = 0;
        return result.normalized;
    }

    private void UpdateWalkAnimation(Vector2 characterVelocity)
    {
        //this.animator.speed = characterVelocity.magnitude;

        if (characterVelocity.magnitude < 0.01)
        {
            if (!this.currentState.StartsWith("Face"))
            {
                this.currentState = "Face" + this.currentState;
                this.animator.CrossFade(this.currentState, 0f);
            }
        }
        else
        {
            var desiredDirection = characterVelocity.normalized;
            if (Vector2.Dot(currentDirection, desiredDirection) > 0.7f && !currentState.StartsWith("Face"))
                // Close enough; don't change it.
                return;

            this.currentDirection = this.NearestCardinalDirection(desiredDirection);
            if (this.currentDirection.x > 0)
                currentState = "East";
            else if (this.currentDirection.x < 0)
                currentState = "West";
            else if (this.currentDirection.y > 0)
                currentState = "North";
            else
                currentState = "South";
            this.animator.CrossFade(currentState, 0);
        }
    }

    private void SwitchToFacingAnimation(Vector2 direction)
    {
        currentDirection = direction;
        if (this.currentDirection.x > 0)
            currentState = "FaceEast";
        else if (this.currentDirection.x < 0)
            currentState = "FaceWest";
        else if (this.currentDirection.y > 0)
            currentState = "FaceNorth";
        else
            currentState = "FaceSouth";
        this.animator.CrossFade(currentState,0);
    }
    #endregion

    #region DebugDrawing
    public Vector2 RedVector;

    public Vector2 GreenVector;

    public Vector2 BlueVector;

    public bool DisplayDebugVectors;

    internal void OnRenderObject()
    {
        if (DisplayDebugVectors)
        {
            this.debugArrowShader.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            this.DrawArrowhead(RedVector, Color.red);
            this.DrawArrowhead(GreenVector, Color.green);
            this.DrawArrowhead(BlueVector, Color.blue);
            GL.End();
        }
    }

    private void DrawArrowhead(Vector2 vector, Color color)
    {
        if (vector != Vector2.zero)
        {
            var start = Position;
            var end = Position + vector;
            var perp = 0.05f * vector.PerpClockwise().normalized;
            GL.Color(color);
            GL.Vertex(start - perp);
            GL.Vertex(end);
            GL.Vertex(start + perp);
        }
    }
    #endregion
}
