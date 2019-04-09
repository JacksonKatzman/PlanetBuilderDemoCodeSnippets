using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EnumStorage;
using TechnologyTrees;
using Pathfinding;

namespace AIDecisionObjects
{
	public class Package
	{
		public PlanetFeatureSettings.PlanetResourceType type;
		public int amount;
		public bool sacred;

		public Package (PlanetFeatureSettings.PlanetResourceType type, int amount)
		{
			this.type = type;
			this.amount = amount;
			sacred = false;
		}
		
	}
	public class Building : MonoBehaviour
	{
		[System.Serializable]
		public class GenericMaterialCost
		{
			public ResourceInfoDatabase.ResourceClassification type;
			public int amountReq;
			public int amountStored;
		}
		[System.Serializable]
		public class SpecificMaterialCost
		{
			public PlanetFeatureSettings.PlanetResourceType type;
			public int amountReq;
			public int amountStored;
		}
		public List<GenericMaterialCost> genericBuildCost;
		public List<SpecificMaterialCost> specificBuildCost;
		public BuildingType buildingType;
		public bool built = false;
		//TODO:
		//Rework workable buildings to have max worker limit
		//Add list here to store and checks when choosing a place to gather from as well as removing self from worker when leaving
	}
	public class House : Building
	{
		//TODO: FIX COSTS LATER
		public List<UnitAI> residents;
		public int maxResidents = 5;

		public House()
		{
			Initialize();
		}

		public void Initialize()
		{
			residents = new List<UnitAI>();
			//genericBuildCost = new GenericMaterialCost[1];
			//genericBuildCost[0].type = ResourceInfoDatabase.ResourceClassification.BuildingMaterial;
			//genericBuildCost[0].amount = 5;
			//specificBuildCost = null;
			buildingType = BuildingType.House;
		}

		public bool ApplyForHousing(UnitAI ai)
		{
			Debug.Log("Res Count: " + residents.Count.ToString());
			Debug.Log("Max Res: " + maxResidents.ToString());
			if(residents.Count < maxResidents)
			{
				ai.house = this;
				residents.Add(ai);
				Debug.Log("RESIDENT REGISTERED TO HOUSE!! &&&");
				return true;
			}
			else
			{
				return false;
			}
		}
	}



	[System.Serializable]
	public class Settlement : MonoBehaviour
	{
		[System.Serializable]
		public class SettlementBuildingKeeper
		{
			public Vector3 location;
			public Building building;
		}
		public enum SettlementType { Settlement, Hamlet, Village, Town, City};
		public SettlementType settlementType;
		public List<SettlementBuildingKeeper> buildings;
		public List<SettlementBuildingKeeper> improvements;
		public List<Building> buildQueue;
		public ResourceStockpile stockpile;
		public int residents;
		public Seeker seeker;

		public Settlement()
		{
			settlementType = SettlementType.Settlement;
			residents = 0;
		}
		public void Initialize()
		{
			Debug.Log("Settlement INIT:");
			buildings = new List<SettlementBuildingKeeper>();
			improvements = new List<SettlementBuildingKeeper>();
			buildQueue = new List<Building>();
			Debug.Log("Settlement Child count: " + gameObject.transform.childCount.ToString());
			for(int i = 0; i < gameObject.transform.childCount - 1; i++)
			{
				Debug.Log("Assigning Child: " + i + " at loc: " + gameObject.transform.GetChild(i).transform.position.ToString());
				SettlementBuildingKeeper sbk = new SettlementBuildingKeeper();
				sbk.building = null;
				sbk.location = gameObject.transform.GetChild(i).transform.position;
				buildings.Add(sbk);
			}
			stockpile = new ResourceStockpile();
			stockpile.stockpileObj = transform.GetChild(transform.childCount - 1).gameObject;
			seeker = gameObject.AddComponent<Seeker>();
		}
		public int GetMaxBuildings()
		{
			return ((int)settlementType + 1) * 6;
		}

		public bool RegisterBuildingWithSettlement(Building b, Vector3 loc)
		{
			for(int i = 0; i < buildings.Count; i++)
			{
				if(loc == buildings[i].location && buildings[i].building == null)
				{
					buildings[i].building = b;
					return true;
				}
			}
			return false;
		}

		public void RegisterFarmImprovement(Building b)
		{
			SettlementBuildingKeeper s = new SettlementBuildingKeeper();
			s.building = b;
			s.location = b.transform.position;
			improvements.Add(s);
		}

		public void AddBuildingToBuildQueue(Building b)
		{
			buildQueue.Add(b);
		}

		public Vector3 FindFirstAvailableBuildingSpot()
		{
			Debug.Log("TOTAL SPOTS: " + buildings.Count);
			for(int i = 0; i < buildings.Count; i++)
			{
				if(buildings[i].building == null)
				{
					return buildings[i].location;
				}
			}
			return Vector3.zero;
		}

		public List<Building> GetAllBuildingsOfType(BuildingType type)
		{
			List<Building> toReturn = new List<Building>();
			foreach(SettlementBuildingKeeper b in buildings)
			{
				if(b.building != null)
				{
					if(b.building.buildingType == type)
					{
						toReturn.Add(b.building);
					}
				}
			}
			return toReturn;
		}

		public House GetFirstHouseWithRoom()
		{
			List<Building> houses = GetAllBuildingsOfType(BuildingType.House);
			foreach(Building house in houses)
			{
				if(house.gameObject.GetComponent<House>().residents.Count < house.gameObject.GetComponent<House>().maxResidents)
				{
					return house.gameObject.GetComponent<House>();
				}
			}
			return null;
		}
	}

	[System.Serializable]
	public class ResourceStockpile
	{
		public GameObject stockpileObj;
		public int[] resources = new int[(int)PlanetFeatureSettings.PlanetResourceType.NUM_RESOURCES];

		public int GetTotalAmountOfClassification(int[] list)
		{
			int tracker = 0;
			for(int i = 0; i < list.Length; i++)
			{
				tracker += resources[list[i]];
			}
			return tracker;
		}
		public void RemoveMatsSpreadAroundClassification(int[] list, int amount)
		{
			int i = 0;
			while(amount > 0)
			{
				if(amount <= resources[list[i]])
				{
					int temp = amount;
					amount -= resources[list[i]];
					resources[list[i]] -= temp;
				}
				else
				{
					amount -= resources[list[i]];
					resources[list[i]] = 0;
				}
				i++;
			}
		}
		public Package RequestMatsOfClassification(int[] list, int amount)
		{
			Package ret = new Package(PlanetFeatureSettings.PlanetResourceType.NUM_RESOURCES, 0);
			ret.amount = 0;
			int temp = FindResourceWithLargestStockpileOfClassification(list);
			if(temp != -1)
			{
				if(resources[temp] >= amount)
				{
					ret.amount = amount;
					ret.type = (PlanetFeatureSettings.PlanetResourceType)temp;
					resources[temp] -= amount;
				}
				else
				{
					ret.amount = resources[temp];
					ret.type = (PlanetFeatureSettings.PlanetResourceType)temp;
					resources[temp] -= 0;
				}
				return ret;
			}
			return null;
		}

		int FindResourceWithLargestStockpileOfClassification(int[] list)
		{
			int currentMax = 0;
			int ret = -1;
			for(int i = 0; i < list.Length; i++)
			{
				if(resources[list[i]] > currentMax)
				{
					currentMax = resources[list[i]];
					ret = list[i];
				}
			}
			return ret;
		}
	}
}
