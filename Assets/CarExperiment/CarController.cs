using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

public class CarController : UnitController
{

	public float Speed = 5f;
	public float TurnSpeed = 180f;
	public int Lap = 1;
	public int CurrentPiece, LastPiece;
	bool MovingForward = true;
	bool IsRunning;
	public float SensorRange = 10;
	int WallHits;
	IBlackBox box;

	// WEATHER CONDITIONS VARIABLES
	//	[Range (-1.0f, 1.0f)]
	//	public float gas = 0.0f;
	//	[Range (-1.0f, 1.0f)]
	//	public float steer = 0.0f;


	[Header ("Rain Settings")]
	[Range (0.0f, 1.0f)]
	public float rain = 0.5f;
	private float oldTurnAngle = -1;
	private int lostControlCounter = 0;
	bool lostControl = false;
	float lostControlTime = 0f;
	Vector3 lostControlStartPos;
	Vector3 lostControlEndPos;
	public float penaltyTime = 20f;
	public float penaltyDist = 2f;

	// Use this for initialization
	void Start ()
	{
	}
	
	// Update is called once per frame
	void FixedUpdate ()
	{
		//grab the input axes
		//var steer = Input.GetAxis("Horizontal");
		//var gas = Input.GetAxis("Vertical");

		////if they're hittin' the gas...
		//if (gas != 0)
		//{
		//    //take the throttle level (with keyboard, generally +1 if up, -1 if down)
		//    //  and multiply by speed and the timestep to get the distance moved this frame
		//    var moveDist = gas * speed * Time.deltaTime;

		//    //now the turn amount, similar drill, just turnSpeed instead of speed
		//    //   we multiply in gas as well, which properly reverses the steering when going 
		//    //   backwards, and scales the turn amount with the speed
		//    var turnAngle = steer * turnSpeed * Time.deltaTime * gas;

		//    //now apply 'em, starting with the turn           
		//    transform.Rotate(0, turnAngle, 0);

		//    //and now move forward by moveVect
		//    transform.Translate(Vector3.forward * moveDist);
		//}

		// Five sensors: Front, left front, left, right front, right
	
		if (IsRunning) {
			float frontSensor = 0;
			float leftFrontSensor = 0;
			float leftSensor = 0;
			float rightFrontSensor = 0;
			float rightSensor = 0;


			// Front sensor
			RaycastHit hit;
			if (Physics.Raycast (transform.position + transform.forward * 1.1f, transform.TransformDirection (new Vector3 (0, 0, 1).normalized), out hit, SensorRange)) {
				if (hit.collider.tag.Equals ("Wall")) {
					frontSensor = 1 - hit.distance / SensorRange;
				}
			}

			if (Physics.Raycast (transform.position + transform.forward * 1.1f, transform.TransformDirection (new Vector3 (0.5f, 0, 1).normalized), out hit, SensorRange)) {
				if (hit.collider.tag.Equals ("Wall")) {
					rightFrontSensor = 1 - hit.distance / SensorRange;
				}
			}

			if (Physics.Raycast (transform.position + transform.forward * 1.1f, transform.TransformDirection (new Vector3 (1, 0, 0).normalized), out hit, SensorRange)) {
				if (hit.collider.tag.Equals ("Wall")) {
					rightSensor = 1 - hit.distance / SensorRange;
				}
			}

			if (Physics.Raycast (transform.position + transform.forward * 1.1f, transform.TransformDirection (new Vector3 (-0.5f, 0, 1).normalized), out hit, SensorRange)) {
				if (hit.collider.tag.Equals ("Wall")) {
					leftFrontSensor = 1 - hit.distance / SensorRange;
				}
			}

			if (Physics.Raycast (transform.position + transform.forward * 1.1f, transform.TransformDirection (new Vector3 (-1, 0, 0).normalized), out hit, SensorRange)) {
				if (hit.collider.tag.Equals ("Wall")) {
					leftSensor = 1 - hit.distance / SensorRange;
				}
			}

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

	
			var moveDist = gas * Speed * Time.deltaTime;
//			var turnAngle = steer * TurnSpeed * Time.deltaTime * gas;
//
//			//transform.Translate (Vector3.forward * moveDist * rain);
//			transform.Rotate (new Vector3 (0, turnAngle, 0));
//			//			Debug.Log (turnAngle);
//			//transform.Rotate ((new Vector3 (0, turnAngle, 0) + (rain * new Vector3 (0, oldTurnAngle, 0))));
//			//oldTurnAngle = turnAngle;
//			transform.Translate (Vector3.forward * moveDist);

			//Debug.Log ("Gas: " + gas + " - Steer: " + steer + " - Together:" + Mathf.Abs (gas * steer));

			if (Mathf.Abs (gas * steer) > 1 - rain) { // Definde condition for when rain will make you slide depending on speed and steer
				
				InitiateLostControl ();
			} 


			if (lostControl) {
				PerformLostControl ();
			} else {
				//Debug.Log (moveDist);
				var turnAngle = steer * TurnSpeed * Time.deltaTime * gas;

				//transform.Translate (Vector3.forward * moveDist * rain);
				transform.Rotate (new Vector3 (0, turnAngle, 0));
				oldTurnAngle = turnAngle;

				//			Debug.Log (turnAngle);
				//transform.Rotate ((new Vector3 (0, turnAngle, 0) + (rain * new Vector3 (0, oldTurnAngle, 0))));
				//oldTurnAngle = turnAngle;
				transform.Translate (Vector3.forward * moveDist);	
			}
		}
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
		if (LastPiece > 2 && MovingForward) {
			Lap++;            
		}
	}

	public override float GetFitness ()
	{
		if (Lap == 1 && CurrentPiece == 0) {
			return 0;
		}
		int piece = CurrentPiece;
		if (CurrentPiece == 0) {
			piece = 17;
		}
		float fit = Lap * piece - WallHits * 0.2f - lostControlCounter * 1.0f;
		//  print(string.Format("Piece: {0}, Lap: {1}, Fitness: {2}", piece, Lap, fit));
		if (fit > 0) {
			return fit;
		}
		return 0;
	}

	void OnCollisionEnter (Collision collision)
	{
		if (collision.collider.tag.Equals ("Road")) {
			RoadPiece rp = collision.collider.GetComponent<RoadPiece> ();
			//  print(collision.collider.tag + " " + rp.PieceNumber);
            
			if ((rp.PieceNumber != LastPiece) && (rp.PieceNumber == CurrentPiece + 1 || (MovingForward && rp.PieceNumber == 0))) {
				LastPiece = CurrentPiece;
				CurrentPiece = rp.PieceNumber;
				MovingForward = true;                
			} else {
				MovingForward = false;
			}
			if (rp.PieceNumber == 0) {
				CurrentPiece = 0;
			}
		} else if (collision.collider.tag.Equals ("Wall")) {
			WallHits++;
		}
	}

	void InitiateLostControl ()
	{
		// Loose control
		lostControlCounter++;
		lostControlTime = Time.time;
		lostControlStartPos = this.transform.position;
		Vector3 fwd = transform.TransformDirection (Vector3.forward);
		RaycastHit hit;
		if (Physics.Raycast (transform.position, this.transform.forward, out hit, penaltyDist)) {
			lostControlEndPos = new Vector3 (this.transform.position.x + (this.transform.forward.x * hit.distance), this.transform.position.y, this.transform.position.z + (this.transform.forward.z * hit.distance));
		} else {
			lostControlEndPos = new Vector3 (this.transform.position.x + (this.transform.forward.x * penaltyDist), this.transform.position.y, this.transform.position.z + (this.transform.forward.z * penaltyDist));
		}
		lostControl = true;
	}

	void PerformLostControl ()
	{
		if ((Time.time - lostControlTime) >= penaltyTime) {
			Debug.Log ("Regained Control");
			lostControl = false;
		}
		float lerpVal = (Time.time - lostControlTime) / penaltyTime;
		//Debug.Log (lerpVal);

		transform.position = Vector3.Lerp (lostControlStartPos, lostControlEndPos, lerpVal);
		transform.Rotate (new Vector3 (0, oldTurnAngle, 0));
	}



	//void OnGUI()
	//{
	//    GUI.Button(new Rect(10, 200, 100, 100), "Forward: " + MovingForward + "\nPiece: " + CurrentPiece + "\nLast: " + LastPiece + "\nLap: " + Lap);
	//}
    
}
