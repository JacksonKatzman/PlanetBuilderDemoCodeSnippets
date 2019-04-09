using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class ShapeSettings : ScriptableObject
{
	public float planetRadius = 1;
	public NoiseLayer[] noiseLayers;

	[System.Serializable]
	public class NoiseLayer
	{
		public bool enabled = true;
		public bool useFirstLayerAsMask;
		public NoiseSettings noiseSettings;

		public NoiseLayer(NoiseLayer other)
		{
			this.enabled = other.enabled;
			this.useFirstLayerAsMask = other.useFirstLayerAsMask;
			this.noiseSettings = new NoiseSettings(other.noiseSettings);
		}
	}

	public ShapeSettings(ShapeSettings other)
	{
		this.planetRadius = other.planetRadius;
		this.noiseLayers = new NoiseLayer[other.noiseLayers.Length];
		for(int i = 0; i < other.noiseLayers.Length; i++)
		{
			this.noiseLayers[i] = new NoiseLayer(other.noiseLayers[i]);
		}
	}
}
