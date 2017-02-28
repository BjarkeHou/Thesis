using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;


public class ForceController : UnitController
{

	public float thrust;
	public float rotationSpeed;
	private Rigidbody rb;

	private int maxRoadPieces = 24;

	public float WallPunishment = 1.0f;

	public int Lap = 1;
	public bool passedWaypoint = false;
	public int CurrentPiece, LastPiece;
	bool MovingForward = true;
	bool IsRunning;

	float averageVelocity = 0;
	int velocityCounter = 0;

	public float SensorRange = 10;
	int WallHits;
	IBlackBox box;

	// Use this for initialization
	void Start ()
	{
		rb = GetComponent<Rigidbody> ();
	}
	
	// Update is called once per frame
	void FixedUpdate ()
	{
		if (!IsRunning) {
			return;
		}

//		if (Input.GetKey (KeyCode.J) && !Input.GetKey (KeyCode.L)) {
//			transform.Rotate (0, -rotationSpeed, 0);
//			//rb.AddForce (Vector3.Normalize (-transform.right + transform.forward) * thrust);
//		} else if (!Input.GetKey (KeyCode.J) && Input.GetKey (KeyCode.L)) {
//			//rb.AddForce (Vector3.Normalize (transform.right + transform.forward) * thrust);
//			transform.Rotate (0, rotationSpeed, 0);
//		} 	
//
//		if (Input.GetKey (KeyCode.Space))
//			rb.AddForce (Vector3.Normalize (transform.forward) * thrust);
//		else if (Input.GetKey (KeyCode.K))
//			rb.AddForce (Vector3.Normalize (-transform.forward) * 2 * thrust);

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

		ISignalArray inputArr = box.InputSignalArray;
		inputArr [0] = frontSensor;
		inputArr [1] = leftFrontSensor;
		inputArr [2] = leftSensor;
		inputArr [3] = rightFrontSensor;
		inputArr [4] = rightSensor;

		box.Activate ();

		ISignalArray outputArr = box.OutputSignalArray;

		var steer = (float)outputArr [0] * 2 - 1;
		var gas = (float)outputArr [1] * 2 - 1;

		transform.Rotate (0, (steer * rotationSpeed), 0);
		rb.AddForce (Vector3.Normalize (transform.forward) * thrust * gas);

		averageVelocity += rb.velocity.magnitude;
		velocityCounter++;
	}

	private float getSensor (Vector3 direction)
	{
		RaycastHit hit;
		if (Physics.Raycast (transform.position + transform.forward * 1.1f, transform.TransformDirection (new Vector3 (-1, 0, 0).normalized), out hit, SensorRange)) {
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
		if (WallHits < 2)
			fit = fit + fit;
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
		if (GetHighestPiece () > 6) {
			//print (GetHighestPiece () + " - " + Lap + " - " + WallHits);
			print ("Average Speed: " + (averageVelocity / velocityCounter));
		}
			

	}
}
