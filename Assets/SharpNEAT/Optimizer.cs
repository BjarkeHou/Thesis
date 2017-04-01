using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;
using System.Collections.Generic;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using System;
using System.Xml;
using System.IO;

public class Optimizer : MonoBehaviour
{

	public bool carKnowsDrag = false;
	public bool randomDrag = false;

	int NUM_INPUTS = 5;
	int NUM_OUTPUTS = 2;

	public int Trials;
	public float TrialDuration;
	public float StoppingFitness;
	[Range (0, 20)]
	public int evoSpeed = 20;
	public string folder_prefix = "test_1";
	bool EARunning;

	string popFileSavePath, champFileSavePath;

	SimpleExperiment experiment;
	static NeatEvolutionAlgorithm<NeatGenome> _ea;

	public GameObject Unit;
	public GameObject[] trackStartPositions;

	// Running map
	bool mapRunning;
	List<GameObject> runningCars;
	float stopwatch = 0.0f;
	int bestKeyInMap = -1;
	float bestFitnessInMap = -1.0f;
	int maxKey;
	int minKey;
	float highTesting = 0.75f;
	float lowTesting = 0.25f;
	bool searching = true;

	// Running tests
	public float trialDurationPerNetwork = 500.0f;
	float testDrag = 1.0f;
	bool testRunning = false;
	int networkCounter = 1;
	float testStopwatch = 0.0f;
	float searchStopwatch = 0.0f;
	NeatGenome intelligentNetwork;
	NeatGenome unIntelligentNetwork;
	StreamWriter sw;

	private GameObject StartPos;

	Dictionary<IBlackBox, UnitController> ControllerMap = new Dictionary<IBlackBox, UnitController> ();
	private DateTime startTime;
	private float timeLeft;
	private float accum;
	private int frames;
	private float updateInterval = 12;

	private uint Generation;
	private float Fitness;
	private float bestFitness = 0.0f;
	private float MeanFitness;
	private float ChampAvgSpeed;
	private int bestPiece = 0;
	private int bestCounter = 0;

	private Dictionary<int, NeatGenome> map;
	private int mapLength;

	// Use this for initialization
	void Start ()
	{
		Utility.DebugLog = true;

		champFileSavePath = Application.persistentDataPath + string.Format ("/{0}/best.xml", folder_prefix);
		popFileSavePath = Application.persistentDataPath + string.Format ("/{0}/pop.xml", folder_prefix); 

		StartPos = trackStartPositions [0];
	}


	void SetupNewExperiment ()
	{
		experiment = new SimpleExperiment ();
		XmlDocument xmlConfig = new XmlDocument ();
		TextAsset textAsset = (TextAsset)Resources.Load ("experiment.config");
		xmlConfig.LoadXml (textAsset.text);
		experiment.SetOptimizer (this);

		experiment.Initialize ("test1", xmlConfig.DocumentElement, NUM_INPUTS, NUM_OUTPUTS);
	}

	// Update is called once per frame
	void Update ()
	{
		
		CheckFPS ();       



		if (testRunning) {

			TestUpdate ();
		}




	}

	void RunTest ()
	{

		SetupNewExperiment ();

		map = new Dictionary<int, NeatGenome> ();
		runningCars = new List<GameObject> ();
		loadMap ();
		maxKey = int.MinValue;
		minKey = int.MaxValue;

		foreach (int key in map.Keys) {
			if (key > maxKey)
				maxKey = key;
			if (key < minKey)
				minKey = key;
		}
		print ("Lowest Key: " + minKey + " - Highest Key: " + maxKey);

		InstantiateCar (map [ConvertPercentageToKey (0.5f)]);
		InstantiateCar (map [ConvertPercentageToKey (lowTesting)]);
		InstantiateCar (map [ConvertPercentageToKey (highTesting)]);

		foreach (GameObject car in runningCars) {
			car.GetComponent<Rigidbody> ().drag = testDrag;
		}

		mapRunning = true;

		// Load the two seperate networks.


//		unIntelligentNetwork = loadGenome ("unknown");


		Time.timeScale = 1;

//		maxKey = int.MinValue;
//		minKey = int.MaxValue;

//		foreach (int key in map.Keys) {
//			if (key > maxKey)
//				maxKey = key;
//			if (key < minKey)
//				minKey = key;
		// File writer
		sw = File.CreateText (Application.persistentDataPath + string.Format ("/{0}/map_testResults.txt", folder_prefix));
		testRunning = true;
	}

	void TestUpdate ()
	{


		if (testDrag >= 0.25f) {

			if (searching) {
				if (MapSearchUpdate ()) {
					InstantiateCar (map [bestKeyInMap]);
					foreach (GameObject car in runningCars) {
						car.GetComponent<Rigidbody> ().drag = testDrag;
					}

				}
			} else if (testStopwatch > trialDurationPerNetwork) {
				UnitController car = runningCars [0].GetComponent<UnitController> ();

				float fitness = car.GetFitness ();
				float bestLapTime = car.GetBestLapTime ();
				float avgSpeed = car.GetAvgSpeed ();
				int piecesTraveled = car.GetRoadPiecesTraveled ();
				int wallhits = car.GetWallHits ();

				string log = string.Format ("{0};{1};{2};{3};{4};{5};{6}", testDrag, fitness, bestLapTime, avgSpeed, piecesTraveled, wallhits, searchStopwatch);
				sw.WriteLine (log);
				print (log);

				testDrag -= 0.1000000000f;
				Destroy (runningCars [0]);
				runningCars.Clear ();
				//InstantiateCar (intelligentNetwork);
//				runningCars [0].GetComponent<Rigidbody> ().drag = testDrag;
				testStopwatch = 0.0f;
				searchStopwatch = 0.0f;
				searching = true;

				maxKey = int.MinValue;
				minKey = int.MaxValue;

				foreach (int key in map.Keys) {
					if (key > maxKey)
						maxKey = key;
					if (key < minKey)
						minKey = key;
				}
				print ("Lowest Key: " + minKey + " - Highest Key: " + maxKey);

				InstantiateCar (map [ConvertPercentageToKey (0.5f)]);
				InstantiateCar (map [ConvertPercentageToKey (lowTesting)]);
				InstantiateCar (map [ConvertPercentageToKey (highTesting)]);

				foreach (GameObject go in runningCars) {
					go.GetComponent<Rigidbody> ().drag = testDrag;
				}
			} 
		} else {
			testRunning = false;
			sw.Close ();
			print ("TEST DONE GET GOING!");
		}


		testStopwatch += Time.deltaTime;
		
	}

	bool MapSearchUpdate ()
	{
		stopwatch += Time.deltaTime;
		searchStopwatch += Time.deltaTime;
		if (stopwatch > TrialDuration && searching) {
			stopwatch = 0.0f;
			if (runningCars.Count == 3) {
				bestFitnessInMap = runningCars [0].GetComponent<UnitController> ().GetFitness ();
				bestKeyInMap = ConvertPercentageToKey (0.5f);
				Destroy (runningCars [0]);
				runningCars.RemoveAt (0);
			}
			UnitController car1 = runningCars [0].GetComponent<UnitController> ();
			UnitController car2 = runningCars [1].GetComponent<UnitController> ();
			Debug.Log (car1.GetFitness () + " from " + ConvertPercentageToKey (0.25f) + " vs. " + car2.GetFitness () + " from " + ConvertPercentageToKey (0.75f));
			if (car1.GetFitness () > car2.GetFitness ()) {
				if (car1.GetFitness () > bestFitnessInMap) {
					bestFitnessInMap = car1.GetFitness ();
					bestKeyInMap = ConvertPercentageToKey (0.25f);
				}
				maxKey = (int)(maxKey - ((maxKey - minKey) * 0.5f));
			} else {
				if (car2.GetFitness () >= bestFitnessInMap) {
					bestFitnessInMap = car2.GetFitness ();
					bestKeyInMap = ConvertPercentageToKey (0.75f);
				}
				minKey = (int)(minKey + ((maxKey - minKey) * 0.5f));
			}
			Debug.Log ("Best Fitness: " + bestFitnessInMap + " at " + bestKeyInMap);
			ControllerMap.Clear ();
			foreach (GameObject car in runningCars) {
				Destroy (car);
			}
			runningCars.Clear ();
			if (ConvertPercentageToKey (lowTesting) != ConvertPercentageToKey (highTesting)) {
				InstantiateCar (map [ConvertPercentageToKey (lowTesting)]);
				InstantiateCar (map [ConvertPercentageToKey (highTesting)]);

				foreach (GameObject car in runningCars) {
					car.GetComponent<Rigidbody> ().drag = testDrag;
				}
			} else {
//				InstantiateCar (map [bestKeyInMap]);
				Debug.Log ("Found best car in map at " + bestKeyInMap);
				searching = false;
				return true;
			}
		}

		return false;
	}

	void CheckFPS ()
	{
		Time.timeScale = evoSpeed;
		//  evaluationStartTime += Time.deltaTime;
		timeLeft -= Time.deltaTime;
		accum += Time.timeScale / Time.deltaTime;
		++frames;
		if (timeLeft <= 0.0) {
			var fps = accum / frames;
			timeLeft = updateInterval;
			accum = 0.0f;
			frames = 0;
			//   print("FPS: " + fps);
			if (fps < 10) {
				evoSpeed = evoSpeed - 1;
				//Time.timeScale = Time.timeScale - 1;
				print ("Lowering time scale to " + evoSpeed);
			}
		}
	}

	public void StartEA ()
	{        
		if (carKnowsDrag) {
			NUM_INPUTS = 6;
		} 
		if (!randomDrag) {
			Trials = 1;
		}
			
		SetupNewExperiment ();

		//		mapLength = (int)((maxRain - minRain) / rainInterval);
//		map = new NeatGenome[1];
		//loadBest ();


		// print("Loading: " + popFileLoadPath);
		_ea = experiment.CreateEvolutionAlgorithm (popFileSavePath);
		startTime = DateTime.Now;

		_ea.UpdateEvent += new EventHandler (ea_UpdateEvent);
		_ea.PausedEvent += new EventHandler (ea_PauseEvent);

		//   Time.fixedDeltaTime = 0.045f;
		_ea.StartContinue ();
		EARunning = true;
	}


	void loadMap ()
	{
		string path = Application.persistentDataPath + string.Format ("/{0}/map/", folder_prefix);

		DirectoryInfo dir = new DirectoryInfo (path);
		if (dir.Exists) {

			FileInfo[] info = dir.GetFiles ("*.xml");

			foreach (FileInfo f in info) {
				int key = int.Parse (f.Name.Substring (0, f.Name.Length - 4));

				try {
					using (XmlReader xr = XmlReader.Create (path + f.Name))
						map.Add (key, NeatGenomeXmlIO.ReadCompleteGenomeList (xr, false, (NeatGenomeFactory)experiment.CreateGenomeFactory ()) [0]);


				} catch (Exception e1) {
					print (" Error loading genome from file!\nLoading aborted.\n" + e1.Message);
					continue;
				}	

			}
		}
	}

	void SetupNewMAPExperiment ()
	{
		map = new Dictionary<int, NeatGenome> ();//NeatGenome[10];
		experiment = new SimpleExperiment ();

		XmlDocument xmlConfig = new XmlDocument ();
		TextAsset textAsset = (TextAsset)Resources.Load ("experiment.config");
		xmlConfig.LoadXml (textAsset.text);
		experiment.SetOptimizer (this);

		experiment.Initialize ("test1", xmlConfig.DocumentElement, NUM_INPUTS, NUM_OUTPUTS);
		loadMap ();
	}

	public void StartMAP2 ()
	{
		if (carKnowsDrag) {
			NUM_INPUTS = 6;
		} 
		if (!randomDrag) {
			Trials = 1;
		}


		//TODO SetupNewExperiment skal fixes til MAP Elites
		SetupNewMAPExperiment ();



		// print("Loading: " + popFileLoadPath);
		_ea = experiment.CreateEvolutionAlgorithm (popFileSavePath);
		startTime = DateTime.Now;

		_ea.UpdateEvent += new EventHandler (ea_UpdateEventMAP);
		_ea.PausedEvent += new EventHandler (ea_PauseEvent);
		IList<NeatGenome> unTestedGenomes = null;

		if (map.Values.Count == 0)
			unTestedGenomes = _ea.GenomeList;
		else
			unTestedGenomes = new List<NeatGenome> (map.Values);

		//Loop this:
		//		while (firstRun) {
		List<NeatGenome> children = new List<NeatGenome> ();

		for (int i = 0; i < 50; i++) {
			NeatGenome mom = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
			NeatGenome dad = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
			NeatGenome child = mom.CreateOffspring (dad, _ea.CurrentGeneration);
			children.Add (child);
		}

		print ("Test length: " + children.Count);
		_ea.GenomeList = children;
		_ea.StartContinueMAP2 ();
	}


	int convertAvgSpeedToKey (float avgSpeed)
	{
		return (int)(avgSpeed * 10);
	}

	void ea_UpdateEvent (object sender, EventArgs e)
	{
		Utility.Log (string.Format ("gen={0:N0} bestFitness={1:N6}",
			_ea.CurrentGeneration, _ea.Statistics._maxFitness));
		
		Fitness = (float)_ea.Statistics._maxFitness;
		MeanFitness = (float)_ea.Statistics._meanFitness;
		if (bestFitness < Fitness) {

			//map [getIndex (rain)] = _ea.CurrentChampGenome;

			XmlWriterSettings _xwSettings = new XmlWriterSettings ();
			_xwSettings.Indent = true;
			// Save genomes to xml file.        
			DirectoryInfo dirInf = new DirectoryInfo (Application.persistentDataPath + string.Format ("/{0}", folder_prefix));
			if (!dirInf.Exists) {
				Debug.Log ("Creating subdirectory");
				dirInf.Create ();
			}

			using (XmlWriter xw = XmlWriter.Create (champFileSavePath, _xwSettings)) {
				experiment.SavePopulation (xw, new NeatGenome[] { _ea.CurrentChampGenome });
			}

			bestFitness = Fitness;
			Debug.Log ("New best saved: " + bestFitness);
			TrialDuration = bestFitness * 50.0f > 70.0f ? bestFitness * 50.0f : 70.0f;
			if (TrialDuration > 450.0f)
				TrialDuration = 450.0f;
		}
			
		Generation = _ea.CurrentGeneration;
		ChampAvgSpeed = _ea.CurrentChampGenome.EvaluationInfo.AvgSpeed;

	}

	void SaveGenome (NeatGenome genome, string fileName)
	{
		XmlWriterSettings _xwSettings = new XmlWriterSettings ();
		_xwSettings.Indent = true;

		string path = Application.persistentDataPath + string.Format ("/{0}/map/{1}/", folder_prefix, Generation);
		// Save genomes to xml file.        
		DirectoryInfo dirInf = new DirectoryInfo (path);
		if (!dirInf.Exists) {
			Debug.Log ("Creating subdirectory");
			dirInf.Create ();
		}
		using (XmlWriter xw = XmlWriter.Create (path + fileName + ".xml", _xwSettings)) {
			experiment.SavePopulation (xw, new NeatGenome[] {
				genome
			});
		}
	}

	void ea_UpdateEventMAP (object sender, EventArgs e)
	{
		Utility.Log (string.Format ("gen={0:N0} bestFitness={1:N6}",
			_ea.CurrentGeneration, _ea.Statistics._maxFitness));

		Generation = _ea.CurrentGeneration;

		IList<NeatGenome> testedGenomes = _ea.GenomeList;
		print ("TestedGenomes = " + testedGenomes.Count);

		foreach (NeatGenome genome in testedGenomes) {
			int key = convertAvgSpeedToKey (genome.EvaluationInfo.AvgSpeed);
			if (key == -1)
				continue;
			if (map.ContainsKey (key)) {
				if (map [key] != null) {
					if (genome.EvaluationInfo.Fitness > map [key].EvaluationInfo.Fitness) {
						Debug.Log ("New Best! Key: " + key + " Old: " + map [key].EvaluationInfo.Fitness + " New: " + genome.EvaluationInfo.Fitness);
						map [key] = genome;
						SaveGenome (genome, key.ToString ());

					}
				} else {
					map [key] = genome;
					SaveGenome (genome, key.ToString ());
					Debug.Log ("New Added! Key: " + key + " New: " + genome.EvaluationInfo.Fitness);
				}
			} else {
				map.Add (key, genome);
				SaveGenome (genome, key.ToString ());
				Debug.Log ("New Added! Key: " + key + " New: " + genome.EvaluationInfo.Fitness);
			}
		}
		Debug.Log ("Map size: " + map.Values.Count);
		foreach (KeyValuePair<int,NeatGenome> value in map) {
			//Now you can access the key and value both separately from this attachStat as:
			Debug.Log ("Key: " + value.Key + " - Fitness: " + value.Value.EvaluationInfo.Fitness);
		}

		IList<NeatGenome> unTestedGenomes = new List<NeatGenome> (map.Values);

		//Loop this:
		//		while (firstRun) {
		List<NeatGenome> children = new List<NeatGenome> ();

		for (int i = 0; i < 50; i++) {
			NeatGenome mom = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
			NeatGenome dad = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
			NeatGenome child = mom.CreateOffspring (dad, _ea.CurrentGeneration);
			children.Add (child);
		}
			
		_ea.GenomeList = children;

		Fitness = (float)_ea.Statistics._maxFitness;
		MeanFitness = (float)_ea.Statistics._meanFitness;
		if (bestFitness < Fitness) {

			//map [getIndex (rain)] = _ea.CurrentChampGenome;

			XmlWriterSettings _xwSettings = new XmlWriterSettings ();
			_xwSettings.Indent = true;
			// Save genomes to xml file.        
			DirectoryInfo dirInf = new DirectoryInfo (Application.persistentDataPath + string.Format ("/{0}", folder_prefix));
			if (!dirInf.Exists) {
				Debug.Log ("Creating subdirectory");
				dirInf.Create ();
			}

			using (XmlWriter xw = XmlWriter.Create (champFileSavePath, _xwSettings)) {
				experiment.SavePopulation (xw, new NeatGenome[] { _ea.CurrentChampGenome });
			}

			bestFitness = Fitness;
			Debug.Log ("New best saved: " + bestFitness);
			TrialDuration = bestFitness * 50.0f > 70.0f ? bestFitness * 50.0f : 70.0f;
			if (TrialDuration > 450.0f)
				TrialDuration = 450.0f;
		}


		ChampAvgSpeed = _ea.CurrentChampGenome.EvaluationInfo.AvgSpeed;

	}


	private NeatGenome loadGenome (string filename)
	{
		NeatGenome genome = null;

		DirectoryInfo dir = new DirectoryInfo (Application.persistentDataPath + string.Format ("/{0}", folder_prefix));
		if (dir.Exists) {
			
		
			FileInfo[] info = dir.GetFiles (filename + ".xml");
			print (info.Length);
			foreach (FileInfo f in info) {
			
				try {

					using (XmlReader xr = XmlReader.Create (Application.persistentDataPath + string.Format ("/{0}/{1}", folder_prefix, f.Name)))
						genome = NeatGenomeXmlIO.ReadCompleteGenomeList (xr, false, (NeatGenomeFactory)experiment.CreateGenomeFactory ()) [0];


				} catch (Exception e1) {
					print (" Error loading genome from file!\nLoading aborted.\n" + e1.Message);
					continue;
				}	

			}
//				Debug.Log ("Filled map with " + map + " elites.");
//			}
		}
			
		      

		return genome;
	}


	void ea_PauseEvent (object sender, EventArgs e)
	{
		Time.timeScale = 1;
		Utility.Log ("Done ea'ing (and neat'ing)");

		XmlWriterSettings _xwSettings = new XmlWriterSettings ();
		_xwSettings.Indent = true;
		// Save genomes to xml file.        
		DirectoryInfo dirInf = new DirectoryInfo (Application.persistentDataPath + string.Format ("/{0}", folder_prefix));
		if (!dirInf.Exists) {
			Debug.Log ("Creating subdirectory");
			dirInf.Create ();
		}
		using (XmlWriter xw = XmlWriter.Create (popFileSavePath, _xwSettings)) {
			experiment.SavePopulation (xw, _ea.GenomeList);
		}
		// Also save the best genome

//		using (XmlWriter xw = XmlWriter.Create (champFileSavePath, _xwSettings)) {
//			experiment.SavePopulation (xw, new NeatGenome[] { _ea.CurrentChampGenome });
//		}
		DateTime endTime = DateTime.Now;
		Utility.Log ("Total time elapsed: " + (endTime - startTime));

		System.IO.StreamReader stream = new System.IO.StreamReader (popFileSavePath);
       

      
		EARunning = false;            
	}

	public void StopEA ()
	{

		if (_ea != null && _ea.RunState == SharpNeat.Core.RunState.Running) {
			_ea.Stop ();
		}
	}



	public void StopMAP ()
	{
		if (_ea != null && _ea.RunState == SharpNeat.Core.RunState.Running) {
			_ea.Stop ();
		}
	}

	public void Evaluate (IBlackBox box)
	{
		GameObject obj = Instantiate (Unit, StartPos.transform.position, StartPos.transform.rotation) as GameObject;
		UnitController controller = obj.GetComponent<UnitController> ();

		ControllerMap.Add (box, controller);

		controller.Activate (box);
	}

	public void StopEvaluation (IBlackBox box)
	{
		UnitController ct = ControllerMap [box];

		Destroy (ct.gameObject);
	}

	public void RunMap ()
	{
		SetupNewExperiment ();
		Time.timeScale = 1;
		map = new Dictionary<int, NeatGenome> ();
		runningCars = new List<GameObject> ();
		loadMap ();
		maxKey = int.MinValue;
		minKey = int.MaxValue;

		foreach (int key in map.Keys) {
			if (key > maxKey)
				maxKey = key;
			if (key < minKey)
				minKey = key;
		}
		print ("Lowest Key: " + minKey + " - Highest Key: " + maxKey);

		InstantiateCar (map [ConvertPercentageToKey (0.5f)]);
		InstantiateCar (map [ConvertPercentageToKey (lowTesting)]);
		InstantiateCar (map [ConvertPercentageToKey (highTesting)]);

		mapRunning = true;
	}

	void InstantiateCar (NeatGenome genome)
	{
		// Get a genome decoder that can convert genomes to phenomes.
		var genomeDecoder = experiment.CreateGenomeDecoder ();
		// Decode the genome into a phenome (neural network).
		// 30-((30-0)*(1-0,75))
		var phenome = genomeDecoder.Decode (genome);
		GameObject car = Instantiate (Unit, StartPos.transform.position, StartPos.transform.rotation) as GameObject;
		//obj.GetComponent<CarController> ().rain = minRain + (bestCounter * rainInterval);
		UnitController controller = car.GetComponent<UnitController> ();
		runningCars.Add (car);
		ControllerMap.Add (phenome, controller);
		controller.Activate (phenome);
	}

	int ConvertPercentageToKey (float percentage)
	{
		return (int)(maxKey - ((maxKey - minKey) * (1.0f - percentage)));
	}

	public void StopRunMap ()
	{
		mapRunning = false;
		ControllerMap.Clear ();
		foreach (GameObject car in runningCars) {
			Destroy (car);
		}
		runningCars.Clear ();
	}

	public float GetFitness (IBlackBox box)
	{
		if (ControllerMap.ContainsKey (box)) {
			return ControllerMap [box].GetFitness ();
		}
		return 0;
	}

	public float GetAvgSpeed (IBlackBox box)
	{
		if (ControllerMap.ContainsKey (box)) {
			return ControllerMap [box].GetAvgSpeed ();
		}
		return 0;
	}

	void OnGUI ()
	{
		if (GUI.Button (new Rect (10, 10, 100, 40), "Start EA")) {
			StartEA ();
		}
		if (GUI.Button (new Rect (10, 60, 100, 40), "Stop EA")) {
			StopEA ();
		}
		if (GUI.Button (new Rect (10, 110, 100, 40), "Run Map")) {
			RunMap ();
		}
		if (GUI.Button (new Rect (10, 160, 100, 40), "Stop Map")) {
			StopRunMap ();
		}
		if (GUI.Button (new Rect (10, 210, 100, 40), "Start Test")) {
			RunTest ();
		}
//		if (GUI.Button (new Rect (10, 210, 100, 40), "Start MAP Training")) {
//			StartMAP2 ();
//		}
//		if (GUI.Button (new Rect (10, 260, 100, 40), "Stop MAP Training")) {
//			StopMAP ();
//		}

		GUI.Button (new Rect (10, Screen.height - 140, 140, 100), string.Format ("Generation: {0}\nFitness: {1:0.00}\nBestFitness: {2:0.00}\nMeanFitness: {3:0.00}\nBest AvgSpeed: {4:0.00}\n", Generation, Fitness, bestFitness, MeanFitness, ChampAvgSpeed));
	}

	//	class MapEntry
	//	{
	//		private NeatGenome _genome;
	//		private float _fitness;
	//
	//		public MapEntry (NeatGenome genome, float fitness)
	//		{
	//			_genome = genome;
	//			_fitness = fitness;
	//		}
	//
	//		public NeatGenome Genome {
	//			get { return _genome; }
	//			set { _genome = value; }
	//		}
	//
	//		public float Fitness {
	//			get { return _fitness; }
	//			set { _fitness = value; }
	//		}
	//	}
}
