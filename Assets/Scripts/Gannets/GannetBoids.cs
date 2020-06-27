using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GannetBoids : MonoBehaviour {

/*
Each Predator (member of the swarm) will have this script attached to it
The script will be independent for each predator however, i.e every Predator 
needs their own position, direction, velocity, etc.
*/

/*
Settings is a "ScriptableObject" which allows each predator to obtain its 
starting psettings without creating 50 new instances of the class.
For more info: https://docs.unity3d.com/Manual/class-ScriptableObject.html
*/
    GannetSettings psettings;

//These are the values of the predator that update every frame

    public Vector3 velocity;
    public Vector3 acceleration;
    public Vector3 position; //Current position (x,y,z)
    public Vector3 forward; //Current Direction (x,y,z)
    public int howManyPerceived; //How many predators can this predator see 
    public Vector3 avgSwarmDirection; //Of all the predators in the perception radius, what is the average direction  
    public Vector3 avgAvoidanceDirection; //For each perceived predator, there will be an avoidance direction to turn away from that predator, this is the average of avoidance for all perceived predators.
    public Vector3 otherPredatorCoordinates; //The centre Coordinate of all perceived predators.
    public Material material; //this predators Material
    public Transform predatorTransform; //We "Cache" the transform for optimisation
    public Transform target;
    public bool predator;
    public bool isInWater=false;
    public float timer;
    private float noBreath = 5;
    private float maxView = 50;
    private bool keepFly = false;
    private float speeds = 1;
    //Awake happens before anything
    void Awake () {
        material = transform.GetComponentInChildren<MeshRenderer>().material; //Get the material component of this predator
        predatorTransform = transform; //Set the Cache
        if (predator) this.material.color = Color.yellow;
    }

    
    //We can Initialize the predator with Settings from our Settings Script
    public void Initialize (GannetSettings psettings) {
        this.psettings = psettings; //Get a new reference of the psettings just for this predator
        position = predatorTransform.position; //Initialize the start position
        forward = predatorTransform.forward; //Initialize the start direction
        float startSpeed = (psettings.minimumSpeed + psettings.maximumSpeed) / 2; //Initialize the start speed
        velocity = transform.forward * startSpeed; //Initialize the velocity
        //body.velocity = velocity;
    }


    //Update is called every frame
    void Update()
    {
        //If this predator is the target then change its colour to red
        if (ThirdPersonCamera.Target == this.gameObject)
        {
            this.material.color = Color.red;
        }
        if (isInWater)
        {
            timer += Time.deltaTime;
            if (timer > noBreath)
            {
                Debug.Log("憋不住了");
            }
        }
        else
        {
            timer -= Time.deltaTime ;
            if (timer < 0) timer = 0;
        }

        if (timer>=noBreath) target = null;
        else if (ClosestPrey() != null) target = ClosestPrey().transform;

        //if (target != null)
        //{
        //    Debug.Log(target);
        //}
    }

    public void MovePredator () {
        

       acceleration = Vector3.zero; //Start with an acceleration of 0

        if (target != null) {
            Vector3 offsetToTarget = (target.position - position);
            acceleration = MoveTowards (offsetToTarget) * psettings.targetWeight;
        }

        if (howManyPerceived != 0) {

            //Divide the sum of the other predators coordinates with how many there are to get the average,
            //This will give back the centre of all perceived predators
            otherPredatorCoordinates /= howManyPerceived; 

            //We need to get the difference between this predators location and the swarms centre location
            Vector3 offsetToSwarmCentre = (otherPredatorCoordinates - position);

            //Cohesion - Move towards the Centre of the swarm, based on the weight
            var cohesion = MoveTowards (offsetToSwarmCentre) * psettings.cohesionWeight; 

            //Alignment - Move towards the average swarms direction, based on the weight
            var alignment = MoveTowards (avgSwarmDirection) * psettings.alignmentWeight;

            //Seperation - Move towards the avoidance direction, based on the weight
            var seperation = MoveTowards (avgAvoidanceDirection) * psettings.seperateWeight;

            //Add these new values to the acceleration of the predator
            acceleration += cohesion;
            acceleration += alignment;
            acceleration += seperation;
        }

        //Check whether the predator is heading for an obstacle
        if (HeadingForObstacle ()) {

            //if it is then get the closest direction that doesnt intersect with an obstacle
            Vector3 avoidCollisionRay = ObstacleDirections ();
            //And move towards that direction
            Vector3 avoidCollisionForce = MoveTowards (avoidCollisionRay) * psettings.avoidCollisionWeight;
            acceleration += avoidCollisionForce;
        }

        //Some physics equations
        // V - m/s,  a - m/s^2, T - s
        velocity += acceleration * Time.deltaTime;

        //Speed is the scalar of the velocity vector
        float speed = velocity.magnitude;
        // 
        Vector3 direction = velocity / speed;

        //Make sure the speed doesn't exceed the min or max speeds
        speed = Mathf.Clamp (speed, psettings.minimumSpeed, psettings.maximumSpeed);
        //Update the velocity to effectively clamp the velocity as well
        velocity = direction * speed*speeds;
        if (timer >= noBreath)
        {
            keepFly = false;
            if (predatorTransform.position.y < 8)
            {
                predatorTransform.position += speed * Vector3.up * Time.deltaTime;
                //Set the new direction of the predator
                predatorTransform.forward = Vector3.up;
                //Set the new position of the predator
                position = predatorTransform.position;
                forward = Vector3.up;
            }
            else
            {
                // X - m, V - m/s, T = s
                predatorTransform.position += velocity * Time.deltaTime;
                //Set the new direction of the predator
                predatorTransform.forward = direction;
                //Set the new position of the predator
                position = predatorTransform.position;
                forward = direction;
            }
           
        }
        else if (timer < noBreath && target == null)
        {
            if (predatorTransform.position.y < 8)
                keepFly = true;
            else keepFly = false;
            //if (predatorTransform.position.y > 7 && predatorTransform.position.y < 7.5f)
            //    velocity.y = Random.Range(-0.5f, 1.5f) ;
            predatorTransform.position += velocity * Time.deltaTime; ;
            //Set the new direction of the predator
            predatorTransform.forward = direction;
            //Set the new position of the predator
            position = predatorTransform.position;
            forward = direction;
        }
        else 
        {
            keepFly = false;
            // X - m, V - m/s, T = s
            predatorTransform.position += velocity * Time.deltaTime;
            //Set the new direction of the predator
            predatorTransform.forward = direction;
            //Set the new position of the predator
            position = predatorTransform.position;
            forward = direction;
        }
        
    }

    Vector3 MoveTowards (Vector3 vector) {
        //Normalizing the vector just gives the direction towards it, not the length, 
        //so we can make sure it can only travels the max speed towards it
        Vector3 v = vector.normalized * psettings.maximumSpeed - velocity;
        //https://docs.unity3d.com/ScriptReference/Vector3.ClampMagnitude.html
        return Vector3.ClampMagnitude (v, psettings.maxSteerForce);
    }

    bool HeadingForObstacle() {
        //What did the raycast hit?
        RaycastHit hit;
        //If it does hit something, return true
        if (Physics.SphereCast (position, psettings.raycastDisplacementRadius, forward, out hit, psettings.collisionAvoidDst, psettings.obstacleMask)) {
            return true;
        } else 
            return false;
    }

    Vector3 ObstacleDirections() {
        //An array of all the directions the predators can move (300)
        Vector3[] rayDirections = Helper.directions;
        //Iterate through all the directions
        for (int i = 0; i < rayDirections.Length; i++) {
            Vector3 dir = predatorTransform.TransformDirection (rayDirections[i]);
            //Shoot a ray from this direction
            Ray ray = new Ray (position, dir);
            //If it Doesn't hit anything (an obstacle), then return that directions
            if (!Physics.SphereCast (ray, psettings.raycastDisplacementRadius, psettings.collisionAvoidDst, psettings.obstacleMask)) {
                if (keepFly)
                {
                    dir.y = Random.Range(0.1f, 1f) ;
                }
                return dir;
            }
        }
        //If somehow everything around it is an obstacle then just move forward
        //We just need this so that this function always returns something
        return forward;
    }

    GameObject ClosestPrey()
    {
        GameObject[] gos;
        gos = GameObject.FindGameObjectsWithTag("Boid");
        GameObject closest = null;
        float distance =maxView;
        Vector3 position = transform.position;
        foreach (GameObject go in gos)
        {
            Vector3 diff = go.transform.position - position;
            float curDistance = diff.sqrMagnitude;
            if (curDistance < distance)
            {
                closest = go;
                distance = curDistance;
            }
        }
        
        return closest;
        
    }

    private void OnTriggerExit(Collider other)
    {
         isInWater = true;
        speeds = 0.6f;
    }
    private void OnTriggerEnter(Collider other)
    {
        isInWater = false;
        speeds = 1;
    }

}