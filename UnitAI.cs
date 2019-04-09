using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using AIDecisionObjects;
using EnumStorage;

//A Warning here: Much todo in terms of cleaning up this code as it has been refactored and decoupled so that entire groups of AI can be managed more easily
//Did this for the sake of less simulation of free will and more to create better gameplay. Much of functionality has been moved to Faction and its managers.
public class UnitAI : MonoBehaviour
{
	public GameManager gM;
	public Seeker seeker;
	AIPathAlignedToSurface ai;
	//Decision Making Variables
	public enum State {Idle, Moving, Working, Waiting, Planning, Nascent, Debug};
	public State state = State.Nascent;
	public AIActionFlag actionFlag = AIActionFlag.None;
	public Speciality speciality;
	public bool asleep = true;
	public bool waiting = false;
	public RacialAttributes racialAttributes;
	public Faction faction;
	public Settlement homeSettlement;
	public UnitAI myLeader = null;
	public List<UnitAI> myFollowers;
	[Range(0,10)]
	public float leadershipAbility;
	[Range(0,10)]
	public float energy;
	float maxEnergy;
	float waitTimer = 0.0f;
	public House house = null;
	Vector3 buildDestination;
	bool hasLeaderJob = false;
	Package package;
	ResourceInfoDatabase.ResourceClassification improvementClass;
	Building currentProject;

	public float prayerInterval;
	public int prayerIterations;
	float prayerTimer;
	int currentPrayerIterations;


    // Start is called before the first frame update
    void Start()
    {
		seeker = GetComponent<Seeker>();
		ai = GetComponent<AIPathAlignedToSurface>();
		myFollowers = new List<UnitAI>();
		prayerInterval = gM.godManager.prayerInterval;
		prayerIterations = gM.godManager.prayerIterations;
		prayerTimer = Random.Range(prayerInterval, 2.0f*prayerInterval);
		state = State.Nascent;
    }

	public void Initialize()
	{
		asleep = false;
		faction.RegisterWithFaction(this);
		speciality = (Speciality)Random.Range(0, (int)Speciality.NUM_TYPES);
		maxEnergy = 100.0f + 5 * racialAttributes.endurance;
		energy = maxEnergy;
		Debug.Log("AI INIT COMPLETE!");
		if(racialAttributes.carnivorous == 0 && speciality == Speciality.Hunter)
		{
			speciality = Speciality.Farmer;
			return;
		}
		if(racialAttributes.herbivorous == 0 && speciality == Speciality.Farmer)
		{
			speciality = Speciality.Hunter;
			return;
		}
		if(speciality == Speciality.Hunter)
		{
			float switchChance = racialAttributes.herbivorous - racialAttributes.carnivorous;
			float rand = Random.Range(1.0f, 10.0f);
			if(rand <= switchChance)
			{
				speciality = Speciality.Farmer;
			}
			return;
		}
		if(speciality == Speciality.Farmer)
		{
			float switchChance = racialAttributes.carnivorous - racialAttributes.herbivorous;
			float rand = Random.Range(1.0f, 10.0f);
			if(rand <= switchChance)
			{
				speciality = Speciality.Hunter;
			}
			return;
		}
	}

    // Update is called once per frame
    void Update()
    {
		prayerTimer -= Time.deltaTime;
		if(!asleep)
		{
			energy -= (Time.deltaTime / (1.1f * racialAttributes.endurance));
		}
		if(waiting)
		{
			waitTimer -= Time.deltaTime;
			if(waitTimer <= 0.0f)
			{
				waiting = false;
			}
		}
		else if(!asleep)
		{
			HandleAI();
		}
    }

	public void RegisterWithLeader(UnitAI ai)
	{
		if(ai.myLeader == null)
		{
			ai.myLeader = this;
			myFollowers.Add(ai);
		}
	}

	public void UnregisterWithLeader(UnitAI ai)
	{
		if(ai.myLeader == this)
		{
			ai.myLeader = null;
			myFollowers.Remove(ai);
		}
	}

	public void FinishedMovementAction()
	{
		Debug.Log("Unit Finished Moving!");
		waitTimer = Random.Range(0.0f, 1.5f);
		waiting = true;
		if(actionFlag == AIActionFlag.Loafing)
		{
			state = State.Idle;
		}
		else if(actionFlag != AIActionFlag.Praying)
		{
			state = State.Working;
		}
		else if(actionFlag == AIActionFlag.Praying)
		{
			state = State.Working;
			currentPrayerIterations = prayerIterations;
			prayerTimer = 10.0f;
		}
		else
		{
			state = State.Idle;
		}
	}

	void GoToNearestResourceOfType(PlanetFeatureSettings.PlanetResourceType type)
	{
		Vector3[] positions = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResource(type);
		seeker.StartMultiTargetPath(transform.position,positions, false);
	}

	void HandleAI()
	{
		//Debug.Log("HandleAI START!");
		if(state == State.Nascent)
		{
			if(FindHome()) { return;}
		}
		else if(state == State.Idle)
		{
			if(ShouldEat()) { return;}
			if(ShouldPray()) { return;}
			//if(ShouldPlan()) { return;}
			if(BuildHome()) { return;}
			if(Work()) { return;}
			if(GoHome()) { return;}
		}
		else if(state == State.Working)
		{
			//Debug.Log("Unit Was WORKING!");
			if(actionFlag == AIActionFlag.BuildSettlement)
			{
				//BUILD IT
				BuildNewSettlement();
				//TODO: SET STATE TO IDLE
				//Set state and flag
				actionFlag = AIActionFlag.None;
				state = State.Idle;
				Debug.Log("Settlement Built!");
				return;
			}
			if(actionFlag == AIActionFlag.Gathering)
			{
				ResourceInfoDatabase.ResourceSpawnPoint spawn = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetResourceSpawnPointByLocation(transform.position);
				if(spawn != null)
				{
					package = spawn.RequestPackageFromSpawnPoint((int)racialAttributes.strength);
					actionFlag = AIActionFlag.Hauling;
					state = State.Moving;
					seeker.StartPath(transform.position, homeSettlement.stockpile.stockpileObj.transform.position);
					//Gathering speed of a resource is determined by the gather time of the resources divided by dexterity, (plus some randomness for gameplay reasons).
					float dexInverse = (gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.database[(int)package.type].gatherTime) / racialAttributes.dexterity;
					waitTimer = Random.Range(1.0f * dexInverse, 2.0f * dexInverse);
					waiting = true;
					return;
				}
				return;
			}
			if(actionFlag == AIActionFlag.Hauling)
			{
				Debug.Log("Adding " + ((int)racialAttributes.strength).ToString() + " " + package.type.ToString() + " to stockpile.");
				homeSettlement.stockpile.resources[(int)package.type] += package.amount;
				if(package.sacred)
				{
					gM.godManager.currentFaith += 1;
				}

				actionFlag = AIActionFlag.None;
				state = State.Idle;
				return;
			}
			if(actionFlag == AIActionFlag.Eating)
			{
				Eat();
			}
			if(actionFlag == AIActionFlag.Praying)
			{
				Pray();
			}
			if(actionFlag == AIActionFlag.ConstructingGet)
			{
				if(homeSettlement.buildQueue.Count > 0)
				{
					ResourceInfoDatabase.ResourceClassification whatsNeeded;
					Building project = homeSettlement.buildQueue[0];
					int amountNeeded = 0;
					for(int a = 0; a < project.genericBuildCost.Count; a++)
					{
						if(project.genericBuildCost[a].amountStored < project.genericBuildCost[a].amountReq)
						{
							whatsNeeded = project.genericBuildCost[a].type;
							amountNeeded = project.genericBuildCost[a].amountReq - project.genericBuildCost[a].amountStored;
							break;
						}
					}
					//TODO: Change this later
					int[] listToUse = gM.currentPlanet.buildingMats;
					if(amountNeeded > ((int)racialAttributes.strength))
					{
						amountNeeded = ((int)racialAttributes.strength);
					}

					package = homeSettlement.stockpile.RequestMatsOfClassification(listToUse, amountNeeded);
					if(package != null)
					{
						state = State.Moving;
						actionFlag = AIActionFlag.ConstructingBring;
						seeker.StartPath(transform.position, project.transform.position);
						currentProject = project;
						return;
					}
					else
					{
						GatherBuildingMaterials();
						return;
					}
				}
				else
				{
					state = State.Idle;
					return;
				}
			}
			if(actionFlag == AIActionFlag.ConstructingBring)
			{
				if(currentProject == null)
				{
					state = State.Idle;
				}
				if(!currentProject.built)
				{
					for(int a = 0; a < currentProject.genericBuildCost.Count; a++)
					{
						if(currentProject.genericBuildCost[a].amountStored < currentProject.genericBuildCost[a].amountReq)
						{
							currentProject.genericBuildCost[a].amountStored += package.amount;
							package = null;
							state = State.Idle;
							break;
						}
					}
					bool fin = true;
					for(int a = 0; a < currentProject.genericBuildCost.Count; a++)
					{
						if(currentProject.genericBuildCost[a].amountStored < currentProject.genericBuildCost[a].amountReq)
						{
							fin = false;
							break;
						}
					}
					if(fin)
					{
						currentProject.built = true;
						currentProject.transform.GetChild(0).gameObject.SetActive(true);
						homeSettlement.buildQueue.Remove(currentProject);
						currentProject = null;
					}
					state = State.Idle;
				}
				else
				{
					state = State.Moving;
					actionFlag = AIActionFlag.Hauling;
					seeker.StartPath(transform.position, homeSettlement.stockpile.stockpileObj.transform.position);
				}
			}
		}
		else if(state == State.Waiting)
		{
		}
	}

	void Pray()
	{
		if(prayerTimer <= 0.0f)
		{
			currentPrayerIterations--;
			gM.godManager.currentFaith++;
			if(currentPrayerIterations <= 0)
			{
				//done praying
				prayerTimer = Random.Range(prayerInterval, 2.0f * prayerInterval);
				state = State.Idle;
			}
			else
			{
				prayerTimer = 10.0f;
			}

		}
	}

	void Eat()
	{
		int[] plants = gM.currentPlanet.plants;
		int[] meats = gM.currentPlanet.meats;
		if(racialAttributes.herbivorous >= racialAttributes.carnivorous)
		{
			bool doneEating = false;
			while(!doneEating)
			{
				Debug.Log("Not done eating!");
				int totalAvailablePlants = homeSettlement.stockpile.GetTotalAmountOfClassification(plants);
				int totalAvailableMeats = homeSettlement.stockpile.GetTotalAmountOfClassification(meats);
				if(totalAvailablePlants > 0)
				{
					Debug.Log("Eating plants!");
					homeSettlement.stockpile.RemoveMatsSpreadAroundClassification(plants, 1);
					energy += (racialAttributes.herbivorous * (25/racialAttributes.sustenanceRequirement));
					if(energy >= maxEnergy/2)
					{
						doneEating = true;
						state = State.Idle;
						return;
					}
				}
				else if(totalAvailableMeats > 0)
				{
					Debug.Log("Eating meat!");
					homeSettlement.stockpile.RemoveMatsSpreadAroundClassification(meats, 1);
					energy += (racialAttributes.carnivorous * (25/racialAttributes.sustenanceRequirement));
					if(energy >= maxEnergy/2)
					{
						doneEating = true;
						state = State.Idle;
						return;
					}
				}
				else
				{
					Debug.Log("Nothing to Eat!");
					GatherFood();
					doneEating = true;
					return;
				}
			}
		}
		else
		{
			bool doneEating = false;
			while(!doneEating)
			{
				int totalAvailablePlants = homeSettlement.stockpile.GetTotalAmountOfClassification(plants);
				int totalAvailableMeats = homeSettlement.stockpile.GetTotalAmountOfClassification(meats);
				if(totalAvailableMeats > 0)
				{
					homeSettlement.stockpile.RemoveMatsSpreadAroundClassification(plants, 1);
					energy += (racialAttributes.herbivorous * (25/racialAttributes.sustenanceRequirement));
					if(energy >= maxEnergy/2)
					{
						doneEating = true;
						state = State.Idle;
						return;
					}
				}
				else if(totalAvailablePlants > 0)
				{
					homeSettlement.stockpile.RemoveMatsSpreadAroundClassification(meats, 1);
					energy += (racialAttributes.carnivorous * (25/racialAttributes.sustenanceRequirement));
					if(energy >= maxEnergy/2)
					{
						doneEating = true;
						state = State.Idle;
						return;
					}
				}
				else
				{
					GatherFood();
					doneEating = true;
					return;
				}
			}
		}
	}

	bool ShouldEat()
	{
		if(energy <= (maxEnergy/4))
		{
			actionFlag = AIActionFlag.Eating;
			state = State.Moving;
			seeker.StartPath(transform.position, homeSettlement.stockpile.stockpileObj.transform.position);
			return true;
		}
		return false;
	}

	bool ShouldPray()
	{
		if(prayerTimer <= 0.0f && house != null)
		{
			actionFlag = AIActionFlag.Praying;
			state = State.Moving;
			seeker.StartPath(transform.position, house.transform.position);
			return true;
		}
		return false;
	}

	bool ShouldPlan()
	{
		if(myLeader == null)
		{
			state = State.Planning;
			return true;
		}
		return false;
	}

	bool Work()
	{
		if(speciality == Speciality.Farmer || speciality == Speciality.Hunter)
		{
			if(GatherFood()){return true;}
			if(GatherBuildingMaterials()){return true;}
		}
		else if(speciality == Speciality.Gatherer)
		{
			if(GatherBuildingMaterials()){return true;}
			if(GatherFood()){return true;}
		}
		else if(speciality == Speciality.Builder)
		{
			if(ConstructionQueued()){return true;}
			if(GatherBuildingMaterials()){return true;}
			if(GatherFood()){return true;}
		}
		else
		{
			
		}
		return false;
	}

	void BuildNewSettlement()
	{
		ResourceInfoDatabase.ResourceSpawnPoint spawn = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetUnoccupiedSpawnPointByLocation(transform.position);
		spawn.occupied = true;
		spawn.type = PlanetFeatureSettings.PlanetResourceType.NUM_RESOURCES;
		GameObject obj = gM.PlaceObject(gM.buildingPrefabs[0], transform.position);
		obj.transform.parent = gM.currentPlanet.transform;
		Settlement s = obj.AddComponent<Settlement>();
		s.Initialize();
		faction.RegisterNewSettlement(s);

	}

	void BuildNewHouse()
	{
		GameObject obj = gM.PlaceObject(gM.buildingPrefabs[((int)BuildingType.House)+1], buildDestination);
		obj.transform.parent = homeSettlement.transform;
		House h = obj.AddComponent<House>();
		homeSettlement.RegisterBuildingWithSettlement(h, buildDestination);
		hasLeaderJob = false;
		foreach(UnitAI ai in myFollowers)
		{
			ai.state = State.Idle;
		}
	}
	GameObject BuildResourceImprovement(ResourceInfoDatabase.ResourceClassification resourceClass, Vector3 spot)
	{
		Debug.Log("/////// BUILDING RESOURCE IMPROVEMENT STARTED! ///////////");
		if(resourceClass == ResourceInfoDatabase.ResourceClassification.Plant)
		{
			//build farm
			GameObject obj = gM.PlaceObject(gM.buildingPrefabs[((int)BuildingType.House)+1], spot);
			obj.transform.parent = gM.currentPlanet.transform;
			obj.name = "Farm";
			Building b = obj.AddComponent<Building>();
			b.buildingType = BuildingType.Farm;
			Building.GenericMaterialCost cost = new Building.GenericMaterialCost();
			cost.amountReq = 20;
			cost.amountStored = 0;
			cost.type = ResourceInfoDatabase.ResourceClassification.BuildingMaterial;
			b.genericBuildCost = new List<Building.GenericMaterialCost>();
			b.genericBuildCost.Add(cost);
			homeSettlement.AddBuildingToBuildQueue(b);
			homeSettlement.RegisterFarmImprovement(b);
			return obj;
		}
		return null;
	}

	bool ConstructionQueued()
	{
		if(homeSettlement.buildQueue.Count > 0)
		{
			//theres a building in the queue
			actionFlag = AIActionFlag.ConstructingGet;
			state = State.Moving;
			seeker.StartPath(transform.position, homeSettlement.stockpile.stockpileObj.transform.position);
			return true;
		}
		return false;
	}

	bool GatherBuildingMaterials()
	{
		if(homeSettlement != null && house != null)
		{
			if(house.built)
			{
				Debug.Log("BuildMatsLength: " + gM.currentPlanet.buildingMats.Length.ToString());
				if(gM.currentPlanet.buildingMats.Length > 0)
				{
					PlanetFeatureSettings.PlanetResourceType resourceType = (PlanetFeatureSettings.PlanetResourceType)gM.currentPlanet.buildingMats[(int)Random.Range(0, gM.currentPlanet.buildingMats.Length)];
					if(homeSettlement.stockpile.resources[(int)resourceType] <
						gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.database[(int)resourceType].rarity * 100 * (racialAttributes.wisdom/5))
					{
						int[] x = new int[1];
						x[0] = (int)resourceType;
						Debug.Log("Chosen Build Mat to get: " + (resourceType).ToString());
						Vector3[] spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResources(x);
						if(spawns.Length > 0)
						{
							Debug.Log("Spawns length: " + spawns.Length);
							seeker.StartMultiTargetPath(transform.position, spawns, false);
							state = State.Moving;
							actionFlag = AIActionFlag.Gathering;
							return true;
						}
						else
						{
							Debug.Log("No spawns of: " + resourceType.ToString());
							return false;
						}
					}
				}

			}
		}
		return false;
	}

	bool GatherFood()
	{
		if(homeSettlement != null && house != null)
		{
			if(house.built)
			{
			int totalAvailableFood = homeSettlement.stockpile.GetTotalAmountOfClassification(gM.currentPlanet.plants) + homeSettlement.stockpile.GetTotalAmountOfClassification(gM.currentPlanet.meats);
			//TODO: Change the flat value to one based off of settlement population
			//TODO: Also make it so that people have jobs - some are more likely to gather food etc.
				if(totalAvailableFood < homeSettlement.residents * racialAttributes.sustenanceRequirement * racialAttributes.wisdom * 5)
				{
					if(speciality == Speciality.Farmer)
					{
						if(GatherHerbivorousFood()){return true;}
					}
					if(speciality == Speciality.Hunter)
					{
						if(GatherCarnivorousFood()){return true;}
					}
					if(racialAttributes.herbivorous >= racialAttributes.carnivorous)
					{
						if(GatherHerbivorousFood()){return true;}
						if(GatherCarnivorousFood()){return true;}
					}
					else
					{
						if(GatherCarnivorousFood()){return true;}
						if(GatherHerbivorousFood()){return true;}
					}
				}
			}
		}
		return false;
	}

	bool GatherHerbivorousFood()
	{
		Debug.Log("Not enough food(plants) -> gotta go get some more!");
		//List<ResourceInfoDatabase.ResourceSpawnPoint> spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetAllSpawnsOfChosenResources(buildingMats);
		if(homeSettlement != null)
		{
			if(homeSettlement.improvements.Count > 0)
			{
				Settlement.SettlementBuildingKeeper[] sbks = homeSettlement.improvements.ToArray();
				Vector3[] farms = new Vector3[sbks.Length];
				for(int a = 0; a < sbks.Length; a++)
				{
					farms[a] = sbks[a].location;
				}
				Debug.Log("Farms length: " + farms.Length);
				seeker.StartMultiTargetPath(transform.position, farms, false);
				state = State.Moving;
				actionFlag = AIActionFlag.Gathering;
				return true;
			}
		}
		Vector3[] spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResources(gM.currentPlanet.plants);
		if(spawns.Length > 0)
		{
			Debug.Log("Spawns length: " + spawns.Length);
			seeker.StartMultiTargetPath(transform.position, spawns, false);
			state = State.Moving;
			actionFlag = AIActionFlag.Gathering;
			return true;
		}
		else
		{
			Debug.Log("No plant spawns.");
			return false;
		}
	}

	bool GatherCarnivorousFood()
	{
		Debug.Log("Not enough food(meats) -> gotta go get some more!");

		Vector3[] spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResources(gM.currentPlanet.meats);
		if(spawns.Length > 0)
		{
			Debug.Log("Spawns length: " + spawns.Length);
			seeker.StartMultiTargetPath(transform.position, spawns, false);
			state = State.Moving;
			actionFlag = AIActionFlag.Gathering;
			return true;
		}
		else
		{
			Debug.Log("No meat spawns.");
			return false;
		}
	}

	bool BuildHome()
	{
		if(house == null)
		{
			return false;
		}
		else
		{
			if(house.built == true)
			{
				return false;
			}
			else
			{
				int[] buildingMats = gM.currentPlanet.buildingMats;
				Debug.Log("Buildmats length: " + buildingMats.Length);
				int totalAvailableMats = homeSettlement.stockpile.GetTotalAmountOfClassification(buildingMats);
				if(totalAvailableMats >= 15)
				{
					if(!house.built)
					{
						house.built = true;
						house.transform.GetChild(0).gameObject.SetActive(true);
						homeSettlement.stockpile.RemoveMatsSpreadAroundClassification(buildingMats, 15);
					}
					return true;
				}
				else
				{
					//TODO: START HERE -> Run with this debug and figure out why he sets to gethering but doesn't move!
					Debug.Log("Not enough resources -> gotta go get some more FOR HOUSE!");
					//List<ResourceInfoDatabase.ResourceSpawnPoint> spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetAllSpawnsOfChosenResources(buildingMats);
					Vector3[] spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResources(buildingMats);
					Debug.Log("Spawns length: " + spawns.Length);
					seeker.StartMultiTargetPath(transform.position, spawns, false);
					state = State.Moving;
					actionFlag = AIActionFlag.Gathering;
					return true;
				}
			}
		}
	}

	bool FindHome()
	{
		if(faction.settlements.Count == 0)
		{
			Debug.Log("Unit Found No Settlements in Faction!");
			faction.RequestNewSettlement();
		}
		else
		{
			state = State.Idle;
		}
		return true;
	}

	bool GoHome()
	{
		//Debug.Log("Unit Started GOHOME!");
		if(house != null)
		{
			//Debug.Log("Unit Has House!");
			seeker.StartPath(transform.position, house.gameObject.transform.position);
			actionFlag = AIActionFlag.Loafing;
			state = State.Moving;
			return true;
		}
		else if(homeSettlement != null)
		{
			Debug.Log("///// Applying for Housing.");
			//Check if there are any houses
			//List<Building> houses = homeSettlement.GetAllBuildingsOfType(BuildingType.House);
			House possibleHouse = homeSettlement.GetFirstHouseWithRoom();
			if(possibleHouse != null)
			{
				Debug.Log("///// Possible House Exists.");
				if(possibleHouse.ApplyForHousing(this))
				{
					homeSettlement.residents++;
				}
			}
			else
			{
				Debug.Log("///// Possible House Doesn't Exist.");
				//Wasn't any available houses so we need to build a new one!
				faction.RequestNewHouse(homeSettlement);
			}
		}
		else if(faction.settlements.Count == 0)
		{
			Debug.Log("Unit Found No Settlements in Faction!");
			//There are no settlements in our faction so we need to make a new one!
			//Do we have a leader?
			if(myLeader == null)
			{
				FindLeader();
			}
			//Leader executes the plan
			if(myLeader == null)
			{
				FindSuitableSettlementLocation();
				foreach(UnitAI follower in myFollowers)
				{
					Debug.Log("Unit commanding followers!");
					follower.ai.destination = ai.destination;
					follower.actionFlag = AIActionFlag.HelpingLeader;
					follower.state = State.Moving;
				}
			}

		}
		else if(faction.settlements.Count > 0)
		{
			
		}
		else
		{
			
		}
		return false;
	}

	void FindLeader()
	{
		Debug.Log("Unit Started Finding Leaders!");
		UnitAI leader = faction.GetRandomLeader();
		if(leader != this)
		{
			Debug.Log("Unit Found Leader!");
			leader.RegisterWithLeader(this);
		}
		else
		{
			Debug.Log("Unit Failed to find Leader!");
		}
	}

	bool FindSuitableSettlementLocation()
	{
		Debug.Log("Unit Started Finding Settlement Locs!");
		MinMax fertReq = new MinMax();
		fertReq.AddValue(0.4f);
		fertReq.AddValue(1.0f);
		Vector3[] possibleLocations = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetAllVerticesThatMatchArguements(racialAttributes.temperaturePreferenceRange, fertReq);
		if(possibleLocations.Length > 0)
		{
			Debug.Log("Unit FOUND Settlement Locs!");
			actionFlag = AIActionFlag.BuildSettlement;
			state = State.Moving;
			seeker.StartMultiTargetPath(transform.position,possibleLocations, false);
			return true;
		}
		else
		{
			Debug.Log("Unit FOUND *NO* Settlement Locs!");
			state = State.Debug;
			return false;
		}
	}

	void RequestNewHouse()
	{
		if(!hasLeaderJob)
		{
			actionFlag = AIActionFlag.BuildHouse;
			state = State.Planning;
			hasLeaderJob = true;
		}
	}

	bool ShouldBuildNewBuilding()
	{
		Debug.Log("//////////////// SHOULD BUILD NEW BUILDING???");
		if(homeSettlement != null && house != null)
		{
			if(homeSettlement.improvements.Count < homeSettlement.residents/5 && faction.techTree.availableBuildings[(int)BuildingType.Farm])
			{
				//Build a farm
				Debug.Log("//////////////// Planning Farm Construction.");
				//BuildResourceImprovement(ResourceInfoDatabase.ResourceClassification.Plant);
				Vector3[] spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResources(gM.currentPlanet.plants);
				if(spawns.Length > 0)
				{
					Debug.Log("Spawns length: " + spawns.Length);
					seeker.StartMultiTargetPath(transform.position, spawns, false);
					state = State.Moving;
					actionFlag = AIActionFlag.BuildImprovement;
					improvementClass = ResourceInfoDatabase.ResourceClassification.Plant;
					return true;
				}
				else
				{
					Debug.Log("No plant spawns to build farms on.");
					return false;
				}
			}
		}
		else
		{
			Debug.Log("//////////////// GO HOME");
			GoHome();
		}
		return false;
	}
}
