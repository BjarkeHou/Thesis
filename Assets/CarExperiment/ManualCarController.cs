using UnityEngine;
using System.Collections;

public class ManualCarController : MonoBehaviour
{

	public float Speed = 1f;
	public float TurnSpeed = 30f;
	public int Lap = 1;

	bool MovingForward = true;

	bool lostControl = false;
	float lostControlTime = 0f;
	Vector3 lostControlStartPos;
	Vector3 lostControlEndPos;
	float penaltyTime = 20f;
	float penaltyDist = 2f;
	int WallHits;

	float oldDirection;

	// Use this for initialization
	void Start ()
	{

	}
	
	
	// Update is called once per frame
	void Update ()
	{
		float moveDist = 0;
		float turnAngle = 0;


		if (Input.GetKeyDown (KeyCode.Space)) {
			InitiateLostControl ();
		} 


		if (lostControl) {
			PerformLostControl ();

		} else {
			if (Input.GetKey (KeyCode.UpArrow)) {
				moveDist = Speed * Time.deltaTime;
			}


			if (Input.GetKey (KeyCode.LeftArrow)) {
				turnAngle = -(TurnSpeed * Time.deltaTime);
			} else if (Input.GetKey (KeyCode.RightArrow)) {
				turnAngle = TurnSpeed * Time.deltaTime;
			}
			// Proceed as normal
			transform.Rotate (new Vector3 (0, turnAngle, 0));

			transform.Translate (Vector3.forward * moveDist);	
			oldDirection = turnAngle;
		}

	}

	void InitiateLostControl ()
	{
		// Loose control
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
		Debug.Log ("Lost Control..");
	}

	void PerformLostControl ()
	{
		if ((Time.time - lostControlTime) >= penaltyTime) {
			Debug.Log ("Regained Control");
			lostControl = false;
		}
		float lerpVal = (Time.time - lostControlTime) / penaltyTime;
		Debug.Log (lerpVal);
		if (this.transform.position.y > 1 || -1 > this.transform.position.y)
			Debug.Log ("ARGH!");
		transform.position = Vector3.Lerp (lostControlStartPos, lostControlEndPos, lerpVal);
		transform.Rotate (new Vector3 (0, oldDirection, 0));
	}
}
