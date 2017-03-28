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
	bool mapRunning;
	string popFileSavePath, champFileSavePath;

	SimpleExperiment experiment;
	static NeatEvolutionAlgorithm<NeatGenome> _ea;

	public GameObject Unit;
	public GameObject[] trackStartPositions;

	//	public enum Tracks
	//	{
	//		Track1,
	//		Track2
	//	}
	//
	//	public Tracks SelectedTrack;
	//	private int trackCounter = 0;
	//	public int switchTrackAfterGames = 50;
	//	public int switchRainAfterGames = 200;
	//	public float minRain = 0.0f;
	//	public float maxRain = 0.5f;
	//	public float rainInterval = 0.05f;
	//	private float rain;
		

	private GameObject StartPos;

	Dictionary<IBlackBox, UnitController> ControllerMap = new Dictionary<IBlackBox, UnitController> ();
	private DateTime startTime;
	private float timeLeft;
	private float accum;
	private int frames;
	private float updateInterval = 12;

	private uint Generation;
	private float Fitness;
	private float bestFitness = 0;
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
				print (key);
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

	public IEnumerator StartMAP ()
	{
		if (carKnowsDrag) {
			NUM_INPUTS = 6;
		} 
		if (!randomDrag) {
			Trials = 1;
		}

		mapRunning = true;

		//TODO SetupNewExperiment skal fixes til MAP Elites
		SetupNewMAPExperiment ();

		//		mapLength = (int)((maxRain - minRain) / rainInterval);
		map = new Dictionary<int, NeatGenome> ();//NeatGenome[10];



		// print("Loading: " + popFileLoadPath);
		_ea = experiment.CreateEvolutionAlgorithm (popFileSavePath);
		startTime = DateTime.Now;

		_ea.UpdateEvent += new EventHandler (ea_UpdateEvent);
		_ea.PausedEvent += new EventHandler (ea_PauseEvent);

		IList<NeatGenome> unTestedGenomes = _ea.GenomeList;
		bool firstRun = true;

		//Loop this:
//		while (firstRun) {
		List<NeatGenome> children = new List<NeatGenome> ();
		if (firstRun) {
			for (int i = 0; i < 1; i++) {
				NeatGenome mom = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
				NeatGenome dad = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
				NeatGenome child = mom.CreateOffspring (dad, _ea.CurrentGeneration);
				children.Add (child);
			}
			firstRun = false;
		} else {
				
//				for (int i = 0; i < 10; i++) {
//					
//					NeatGenome mom = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
//					NeatGenome dad = unTestedGenomes [UnityEngine.Random.Range (0, unTestedGenomes.Count)];// = new NeatGenome (); // Should be selected random from the map.
//					NeatGenome child = mom.CreateOffspring (dad, _ea.CurrentGeneration);
//					children.Add (child);
//				}
		}
		//Get 2 random genomes


		// Create offspring like this
		// (Maybe do this with a lot of genomes at once, for optimization)

		print ("Test length: " + children.Count);
		yield return StartCoroutine (_ea.EvaluateList (children));

		IList<NeatGenome> testedGenomes = _ea.GenomeList;
		print ("TestedGenomes = " + testedGenomes.Count);

		foreach (NeatGenome genome in testedGenomes) {
			int key = convertAvgSpeedToKey (genome.EvaluationInfo.AvgSpeed);
			if (key == -1)
				continue;
			if (map.ContainsKey (key)) {
				if (map [key] != null) {
					if (genome.EvaluationInfo.Fitness > map [key].EvaluationInfo.Fitness) {
						map [key] = genome;
					}
				} else {
					map [key] = genome;
				}
			} else {
				map.Add (key, genome);
			}
		}
		foreach (KeyValuePair<int,NeatGenome> value in map) {
			//Now you can access the key and value both separately from this attachStat as:
			Debug.Log ("Key: " + value.Key);
			Debug.Log ("Fitness: " + value.Value.EvaluationInfo.Fitness);
		}
//		}
	}

	public void StartMAP2 ()
	{
		if (carKnowsDrag) {
			NUM_INPUTS = 6;
		} 
		if (!randomDrag) {
			Trials = 1;
		}

		mapRunning = true;

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



		// Create offspring like this
		// (Maybe do this with a lot of genomes at once, for optimization)

		print ("Test length: " + children.Count);
		_ea.GenomeList = children;
		_ea.StartContinueMAP2 ();

//		IList<NeatGenome> testedGenomes = _ea.GenomeList;
//		print ("TestedGenomes = " + testedGenomes.Count);
//
//		foreach (NeatGenome genome in testedGenomes) {
//			int key = convertAvgSpeedToKey (genome.EvaluationInfo.AvgSpeed);
//			if (key == -1)
//				continue;
//			if (map.ContainsKey (key)) {
//				if (map [key] != null) {
//					if (genome.EvaluationInfo.Fitness > map [key].EvaluationInfo.Fitness) {
//						map [key] = genome;
//					}
//				} else {
//					map [key] = genome;
//				}
//			} else {
//				map.Add (key, genome);
//			}
//		}
//		foreach (KeyValuePair<int,NeatGenome> value in map) {
//			//Now you can access the key and value both separately from this attachStat as:
//			Debug.Log ("Key: " + value.Key);
//			Debug.Log ("Fitness: " + value.Value.EvaluationInfo.Fitness);
//		}
//		//		}
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

 
//		if (Generation % switchTrackAfterGames == 0 && Generation != 0 && trackStartPositions.Length > 1) {
//			trackCounter++;
//			StartPos = trackStartPositions [trackCounter];
//			if (trackCounter >= trackStartPositions.Length) {
//				trackCounter = 0;
//			}
//		}

//		if ((Generation % switchRainAfterGames == 0 || bestFitness > StoppingFitness) && Generation != 0) {
//			if (rain == maxRain) {
//				// STOP
//				StopEA ();
//			} else {
//				StopEA ();
//
//				rain += rainInterval;
//				Unit.GetComponent<CarController> ().rain = rain;
//
//				bestFitness = 0;
//
//				champFileSavePath = Application.persistentDataPath + string.Format ("/{1}/{0:0.00}.best.xml", rain, folder_prefix);
//				popFileSavePath = Application.persistentDataPath + string.Format ("/{1}/{0:0.00}.pop.xml", rain, folder_prefix);  		
//
//				SetupNewExperiment ();
//				StartEA ();
//
//			}
//		} 


		//    Utility.Log(string.Format("Moving average: {0}, N: {1}", _ea.Statistics._bestFitnessMA.Mean, _ea.Statistics._bestFitnessMA.Length));

    
	}

	void SaveGenome (NeatGenome genome, string fileName)
	{
		XmlWriterSettings _xwSettings = new XmlWriterSettings ();
		_xwSettings.Indent = true;

		string path = Application.persistentDataPath + string.Format ("/{0}/map/", folder_prefix);
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

		IList<NeatGenome> testedGenomes = _ea.GenomeList;
		print ("TestedGenomes = " + testedGenomes.Count);

		foreach (NeatGenome genome in testedGenomes) {
			int key = convertAvgSpeedToKey (genome.EvaluationInfo.AvgSpeed);
			if (key == -1)
				continue;
			if (map.ContainsKey (key)) {
				if (map [key] != null) {
					if (genome.EvaluationInfo.Fitness > map [key].EvaluationInfo.Fitness) {
						map [key] = genome;
						SaveGenome (genome, key.ToString ());
						Debug.Log ("NEW BEST GENOME SAVED AT " + key);
					}
				} else {
					map [key] = genome;
					SaveGenome (genome, key.ToString ());
					Debug.Log ("NEW SPOT IN MAP SAVED AT " + key);
				}
			} else {
				map.Add (key, genome);
				SaveGenome (genome, key.ToString ());
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

		Generation = _ea.CurrentGeneration;
		ChampAvgSpeed = _ea.CurrentChampGenome.EvaluationInfo.AvgSpeed;


		//		if (Generation % switchTrackAfterGames == 0 && Generation != 0 && trackStartPositions.Length > 1) {
		//			trackCounter++;
		//			StartPos = trackStartPositions [trackCounter];
		//			if (trackCounter >= trackStartPositions.Length) {
		//				trackCounter = 0;
		//			}
		//		}

		//		if ((Generation % switchRainAfterGames == 0 || bestFitness > StoppingFitness) && Generation != 0) {
		//			if (rain == maxRain) {
		//				// STOP
		//				StopEA ();
		//			} else {
		//				StopEA ();
		//
		//				rain += rainInterval;
		//				Unit.GetComponent<CarController> ().rain = rain;
		//
		//				bestFitness = 0;
		//
		//				champFileSavePath = Application.persistentDataPath + string.Format ("/{1}/{0:0.00}.best.xml", rain, folder_prefix);
		//				popFileSavePath = Application.persistentDataPath + string.Format ("/{1}/{0:0.00}.pop.xml", rain, folder_prefix);  		
		//
		//				SetupNewExperiment ();
		//				StartEA ();
		//
		//			}
		//		} 


		//    Utility.Log(string.Format("Moving average: {0}, N: {1}", _ea.Statistics._bestFitnessMA.Mean, _ea.Statistics._bestFitnessMA.Length));


	}


	private NeatGenome loadBest ()
	{
		NeatGenome genome = null;

		DirectoryInfo dir = new DirectoryInfo (Application.persistentDataPath + string.Format ("/{0}", folder_prefix));
		if (dir.Exists) {
			
		
			FileInfo[] info = dir.GetFiles ("*best.xml");



			// If there is not same amount of files as there is rain conditions.
//			if (map.Length != info.Length) {
//				Debug.Log (map.Length + " - " + info.Length);
//				Debug.Log ("Number of files not matching Map length! No files loaded.");
//			} else {
			
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
		mapRunning = false;
        
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

	public void RunBest ()
	{
		Time.timeScale = 1;

		//NeatGenome genome = map [bestCounter];
		NeatGenome genome = loadBest ();
		if (genome == null) {
			Debug.Log ("Elite was NULL.");
			return;
		}
		// Get a genome decoder that can convert genomes to phenomes.
		var genomeDecoder = experiment.CreateGenomeDecoder ();

		// Decode the genome into a phenome (neural network).
		var phenome = genomeDecoder.Decode (genome);

		GameObject obj = Instantiate (Unit, StartPos.transform.position, StartPos.transform.rotation) as GameObject;
		//obj.GetComponent<CarController> ().rain = minRain + (bestCounter * rainInterval);
		UnitController controller = obj.GetComponent<UnitController> ();

		ControllerMap.Add (phenome, controller);

		controller.Activate (phenome);

		bestCounter++;
//		if (bestCounter >= map.Length)
//			bestCounter = 0;
			
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
		if (GUI.Button (new Rect (10, 110, 100, 40), "Run best")) {
			RunBest ();
		}
		if (GUI.Button (new Rect (10, 160, 100, 40), "Start MAP")) {
			StartMAP2 ();
		}
		if (GUI.Button (new Rect (10, 210, 100, 40), "Stop MAP")) {
			StopMAP ();
		}

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
