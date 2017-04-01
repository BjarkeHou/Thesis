using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

public abstract class UnitController : MonoBehaviour
{

	public abstract void Activate (IBlackBox box);

	public abstract void Stop ();

	public abstract float GetFitness ();

	public abstract float GetAvgSpeed ();

	public abstract float GetBestLapTime ();

	public abstract int GetRoadPiecesTraveled ();

	public abstract int GetWallHits ();
}
