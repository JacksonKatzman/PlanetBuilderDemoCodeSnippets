using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Planet : MonoBehaviour
{
	[Range(2,256)]
	public int resolution = 10;
	int startingResolution = 10;
	public bool autoUpdate = true;
	public enum FaceRenderMask {All, Top, Bottom, Left, Right, Front, Back};
	public FaceRenderMask faceRenderMask;

	public ShapeSettings shapeSettings;
	public ColorSettings colorSettings;
	public PlanetFeatureSettings planetFeatureSettings;

	[HideInInspector]
	public bool shapeSettingsFoldout;
	[HideInInspector]
	public bool colorSettingsFoldout;
	[HideInInspector]
	public bool planetFeatureSettingsFoldout;

	ShapeGenerator shapeGenerator = new ShapeGenerator();
	ColorGenerator colorGenerator = new ColorGenerator();

	public PlanetTileDatabase planetTileDatabase;

	[SerializeField, HideInInspector]
	MeshFilter[] meshFilters;
	TerrainFace[] terrainFaces;
	MeshCollider[] meshColliders;
	public MeshFilter finalMesh;
	public GameObject lowResMeshHolder;
	public MeshFilter lowResMesh;

	SphereCollider sphereCollider;

	public int[] buildingMats;
	public int[] plants;
	public int[] meats;

	void Start()
	{
		//Handled in Initialize -- Called in the GameManager
	}

	void Initialize()
	{
		shapeGenerator.UpdateSettings(shapeSettings);
		colorGenerator.UpdateSettings(colorSettings);

		if(meshFilters == null || meshFilters.Length == 0)
		{
			meshFilters = new MeshFilter[6];
			meshColliders = new MeshCollider[6];

		}
		terrainFaces = new TerrainFace[6];

		Vector3[] directions = {Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back};

		for(int i = 0; i < 6; i++)
		{
			if(meshFilters[i] == null)
			{
				GameObject meshObj = new GameObject("mesh");
				meshObj.transform.parent = transform;
				//meshObj.AddComponent
				meshObj.AddComponent<MeshRenderer>();
				meshObj.AddComponent<PlanetMeshIndexTracker>();
				meshObj.GetComponent<PlanetMeshIndexTracker>().planetMeshIndex = i;
				meshFilters[i] = meshObj.AddComponent<MeshFilter>();
				meshFilters[i].sharedMesh = new Mesh();
				meshColliders[i] = meshObj.AddComponent<MeshCollider>();
			}
			meshFilters[i].GetComponent<MeshRenderer>().sharedMaterial = colorSettings.planetMaterial;

			terrainFaces[i] = new TerrainFace(shapeGenerator, meshFilters[i].sharedMesh, resolution, directions[i]);
			bool renderFace = faceRenderMask == FaceRenderMask.All || (int)faceRenderMask - 1 == i;
			meshFilters[i].gameObject.SetActive(renderFace);

		}
			
		Debug.Log("Completed Planet Init!");
	}

	public void BuildResourceAvailabilityArrays()
	{
		buildingMats = planetFeatureSettings.GetAllAllowedResourcesOfClassification(ResourceInfoDatabase.ResourceClassification.BuildingMaterial);
		plants = planetFeatureSettings.GetAllAllowedResourcesOfClassification(ResourceInfoDatabase.ResourceClassification.Plant);
		meats = planetFeatureSettings.GetAllAllowedResourcesOfClassification(ResourceInfoDatabase.ResourceClassification.Meat);
	}

	public void BeginMeshConstruction()
	{
		CreateLowResTileMap();
		Invoke("MergePlanetMeshes", 0.2f);
	}

	public void CreateLowResTileMap()
	{
		startingResolution = resolution;
		resolution = 6;
		OnShapeSettingsUpdated();

		CombineInstance[] combine = new CombineInstance[meshFilters.Length];

		int i = 0;
		while (i < meshFilters.Length)
		{
			combine[i].mesh = meshFilters[i].sharedMesh;
			combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
			//meshFilters[i].gameObject.SetActive(false);

			i++;
		}
		lowResMesh.sharedMesh = new Mesh();
		lowResMesh.sharedMesh.CombineMeshes(combine);
		lowResMeshHolder.GetComponent<MeshCollider>().sharedMesh = lowResMesh.sharedMesh;
		resolution = startingResolution;
	}

	public void MergePlanetMeshes()
	{
		startingResolution = resolution;
		resolution = 22;
		OnShapeSettingsUpdated();

		CombineInstance[] combine = new CombineInstance[meshFilters.Length];

		int i = 0;
		while (i < meshFilters.Length)
		{
			combine[i].mesh = meshFilters[i].sharedMesh;
			combine[i].transform = meshFilters[i].transform.localToWorldMatrix;

			i++;
		}
		finalMesh.sharedMesh = new Mesh();
		finalMesh.sharedMesh.CombineMeshes(combine);
		transform.gameObject.SetActive(true);


		Invoke("GenerateNavMesh", 0.1f);
	}

	void SetOceansAsOceans()
	{
		
		Pathfinding.NavMeshGraph navMeshGraph = (Pathfinding.NavMeshGraph)AstarPath.active.data.graphs[0];
		AstarPath.active.AddWorkItem(()=>
			{
				//Pathfinding.NavmeshTile tile = navMeshGraph.til
				//Debug.Log("NUMBER OF TILES X: " + navMeshGraph.tileXCount.ToString() + " Z: " + navMeshGraph.tileZCount.ToString());
				Pathfinding.NavmeshTile tile = navMeshGraph.GetTile(0,0);
				//Debug.Log("Number of nodes in Tile: " + tile.nodes.Length.ToString());
				for(int i = 0; i < tile.nodes.Length; i++)
				{
					Pathfinding.TriangleMeshNode node = tile.nodes[i];
					Vector3 nodePos = (Vector3)node.position;
					//Debug.Log("Node pos at: " + nodePos.ToString());
					float distanceFromCenter = Mathf.Abs((nodePos - transform.position).magnitude);
					float distanceFromSeaLevel = (distanceFromCenter - shapeSettings.planetRadius) * 100;
					//Debug.Log("Node distance from seaLevel: " + distanceFromSeaLevel.ToString());
					if(distanceFromSeaLevel <= 0.5f)
					{
						Debug.Log("Point is in ocean");
						node.Tag = (uint)1;
					}
					else
					{
						Debug.Log("Point is on land");
					}
				}
			});

	}

	void GenerateNavMesh()
	{
		//Debug.Log(AstarPath.active.data.graphs.Length.ToString());
		Pathfinding.NavMeshGraph navMeshGraph = (Pathfinding.NavMeshGraph)AstarPath.active.data.graphs[0];
		//Debug.Log(AstarPath.active.data.GetGraphIndex(navMeshGraph));
		navMeshGraph.sourceMesh = finalMesh.sharedMesh;
		var graphsToScan = new [] {AstarPath.active.data.graphs[0]};
		//SetOceansAsOceans();
		AstarPath.active.Scan(graphsToScan);
		Invoke("SetOceansAsOceans", 0.1f);

		resolution = startingResolution;
		Invoke("OnShapeSettingsUpdated", 0.1f);
		Invoke("OnColorSettingsUpdated", 0.1f);
	}

	public void GeneratePlanet()
	{
		Initialize();
		GenerateMesh();
		GenerateColors();
	}

	public void OnShapeSettingsUpdated()
	{
		if(autoUpdate)
		{
			Initialize();
			GenerateMesh();
		}
	}
	public void OnColorSettingsUpdated()
	{
		if(autoUpdate)
		{
			Initialize();
			GenerateColors();
		}
	}

	public void OnPlanetFeatureSettingsUpdated()
	{
		if(autoUpdate)
		{
			
		}
	}

	void GenerateMesh()
	{
		for(int i = 0; i < 6; i++)
		{
			if(meshFilters[i].gameObject.activeSelf)
			{
				terrainFaces[i].ConstructMesh();
				meshColliders[i].sharedMesh = meshFilters[i].sharedMesh;
			}
		}

		colorGenerator.UpdateElevation(shapeGenerator.elevationMinMax);
		Debug.Log("Completed Generate Planet Mesh!");
	}

	void GenerateColors()
	{
		colorGenerator.UpdateColors();
		for(int i = 0; i < 6; i++)
		{
			if(meshFilters[i].gameObject.activeSelf)
			{
				terrainFaces[i].UpdateUVs(colorGenerator);
			}
		}
		Debug.Log("Completed Generate Planet Colors!");
	}

	public void UpdateOceanType(int oceanType, GameManager gMan)
	{
		planetFeatureSettings.oceanType = (PlanetFeatureSettings.OceanType)(oceanType);
		PlanetFeatureSettings.OceanType chosenType = planetFeatureSettings.oceanType;
		switch (chosenType)
		{                
		case PlanetFeatureSettings.OceanType.Water:
				planetFeatureSettings.hasLiquidWater = true;
				colorSettings.oceanColor = gMan.gradientKeeper.BasicWater;
			break;
		case PlanetFeatureSettings.OceanType.Lava:
				planetFeatureSettings.hasLiquidWater = false;
				colorSettings.oceanColor = gMan.gradientKeeper.BasicLava;
			break;
		case PlanetFeatureSettings.OceanType.Gas:
				planetFeatureSettings.hasLiquidWater = false;
				colorSettings.oceanColor = gMan.gradientKeeper.BasicGas;
			break;
		case PlanetFeatureSettings.OceanType.None:
				planetFeatureSettings.hasLiquidWater = false;
				if(colorSettings.biomeColorSettings.biomes.Length > 0)
				{
					colorSettings.oceanColor = colorSettings.biomeColorSettings.biomes[0].gradient;
				}
				else
				{
					colorSettings.oceanColor = gMan.gradientKeeper.BasicLava;
				}
			break;
		}

		OnColorSettingsUpdated();
	}

	public void UpdatePlanetSize(float newSize)
	{
		shapeSettings.planetRadius = newSize;
		OnShapeSettingsUpdated();
	}

	public void UpdateLandMassAmount(float newSize)
	{
		if(shapeSettings.noiseLayers.Length > 0)
		{			
			shapeSettings.noiseLayers[0].noiseSettings.simpleNoiseSettings.minValue = Mathf.Clamp(newSize, 0.8f, 2.0f);
		}
		OnShapeSettingsUpdated();
	}

	public void RandomizeLandMass()
	{
		if(shapeSettings.noiseLayers.Length > 0)
		{			
			shapeSettings.noiseLayers[0].noiseSettings.simpleNoiseSettings.center = new Vector3(Random.Range(0.0f, 2.0f),Random.Range(0.0f, 2.0f),Random.Range(0.0f, 2.0f));
		}
		OnShapeSettingsUpdated();
	}

	public void UpdateMountainLevel(float newLevel)
	{
		if(shapeSettings.noiseLayers.Length > 2)
		{			
			shapeSettings.noiseLayers[2].noiseSettings.rigidNoiseSettings.strength = newLevel;
		}
		OnShapeSettingsUpdated();
	}

	public void RandomizeMountains()
	{
		if(shapeSettings.noiseLayers.Length > 2)
		{			
			shapeSettings.noiseLayers[2].noiseSettings.rigidNoiseSettings.center = new Vector3(Random.Range(0.0f, 2.0f),Random.Range(0.0f, 2.0f),Random.Range(0.0f, 2.0f));
		}
		OnShapeSettingsUpdated();
	}

	public void UpdateEquatorTemp(int newTemp)
	{
		planetFeatureSettings.equatorTempterature = newTemp;
	}

	public float GetTemperatureAtLocation(float yValue)
	{
		float changeTemp = Mathf.Clamp01((Mathf.Abs(0.0f - yValue))/shapeSettings.planetRadius);
		float helper = (1 - changeTemp)+0.0001f;
		//float testing = Mathf.Clamp(((1/helper)-1.0f)*10.0f, 0, 120);
		float testing = ((1/helper)-1.0f)*10.0f;
		if(testing > planetFeatureSettings.equatorTempterature * 1.50f)
		{
			testing = planetFeatureSettings.equatorTempterature * 1.50f;
		}

		float calculatedTemp = planetFeatureSettings.equatorTempterature - testing;
		return calculatedTemp;
	}

	public void UpdateSpecificBiome(int biomeListID, Gradient newGradient)
	{
		this.colorSettings.biomeColorSettings.biomes[biomeListID].gradient = newGradient;

		OnColorSettingsUpdated();
	}

	public void InitializePlanetTiles()
	{
		planetTileDatabase = new PlanetTileDatabase();
		planetTileDatabase.planetTiles = new PlanetTileDatabase.PlanetTile[lowResMesh.sharedMesh.triangles.Length/3];
		planetTileDatabase.planet = this;
		planetTileDatabase.InitializePlanetTiles();
		Debug.Log("Number of Planet Tiles: " + planetTileDatabase.planetTiles.Length.ToString());
		planetTileDatabase.lowResMesh = lowResMesh;
		planetTileDatabase.GetAllMidPositions();
		planetTileDatabase.GetMinMaxYForTiles();
		planetTileDatabase.AssignTileBiomes();
	}

}
