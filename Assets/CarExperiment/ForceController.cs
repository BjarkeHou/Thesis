using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;


public class ForceController : UnitController
{
	public bool randomDrag = false;
	public bool knowsDrag = false;
	public bool manuel = false;
	public float thrust;
	public float rotationSpeed;
	private Rigidbody rb;

	private int maxRoadPieces = 24;

	public float WallPunishment = 1.0f;

	public float steer;
	public float gas;

	public int Lap = 1;
	public bool passedWaypoint = false;
	public int CurrentPiece, LastPiece;
	bool MovingForward = true;
	bool IsRunning;

	float distanceTraveled = 0.0f;
	float timeExisted = 0.0f;
	Vector3 lastPosition;
	//	float averageVelocity = 0;
	//	int velocityCounter = 0;

	public float SensorRange = 10;
	int WallHits;
	IBlackBox box;

	// Use this for initialization
	void Start ()
	{
		rb = GetComponent<Rigidbody> ();
		if (randomDrag) {
			rb.drag = (Random.Range (3, 11) / 10.0f);
		}
		lastPosition = this.transform.position;
	}
	
	// Update is called once per frame
	void FixedUpdate ()
	{

		if (manuel) {
			if (Input.GetKey (KeyCode.J) && !Input.GetKey (KeyCode.L)) {
				transform.Rotate (0, -rotationSpeed, 0);
				//rb.AddForce (Vector3.Normalize (-transform.right + transform.forward) * thrust);
			} else if (!Input.GetKey (KeyCode.J) && Input.GetKey (KeyCode.L)) {
				//rb.AddForce (Vector3.Normalize (transform.right + transform.forward) * thrust);
				transform.Rotate (0, rotationSpeed, 0);
			} 	

			if (Input.GetKey (KeyCode.Space))
				rb.AddForce (Vector3.Normalize (transform.forward) * thrust);
			else if (Input.GetKey (KeyCode.K))
				rb.AddForce (Vector3.Normalize (-transform.forward) * 2 * thrust);
		} else {
			if (!IsRunning) {
				return;
			}

			float frontSensor = 0;
			float leftFrontSensor = 0;
			float leftSensor = 0;
			float rightFrontSensor = 0;
			float rightSensor = 0;

			// Front sensor

			frontSensor = getSensor (new Vector3 (0, 0, 1).normalized);
			leftFrontSensor = getSensor (new Vector3 (-0.5f, 0, 1).normalized);
			leftSensor = getSensor (new Vector3 (-1, 0, 0).normalized);
			rightFrontSensor = getSensor (new Vector3 (0.5f, 0, 1).normalized);
			rightSensor = getSensor (new Vector3 (1, 0, 0).normalized);

			//print (leftSensor + "\t" + leftFrontSensor + "\t" + frontSensor + "\t" + rightFrontSensor + "\t" + rightSensor);

			ISignalArray inputArr = box.InputSignalArray;
			inputArr [0] = frontSensor;
			inputArr [1] = leftFrontSensor;
			inputArr [2] = leftSensor;
			inputArr [3] = rightFrontSensor;
			inputArr [4] = rightSensor;
			if (knowsDrag)
				inputArr [5] = rb.drag;

			box.Activate ();

			ISignalArray outputArr = box.OutputSignalArray;

			steer = (float)outputArr [0] * 2 - 1;
			gas = (float)outputArr [1] * 2 - 1;
//		if (steer < 0 && gas > 0)
//			print (steer + " - " + gas);
			transform.Rotate (0, (steer * rotationSpeed), 0);
			rb.AddForce (Vector3.Normalize (transform.forward) * thrust * gas);

			// Calculate Speed
			distanceTraveled += Vector3.Distance (lastPosition, this.transform.position);
			lastPosition = this.transform.position;
			timeExisted += Time.fixedDeltaTime;
//			averageVelocity += rb.velocity.magnitude;
//			velocityCounter++;
		}
	}

	public override float GetAvgSpeed ()
	{
		return distanceTraveled / timeExisted;
	}

	private float getSensor (Vector3 direction)
	{
		RaycastHit hit;
		//Debug.DrawRay (transform.position + transform.forward * 1.1f, transform.TransformDirection (direction.normalized));
		if (Physics.Raycast (transform.position + transform.forward * 1.1f, transform.TransformDirection (direction.normalized), out hit, SensorRange)) {
			if (hit.collider.tag.Equals ("Wall")) {
				return (1 - hit.distance / SensorRange);
			}
		}

		return 0;
	}

	public override void Stop ()
	{
		this.IsRunning = false;
	}

	public override void Activate (IBlackBox box)
	{
		this.box = box;
		this.IsRunning = true;
	}

	public void NewLap ()
	{        
		if (LastPiece > 2 && MovingForward && passedWaypoint) {
			Lap++;
			passedWaypoint = false;
		}
	}

	public override float GetFitness ()
	{
		if (Lap == 1 && CurrentPiece == 0) {
			return 0;
		}
		int piece = CurrentPiece;
		if (CurrentPiece == 0) {
			piece = maxRoadPieces;
		}
		float fit = ((Lap - 1) * maxRoadPieces) + (piece) - (WallHits * WallPunishment);// - lostControlCounter * 0.5f;
//		if (WallHits < piece)
//			fit = fit + fit;
		//  print(string.Format("Piece: {0}, Lap: {1}, Fitness: {2}", piece, Lap, fit));
		if (fit > 0) {
			return fit;
		}
		return 0;
	}

	public int GetHighestPiece ()
	{
		return (Lap - 1) * maxRoadPieces + CurrentPiece;
	}

	void OnTriggerEnter (Collider collision)
	{
		if (collision.GetComponent<Collider> ().tag.Equals ("Road")) {
			RoadPiece rp = collision.GetComponent<Collider> ().GetComponent<RoadPiece> ();
			//  print(collision.collider.tag + " " + rp.PieceNumber);

			if ((rp.PieceNumber != LastPiece) && (rp.PieceNumber == CurrentPiece + 1 || (MovingForward && rp.PieceNumber == 0))) {
				LastPiece = CurrentPiece;
				if (CurrentPiece == 10)
					passedWaypoint = true;
				CurrentPiece = rp.PieceNumber;
				MovingForward = true;                
			} else {
				MovingForward = false;
			}
			if (rp.PieceNumber == 0) {
				CurrentPiece = 0;
				NewLap ();
			}
		}  
	}

	void OnCollisionEnter (Collision collision)
	{
		if (collision.collider.tag.Equals ("Wall")) {
			WallHits++;
		}
	}

	void OnDestroy ()
	{
//		if (GetHighestPiece ()) {
		//print (GetHighestPiece () + " - " + Lap + " - " + WallHits);
//			print ("Average Speed: " + (averageVelocity / velocityCounter));
//		print ("Average Speed: " + GetAvgSpeed ());
//		}
			

	}
}
