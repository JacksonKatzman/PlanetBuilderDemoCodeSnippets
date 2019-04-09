using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class ColorSettings : ScriptableObject
{
	//public Gradient gradient;
	public Material planetMaterial;
	public BiomeColorSettings biomeColorSettings;
	public Gradient oceanColor;

	[System.Serializable]
	public class BiomeColorSettings
	{
		public Biome[] biomes;
		public NoiseSettings noise;
		public float noiseOffset;
		public float noiseStrength;
		[Range(0,1)]
		public float blendAmount;

		[System.Serializable]
		public class Biome
		{
			public Gradient gradient;
			public Color tint;
			[Range(0,1)]
			public float startHeight;
			[Range(0,1)]
			public float tintPercent;

			public Biome(Biome other)
			{
				Gradient testGrad = new Gradient();
				testGrad.SetKeys(other.gradient.colorKeys, other.gradient.alphaKeys);
				testGrad.mode = other.gradient.mode;
				this.gradient = testGrad;
				this.tint = other.tint;
				this.startHeight = other.startHeight;
				this.tintPercent = other.tintPercent;
			}
		}

		public BiomeColorSettings(BiomeColorSettings other)
		{
			this.noise = new NoiseSettings(other.noise);
			this.noiseOffset = other.noiseOffset;
			this.noiseStrength = other.noiseStrength;
			this.blendAmount = other.blendAmount;
			this.biomes = new Biome[other.biomes.Length];
			for(int i = 0; i < other.biomes.Length; i++)
			{
				this.biomes[i] = new Biome(other.biomes[i]);
			}
		}
	}

	public ColorSettings(ColorSettings other)
	{
		this.planetMaterial = other.planetMaterial;
		//this.biomeColorSettings = other.biomeColorSettings;
		this.biomeColorSettings = new BiomeColorSettings(other.biomeColorSettings);
		this.oceanColor = other.oceanColor;
	}
}
