using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TechnologyTrees;
using EnumStorage;

namespace AIDecisionObjects
{
	public class Faction
	{
		public string factionName;
		public List<Settlement> settlements;
		public List<UnitAI> members;
		public UnitAI factionLeader;
		public TechTreeAI techTree;
		public GameManager gM;
		public Religion religion;
		public FactionInfoBar infoBar;
		public FactionWindow factionWindow;
		public List<FactionAction> actionQueue;

		bool FirstFindHomeRequested = false;
		public float actionTimer;

		public Faction (string factionName)
		{
			this.factionName = factionName;
			settlements = new List<Settlement>();
			members = new List<UnitAI>();
			religion = new Religion();
			religion.Initialize(gM);
			actionQueue = new List<FactionAction>();
			actionTimer = 20.0f;
		}


		public Vector3[] GetAllSettlementLocations()
		{
			Vector3[] positions = new Vector3[settlements.Count];
			for(int i = 0; i < positions.Length; i++)
			{
				positions[i] = settlements[i].gameObject.transform.position;
			}
			return positions;
		}

		public void RegisterWithFaction(UnitAI ai)
		{
			if(ai.faction == null || ai.faction == this)
			{
				ai.faction = this;
				members.Add(ai);
				Debug.Log("Faction Reg Success!");
			}
			else
			{
				Debug.Log("Faction Reg Failed!");
			}
		}

		public void UnregisterWithFaction(UnitAI ai)
		{
			if(ai.faction == this)
			{
				ai.faction = null;
				members.Remove(ai);
			}
		}

		public UnitAI GetRandomLeader()
		{
			if(infoBar == null)
			{
				infoBar = gM.uiManager.CreateFactionInfoBar();
			}
			List<UnitAI> leaders = new List<UnitAI>();
			foreach(UnitAI ai in members)
			{
				if(ai.leadershipAbility > 5.0f)
				{
					leaders.Add(ai);
				}
			}
			int numLeaders = leaders.Count;
			return leaders[Random.Range(0, numLeaders)];
		}

		public Vector3[] GetSettlementsWithLivingSpace()
		{
			List<Vector3> locs = new List<Vector3>();
			foreach(Settlement s in settlements)
			{
				bool spaceFound = false;
				List<Building> houses = s.GetAllBuildingsOfType(BuildingType.House);
				foreach(Building house in houses)
				{
					if(house.gameObject.GetComponent<House>().residents.Count < house.gameObject.GetComponent<House>().maxResidents)
					{
						spaceFound = true;
						break;
					}
				}
				if(spaceFound)
				{
					locs.Add(s.gameObject.transform.position);
				}
			}
			return locs.ToArray();
		}

		public void GetNewFactionLeader()
		{
			if(members.Count == 0)
			{
				return;
			}
			UnitAI currentBest = members[0];
			foreach(UnitAI ai in members)
			{
				if(currentBest != null)
				{
					if(ai.leadershipAbility > currentBest.leadershipAbility)
					{
						currentBest = ai;
					}
				}
			}
			factionLeader = currentBest;
			factionLeader.speciality = Speciality.Builder;
		}

		public void RequestNewSettlement()
		{
			if(factionLeader == null)
			{
				GetNewFactionLeader();
			}
			if(!FirstFindHomeRequested)
			{
				FirstFindHomeRequested = true;
				Debug.Log("Faction Started Finding Settlement Locs!");
				MinMax fertReq = new MinMax();
				fertReq.AddValue(0.4f);
				fertReq.AddValue(1.0f);
				Vector3[] possibleLocations = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetAllVerticesThatMatchArguements(factionLeader.racialAttributes.temperaturePreferenceRange, fertReq);
				if(possibleLocations.Length > 0)
				{
					Debug.Log("Unit FOUND Settlement Locs!");
					factionLeader.actionFlag = AIActionFlag.BuildSettlement;
					factionLeader.state = UnitAI.State.Moving;
					factionLeader.seeker.StartMultiTargetPath(factionLeader.transform.position,possibleLocations, false);
				}
				else
				{
					Debug.Log("Faction FOUND *NO* Settlement Locs!");
				}
			}
		}

		public void RegisterNewSettlement(Settlement s)
		{
			settlements.Add(s);
			int b = 5;
			if(members.Count < b)
			{
				b = members.Count;
			}
			for(int a = 0; a < b; a++)
			{
				members[a].homeSettlement = s;
			}
		}

		public void RequestNewHouse(Settlement s)
		{
			if(factionLeader == null)
			{
				GetNewFactionLeader();
			}
			if(s.GetFirstHouseWithRoom() == null)
			{
				Vector3 buildSpot = s.FindFirstAvailableBuildingSpot();
				GameObject obj = gM.PlaceObject(gM.buildingPrefabs[((int)BuildingType.House)+1], buildSpot);
				obj.transform.parent = s.transform;
				House h = obj.AddComponent<House>();
				s.RegisterBuildingWithSettlement(h, buildSpot);
			}
		}
		GameObject BuildResourceImprovement(ResourceInfoDatabase.ResourceClassification resourceClass, Vector3 spot, Settlement s)
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
				s.AddBuildingToBuildQueue(b);
				s.RegisterFarmImprovement(b);
				return obj;
			}
			return null;
		}

		void HandleConstruction(Settlement s)
		{
			if(s.improvements.Count < s.residents/5 && techTree.availableBuildings[(int)BuildingType.Farm])
			{
				//Build a farm
				Debug.Log("//////////////// Planning Farm Construction.");
				//BuildResourceImprovement(ResourceInfoDatabase.ResourceClassification.Plant);
				Vector3[] spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResources(gM.currentPlanet.plants);
				if(spawns.Length > 0)
				{
					Debug.Log("Spawns length: " + spawns.Length);
					Pathfinding.MultiTargetPath mtp = s.seeker.StartMultiTargetPath(s.transform.position, spawns, false);
					mtp.BlockUntilCalculated();
					ResourceInfoDatabase.ResourceSpawnPoint spawn = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetResourceSpawnPointByLocation(mtp.originalEndPoint);
					if(spawn != null &&  spawn.improved == false)
					{
						GameObject.Destroy(spawn.resourceObject);
						spawn.resourceObject = BuildResourceImprovement(ResourceInfoDatabase.ResourceClassification.Plant, spawn.vertexTypeHolder.pos, s);
						spawn.improved = true;
					}
					s.seeker.ReleaseClaimedPath();
				}
				else
				{
					Debug.Log("No plant spawns to build farms on.");
				}
			}
		}

		public void Update()
		{
			//Debug.Log("Faction updating!");
			infoBar.UpdateUI();
			if(techTree.GetCanContinueResearch())
			{
				techTree.GetNextMostImportantTech(factionLeader.racialAttributes);
			}
			/*
			foreach(Settlement s in settlements)
			{
				HandleConstruction(s);
			}
			*/
			actionTimer -= Time.deltaTime;
			if(actionTimer <= 0.0f)
			{
				if(actionQueue.Count > 0)
				{
					actionQueue[0].PerformAction();
					actionQueue.RemoveAt(0);
				}
				for(int a = actionQueue.Count; a < settlements.Count; a++)
				{
					DecideNewAction();
				}
				actionTimer = 30.0f - (1.6f * factionLeader.racialAttributes.intelligence);
			}
		}

		int GetTotalNumImprovementsOfType(BuildingType bType)
		{
			int count = 0;
			foreach(Settlement s in settlements)
			{
				foreach(Settlement.SettlementBuildingKeeper b in s.improvements)
				{
					if(b.building.buildingType == bType)
					{
						count++;
					}
				}
			}
			return count;
		}

		void DecideNewAction()
		{
			//Do all the complex calculations of what action to take in here -- this will likely get messy and need to me transplanted
			float[] needs = new float[(int)NeedWeights.NUM_TYPES];
			RacialAttributes racials = factionLeader.racialAttributes;
			//Calculate food needs
			needs[(int)NeedWeights.Food] += ((Mathf.Clamp(members.Count/5, 1.0f, 500.0f)) / (1+GetTotalNumImprovementsOfType(BuildingType.Farm))) * racials.wisdom;


			NeedWeights mostImportant = NeedWeights.Food;
			for(int a = 1; a < needs.Length; a++)
			{
				if(needs[a] > needs[(int)mostImportant])
				{
					mostImportant = (NeedWeights)a;
				}
			}

			if(mostImportant == NeedWeights.Food && techTree.availableBuildings[(int)BuildingType.Farm])
			{
				Vector3[] spawns = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetLocationsOfChosenResources(gM.currentPlanet.plants);
				Settlement chosen = settlements[0];
				for(int a = 1; a < settlements.Count; a++)
				{
					if(settlements[a].stockpile.GetTotalAmountOfClassification(gM.currentPlanet.plants) < chosen.stockpile.GetTotalAmountOfClassification(gM.currentPlanet.plants))
					{
						chosen = settlements[a];
					}
				}
				if(spawns.Length > 0)
				{
					Debug.Log("Spawns length: " + spawns.Length);
					Pathfinding.MultiTargetPath mtp = chosen.seeker.StartMultiTargetPath(chosen.transform.position, spawns, false);
					mtp.BlockUntilCalculated();
					ResourceInfoDatabase.ResourceSpawnPoint spawn = gM.currentPlanet.planetFeatureSettings.resourceInfoDatabase.GetResourceSpawnPointByLocation(mtp.originalEndPoint);
					if(spawn != null &&  spawn.improved == false)
					{
						List<Building.GenericMaterialCost> genericCosts = new List<Building.GenericMaterialCost>();
						Building.GenericMaterialCost cost = new Building.GenericMaterialCost();
						cost.amountReq = 20;
						cost.amountStored = 0;
						cost.type = ResourceInfoDatabase.ResourceClassification.BuildingMaterial;
						genericCosts.Add(cost);
						ConstructImprovement toAdd =
							new ConstructImprovement("Farm", BuildingType.Farm, spawn.vertexTypeHolder.pos, genericCosts, null, chosen);
						actionQueue.Add(toAdd);
						return;
					}
					chosen.seeker.ReleaseClaimedPath();
				}
				else
				{
					Debug.Log("No plant spawns to build farms on.");
				}
			}
		}
	}
}
