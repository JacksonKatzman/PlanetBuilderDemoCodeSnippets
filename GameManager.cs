using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using AIDecisionObjects;
using TechnologyTrees;

public class GameManager : MonoBehaviour
{
	public bool GameHasStarted = false;
	[Header("Important Planet Setup Attributes")]
	public Planet currentPlanet;
	public ShapeSettings initShapeSettings;
	public ColorSettings initColorSettings;

	[Header("UI Related")]
	public UIManager uiManager;

	[Header("Camera Related")]
	public Camera camera;

	[Header("Biome Related")]
	public Dropdown[] BiomeAvailabilityDropdowns;
	List<string> biomeOptions = new List<string>() {"Grassland", "Forest", "Desert", "Tundra", "Volcanic", "Arctic", "Wasteland"};
	public GradientKeeper gradientKeeper;
	[HideInInspector]
	public bool biomeGradientSettingsFoldout;

	[SerializeField] Text temperatureText;


	[Header("AI Related")]
	public int numberOfAISpawnPoints = 1;
	public List<Vector3> aiSpawnPoints;
	//Temp object list
	public List<GameObject> aiObjects;
	public GameObject BasicAiPrefab;
	public RacialAttributes racialAttributes;
	public List<Faction> factions;
	[SerializeField] GameObject techTreePrefab;

	[Header("Player Related")]
	public GodManager godManager;

	[Header("Resource Prefabs")]
	public GameObject[] resourcePrefabs = new GameObject[((int)PlanetFeatureSettings.PlanetResourceType.NUM_RESOURCES)];
	public GameObject[] buildingPrefabs;

	[Header("Building Blueprints")]
	public House masterHouse;

	//public string[] godNames = {"Arch", "Azir", "Alm", "Anto", "Ankh", "Baal", "Belo", "Beka", "Bahn", "Cho", "Kass", "Mehm", "Rakh", "Siff", "Un", "Virs", "Xin", "Yor"};
	public static GameManager instance = null;

	void Awake()
	{
		if(instance == null)
		{
			instance = this;
		}
		else if(instance != this)
		{
			Destroy(gameObject);
		}
		DontDestroyOnLoad(gameObject);
	}

	void Start()
	{
		techTreePrefab = (GameObject)Resources.Load("Prefabs/AITechTree");
		uiManager = GetComponent<UIManager>();
		aiSpawnPoints = new List<Vector3>();
		aiObjects = new List<GameObject>();
		factions = new List<Faction>();
		SetInitialFaction();
		uiManager.playerUI.SetActive(false);
		LoadResourcePrefabs();

		masterHouse = gameObject.AddComponent<House>();
		masterHouse.Initialize();


	}

	void Update()
	{
		if(GameHasStarted)
		{
			foreach(Faction f in factions)
			{
				f.Update();
			}
		}
	}

	public GameObject CreateTechTree(GameObject go)
	{
		return (GameObject)Instantiate(go, uiManager.canvas.transform);
	}

	public void SetUpAndInitUnitAIs()
	{
		for(int i = 0; i < 5; i++)
		{
			SpawnNewAIAgent();
		}
		foreach(GameObject ai in aiObjects)
		{
			ai.GetComponent<UnitAI>().Initialize();
		}
		GameHasStarted = true;
	}

	void SetInitialFaction()
	{
		factions.Add(new Faction("Wanderers"));
		factions[0].techTree = CreateTechTree(techTreePrefab).GetComponent<TechTreeAI>();
		factions[0].techTree.myFaction = factions[0];
		factions[0].gM = this;
		factions[0].infoBar = uiManager.CreateFactionInfoBar();
		factions[0].infoBar.faction = factions[0];
		factions[0].factionWindow = uiManager.CreateFactionWindow();
		factions[0].factionWindow.faction = factions[0];
	}

	void SpawnNewAIAgent()
	{
		Vector3 spawnPoint = aiSpawnPoints[Random.Range(0, numberOfAISpawnPoints)];
		GameObject obj = PlaceObject(BasicAiPrefab, spawnPoint);
		UnitAI unitAI = obj.GetComponent<UnitAI>();
		unitAI.gM = this;
		unitAI.racialAttributes = this.racialAttributes;
		unitAI.faction = factions[0];
		if(aiObjects.Count <= 0)
		{
			unitAI.leadershipAbility = 6.0f;
		}
		else
		{
			unitAI.leadershipAbility = Random.Range(0.0f, 4.0f);
		}
		//unitAI.Initialize();
		aiObjects.Add(obj);
	}

	public void TryToSpawnResource(PlanetFeatureSettings.PlanetResourceType type, float chanceMultiplier = 1.0f, int spawnAmount = -1, bool sacred = false)
	{
		Debug.Log("Going to try to spawn: " + type.ToString());
		PlanetFeatureSettings settings = currentPlanet.planetFeatureSettings;
		ResourceInfoDatabase info = settings.resourceInfoDatabase;
		//Check if our planet has that resource
		if(currentPlanet.planetFeatureSettings.planetResourceTypes[(int)type])
		{
			//Get which biomes the chosen resource type can occur in
			bool[] allowedSpawnBiomes = info.database[(int)type].spawnBiomes;
			bool found = false;
			//Prevent infinite loop failsafe
			int maxTries = 25;
			int currentTries = 0;
			//Search for spawn point until one is found
			while(!found)
			{
				//Get a random spawn point
				int randPos = Random.Range(0, info.resourceSpawnPoints.Length);
				ResourceInfoDatabase.ResourceSpawnPoint candidate = info.resourceSpawnPoints[randPos];
				//Check if the chosen resource can spawn on this tile type
				Debug.Log("Candidate tiletype: " + candidate.vertexTypeHolder.type.ToString());
				Debug.Log("Resource tiletype: " + info.database[(int)type].spawnLocationType.ToString());
				if(candidate.vertexTypeHolder.type == info.database[(int)type].spawnLocationType)
				{
					//Check if it is already occupied
					Debug.Log("Checking if candidate is occupied!");
					if(!candidate.occupied)
					{
						Debug.Log("Candidate wasn't occupied!");
						Debug.Log("Checking if candidate can spawn in this biome: " + candidate.vertexTypeHolder.biome.ToString());
						//Check if the chosen resource type can spawn in the selected position's biome
						if(info.database[(int)type].spawnBiomes[(int)candidate.vertexTypeHolder.biome] || (info.database[(int)type].spawnBiomes[(int)PlanetFeatureSettings.BiomeType.All]))
						{
							float spawnDecider = Random.Range(0.0f, 100.0f);
							//Check if the resource actually spawns based on it's rarity
							if(spawnDecider <= ((info.database[(int)type].rarity * 100.0f)) * chanceMultiplier)
							{
								GameObject prefab = resourcePrefabs[(int)type];
								//GameObject obj = GameObject.Instantiate(prefab, candidate.vertexTypeHolder.pos, Quaternion.identity);
								GameObject obj = PlaceObject(prefab, candidate.vertexTypeHolder.pos);
								obj.name = info.database[(int)type].resourceName;
								obj.transform.parent = currentPlanet.transform;
								obj.AddComponent<ObjectDestroyer>();
								candidate.occupied = true;
								candidate.resourceObject = obj;
								candidate.type = type;
								if(spawnAmount == -1)
								{
									spawnAmount = Random.Range(500, 2000);
								}
								candidate.amount = spawnAmount;
								candidate.sacred = sacred;
								Debug.Log("Succeeded in spawning: " + type.ToString());
							}
							else
							{
								Debug.Log("Failed to spawn: " + type.ToString());
							}
							//Return true regardless of whether it spawns because we found a suitable location
							//but the RNG decided not to spawn it this time.
							found = true;
						}
					}
					currentTries++;
					if(currentTries > maxTries)
					{
						found = true;
						Debug.Log("No available spawn locations for: " + type.ToString());
					}
				}
			}
		}
	}

	PlanetFeatureSettings.PlanetResourceType PickRandomAvailableResource()
	{
		PlanetFeatureSettings settings = currentPlanet.planetFeatureSettings;
		int randAllowed = Random.Range(0, settings.allowedResources.Count);
		return settings.allowedResources[randAllowed];
	}

	public void CreateNewPlanet()
	{
		if(currentPlanet == null)
		{
			GameObject NewPlanet = new GameObject("New Planet");
			NewPlanet.layer = 13;
			currentPlanet = NewPlanet.AddComponent<Planet>();
			currentPlanet.finalMesh = NewPlanet.AddComponent<MeshFilter>();
			currentPlanet.lowResMeshHolder = new GameObject("LowResMeshHolder");
			currentPlanet.lowResMeshHolder.transform.parent = currentPlanet.transform;
			currentPlanet.lowResMeshHolder.AddComponent<MeshCollider>();
			currentPlanet.lowResMeshHolder.layer = 12;
			currentPlanet.lowResMesh = currentPlanet.lowResMeshHolder.AddComponent<MeshFilter>();
			currentPlanet.shapeSettings = new ShapeSettings (initShapeSettings);
			currentPlanet.colorSettings = new ColorSettings (initColorSettings);
			currentPlanet.planetFeatureSettings = new PlanetFeatureSettings();
			currentPlanet.planetFeatureSettings.Initialize();
			currentPlanet.resolution = 95;
			//currentPlanet.shapeSettings.planetRadius = 2;
			//currentPlanet.gameObject.AddComponent<RotateObjectMouse>();
			currentPlanet.GeneratePlanet();
			GetBiomeOptionUI();
			//UpdateAllowedBiomes();
			ChangeEquatorTemperature(currentPlanet.planetFeatureSettings.equatorTempterature.ToString());
		}
	}

	public void SpawnInitialResources()
	{
		//TODO: Make the difficulty change by allowing a setting to change the number of each type
		//of starting resource the player gets. For now, just using flat values of 8.
		//Randomness still rules as each attempt only has a chance of spawning the resource.
		for(int i = 0; i < 8; i++)
		{
			Debug.Log("Initial Resource Spawn Wave: " + i.ToString());
			if(currentPlanet.buildingMats.Length > 0)
			{
				Debug.Log("Trying to spawn Building Material!");
				int rand = Random.Range(0, currentPlanet.buildingMats.Length);
				TryToSpawnResource((PlanetFeatureSettings.PlanetResourceType)currentPlanet.buildingMats[rand]);
			}
			if(currentPlanet.plants.Length > 0)
			{
				Debug.Log("Trying to spawn Plant!");
				int rand = Random.Range(0, currentPlanet.plants.Length);
				TryToSpawnResource((PlanetFeatureSettings.PlanetResourceType)currentPlanet.plants[rand]);
			}
			if(currentPlanet.meats.Length > 0)
			{
				Debug.Log("Trying to spawn Meat!");
				int rand = Random.Range(0, currentPlanet.meats.Length);
				TryToSpawnResource((PlanetFeatureSettings.PlanetResourceType)currentPlanet.meats[rand]);
			}
		}
	}

	public void RebuildPlanetNavMesh()
	{
		if(currentPlanet != null)
		{
			//currentPlanet.MergePlanetMeshes();
			currentPlanet.BeginMeshConstruction();
		}
	}

	public void ChangeIfHasLiquidWater()
	{
		if(currentPlanet != null)
		{
			currentPlanet.planetFeatureSettings.hasLiquidWater = !currentPlanet.planetFeatureSettings.hasLiquidWater;
		}
	}

	void LoadResourcePrefabs()
	{
		for(int i = 0; i < resourcePrefabs.Length; i++)
		{
			string nameToLoad = "prefabs/" + PlanetFeatureSettings.GetResourceNameToLoad(i);
			resourcePrefabs[i]  = (GameObject)Resources.Load(nameToLoad, typeof(GameObject));
		}
	}

	public void GetBiomeOptionUI()
	{
		for(int i = 0; i < BiomeAvailabilityDropdowns.Length; i++)
		{
			Dropdown dropdown = BiomeAvailabilityDropdowns[i];
			DropdownChangeInfo DCI = dropdown.GetComponent<DropdownChangeInfo>();
			if(DCI != null)
			{
				DCI.gM = this;
				DCI.listID = i;
			}
			else
			{
				DCI = dropdown.gameObject.AddComponent<DropdownChangeInfo>();
				DCI.gM = this;
				DCI.listID = i;
			}
		}
	}

	public void UpdateAllowedBiomes()
	{
		if(currentPlanet != null)
		{
			for(int x = 0; x < 5; x++)
			{
				Debug.Log("Should be clearing options!");
				BiomeAvailabilityDropdowns[x].ClearOptions();
			}
			int[] temperatures = new int[5];
			temperatures[0] = currentPlanet.planetFeatureSettings.equatorTempterature - (2*currentPlanet.planetFeatureSettings.averageBiomeTemperatureShift);
			temperatures[4] = currentPlanet.planetFeatureSettings.equatorTempterature - (2*currentPlanet.planetFeatureSettings.averageBiomeTemperatureShift);
			temperatures[1] = currentPlanet.planetFeatureSettings.equatorTempterature - (1*currentPlanet.planetFeatureSettings.averageBiomeTemperatureShift);
			temperatures[3] = currentPlanet.planetFeatureSettings.equatorTempterature - (1*currentPlanet.planetFeatureSettings.averageBiomeTemperatureShift);
			temperatures[2] = currentPlanet.planetFeatureSettings.equatorTempterature - (0*currentPlanet.planetFeatureSettings.averageBiomeTemperatureShift);

			for(int i = 0; i < 5; i++)
			{
				int temperature = temperatures[i];
				if(temperature >= 150)
				{
					biomeOptions = new List<string>() {"Volcanic", "Wasteland"};
				}
				else if(temperature >= 130)
				{
					biomeOptions = new List<string>() {"Desert", "Volcanic", "Wasteland"};
				}
				else if(temperature >= 85 && temperature < 130)
				{
					biomeOptions = new List<string>() {"Grassland", "Forest", "Desert", "Wasteland"};
				}
				else if(temperature >= 35 && temperature < 85)
				{
					biomeOptions = new List<string>() {"Grassland", "Forest", "Wasteland"};
				}
				else if(temperature >= 0 && temperature < 35)
				{
					biomeOptions = new List<string>() {"Forest", "Tundra", "Wasteland"};
				}
				else if(temperature >= -130 && temperature < 0)
				{
					biomeOptions = new List<string>() {"Tundra", "Arctic", "Wasteland"};
				}
				else
				{
					biomeOptions = new List<string>() {"Arctic", "Wasteland"};
				}
					
				BiomeAvailabilityDropdowns[i].AddOptions(biomeOptions);
				ChangeSpecifiedBiome(BiomeAvailabilityDropdowns[i], BiomeAvailabilityDropdowns[i].captionText.text, i);
			}
		}
	}

	public void ChangeSelectedPower(int chosenPower)
	{
		godManager.OnChangeSelectedPower(chosenPower);
	}

	public void ChangeOceanType(int chosenType)
	{
		if(currentPlanet != null)
		{
			currentPlanet.UpdateOceanType(chosenType, this);
		}
	}

	public void ChangePlanetSize(float newSize)
	{
		if(currentPlanet != null)
		{
			currentPlanet.UpdatePlanetSize(newSize);
		}
	}

	public void ChangeLandMass(float newSize)
	{
		if(currentPlanet != null)
		{
			currentPlanet.UpdateLandMassAmount(newSize);
		}
	}

	public void RandomizeLandMass()
	{
		if(currentPlanet != null)
		{
			currentPlanet.RandomizeLandMass();
		}
	}

	public void ChangeMountainLevel(float newLevel)
	{
		if(currentPlanet != null)
		{
			currentPlanet.UpdateMountainLevel(Mathf.Clamp(newLevel, 0.0f, 2.0f));
		}
	}

	public void RandomizeMountains()
	{
		if(currentPlanet != null)
		{
			currentPlanet.RandomizeMountains();
		}
	}

	public void ChangeEquatorTemperature(string temp)
	{
		if(currentPlanet != null)
		{
			int convertedTemp = int.Parse(temp);
			currentPlanet.UpdateEquatorTemp(Mathf.Clamp(convertedTemp, -400, 400));
			UpdateAllowedBiomes();
		}
	}

	public void ChangeSpecifiedBiome(Dropdown dropdown, string biomeText, int id)
	{
		//BiomeAvailabilityDropdowns[0].captionText
//		Debug.Log("New Biome will be " + biomeText);
		if(currentPlanet != null)
		{
			int biomeID = GetBiomeFromString(biomeText);
			Gradient sendGrad = CloneGradient(gradientKeeper.biomeGradients[biomeID]);
			currentPlanet.UpdateSpecificBiome(id, sendGrad);
			currentPlanet.planetFeatureSettings.biomeTypes[id] = (PlanetFeatureSettings.BiomeType)(biomeID);
			//currentPlanet.colorSettings.biomeColorSettings.biomes[id].gradient = gradientKeeper.biomeGradients[biomeID];
			//currentPlanet.OnColorSettingsUpdated();
		}
	}

	//Tile Set Up
	public void SetUpPlanetTiles()
	{
		currentPlanet.InitializePlanetTiles();
		SetUpAISpawnPoints();
		currentPlanet.planetFeatureSettings.resourceInfoDatabase.SetResourceSpawnLocations(currentPlanet);
	}

	public void SetUpAISpawnPoints()
	{
		List<PlanetTileDatabase.PlanetTile> possibleTiles = new List<PlanetTileDatabase.PlanetTile>();
		currentPlanet.planetTileDatabase.GetAllTilesOfType(ref possibleTiles, PlanetTileDatabase.TileLocationType.Shore);
		Debug.Log("Number of SHORE TILES: " + possibleTiles.Count.ToString());
		List<PlanetTileDatabase.PlanetTile> chosenTiles = new List<PlanetTileDatabase.PlanetTile>();
		int numPoints = numberOfAISpawnPoints;
		if(numPoints > possibleTiles.Count)
		{
			numPoints = possibleTiles.Count;
		}
		for(int i = 0; i < numberOfAISpawnPoints; i++)
		{
			int chosen = Random.Range(0, possibleTiles.Count);
			chosenTiles.Add(possibleTiles[chosen]);
			possibleTiles.RemoveAt(chosen);
		}
		foreach(PlanetTileDatabase.PlanetTile tile in chosenTiles)
		{
			//Vector3 chosenSpawn;
			for(int i = 0; i < tile.vertices.Length; i++)
			{
				if(tile.vertices[i].type == PlanetTileDatabase.TileLocationType.Land)
				{
					aiSpawnPoints.Add(tile.vertices[i].pos);
					break;
				}
			}

		}
		//DEBUGGING
		foreach(Vector3 v in aiSpawnPoints)
		{
			Debug.Log("AI Spawn Point At: " + v.ToString());
		}
	}

	int GetBiomeFromString(string biomeText)
	{
		switch(biomeText)
		{
		case "Grassland":
			return 0;
		case "Forest":
			return 1;
		case "Desert":
			return 2;
		case "Tundra":
			return 3;
		case "Volcanic":
			return 4;
		case "Arctic":
			return 5;
		case "Wasteland":
			return 6;
		default:
			return 6;
		}
	}

	public Gradient CloneGradient(Gradient toClone)
	{
		Gradient testGrad = new Gradient();
		testGrad.SetKeys(toClone.colorKeys, toClone.alphaKeys);
		testGrad.mode = toClone.mode;
		//GradientColorKey[] testKeys = new GradientColorKey[toClone.colorKeys.Length];
		//Debug.Log("TOCLONE color keys Length: " + toClone.colorKeys.Length.ToString());
		//Debug.Log("NEW color keys Length: " + testKeys.Length.ToString());
		return testGrad;
	}

	public void UpdateTemperatureUI(float newTemp)
	{
		temperatureText.text = newTemp.ToString() + "°F";
	}

	public GameObject PlaceObjectWithMouse(GameObject prefab, Vector3 position)
	{
		//Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
		Ray ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(position));
		RaycastHit hit;
		Debug.Log("Casting a ray to place an object!");
		// Figure out where the ground is
		if (Physics.Raycast(ray, out hit, Mathf.Infinity)) {
			Debug.Log("Hit something while trying to place object!");
			Vector3 p = hit.point;
			//var rot = Quaternion.identity;
			Quaternion rot = Quaternion.LookRotation(hit.normal, Vector3.right) * Quaternion.Euler(90, 0, 0);
			Debug.Log("Placing Object!");
			GameObject obj = GameObject.Instantiate(prefab, position, rot);
			return obj;
		}
		else
		{
			Debug.Log("Failed to rotate correctly but placing anyway!");
			GameObject obj = GameObject.Instantiate(prefab, position, Quaternion.identity);
			return obj;
		}
	}

	public GameObject PlaceObject(GameObject prefab, Vector3 position)
	{
		Vector3 direction = position - currentPlanet.transform.position;
		direction.Normalize();
		Vector3 raycastStartPos = direction * currentPlanet.shapeSettings.planetRadius * 3;
		direction = position - raycastStartPos;
		direction = direction.normalized * 2;
		Ray ray = new Ray(raycastStartPos, direction);
		//Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
		//Ray ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(position));
		RaycastHit hit;
		Debug.Log("Casting a ray to place an object!");
		// Figure out where the ground is
		if (Physics.Raycast(ray, out hit, Mathf.Infinity)) {
			Debug.Log("Hit something while trying to place object!");
			Vector3 p = hit.point;
			//var rot = Quaternion.identity;
			Quaternion rot = Quaternion.LookRotation(hit.normal, Vector3.right) * Quaternion.Euler(90, 0, 0);
			Debug.Log("Placing Object!");
			GameObject obj = GameObject.Instantiate(prefab, p, rot);
			return obj;
		}
		else
		{
			Debug.Log("Failed to rotate correctly but placing anyway!");
			GameObject obj = GameObject.Instantiate(prefab, position, Quaternion.identity);
			return obj;
		}
	}


}
