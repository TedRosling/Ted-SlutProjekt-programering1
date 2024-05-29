using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

public class Simulation : MonoBehaviour
{
	//there are 4 spawn modes at the moment that make the pattern look diferent :D
	public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

	//sets different kernels to numbers (kernels are used in HLSL and therefore need a reference that c# can understand)
	const int updateKernel = 0;
	const int diffuseMapKernel = 1;

	//setting up references to scripts..
	public ComputeShader compute;
	public ComputeShader drawAgentsCS;

	public SlimeSettings settings;

	//display settings :D
	public bool showAgentsOnly;
	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;

	//Johan im not sure if you will go through my code but, is it a good or bad thing to write it like this? (i mean the serialize field, hide in inspector thing)
	[SerializeField, HideInInspector] protected RenderTexture trailMap;
	[SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
	[SerializeField, HideInInspector] protected RenderTexture displayTexture;

	ComputeBuffer agentBuffer;
	ComputeBuffer settingsBuffer;
	Texture2D colourMapTexture;

	//creates reference for rendering onto the screen
	protected virtual void Start()
	{
		Init();
		transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = displayTexture;
	}


	void Init()
	{

		//Creates texture
		ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);

		//Creates agents with start pos and rot
		Agent[] agents = new Agent[settings.numAgents];
		for (int i = 0; i < agents.Length; i++)
		{
			//finds centre of screen by taking half of the height and half of the width and using that as cords
			Vector2 centre = new Vector2(settings.width / 2, settings.height / 2);
			
			Vector2 startPos = Vector2.zero;

			float randomAngle = Random.value * Mathf.PI * 2;
			float angle = 0;

			//dif spawn modes selected in editor
			if (settings.spawnMode == SpawnMode.Point)
			{
				startPos = centre;
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.Random)
			{
				startPos = new Vector2(Random.Range(0, settings.width), Random.Range(0, settings.height));
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.InwardCircle)
			{
				startPos = centre + Random.insideUnitCircle * settings.height * 0.5f;
				angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
			}
			else if (settings.spawnMode == SpawnMode.RandomCircle)
			{
				startPos = centre + Random.insideUnitCircle * settings.height * 0.15f;
				angle = randomAngle;
			}
			
			Vector3Int speciesMask;
			int speciesIndex = 0;

			if (settings.numSpecies == 1)
			{
				speciesMask = Vector3Int.one;
			}
			else
			{
				int species = Random.Range(1, settings.numSpecies + 1);
				speciesIndex = species - 1;
				speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
			}



			agents[i] = new Agent() { position = startPos, angle = angle, speciesMask = speciesMask, speciesIndex = speciesIndex };
		}

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);
		compute.SetInt("numAgents", settings.numAgents);
		drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
		drawAgentsCS.SetInt("numAgents", settings.numAgents);


		compute.SetInt("width", settings.width);
		compute.SetInt("height", settings.height);


	}

	//calls run sim method
	void Update()
	{
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
		}
	}

	//runs the simulation
	void RunSimulation()
	{

		var speciesSettings = settings.speciesSettings;
		ComputeHelper.CreateStructuredBuffer(ref settingsBuffer, speciesSettings);
		compute.SetBuffer(0, "speciesSettings", settingsBuffer);


		// Assign textures
		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);

		// Assign settings
		compute.SetFloat("deltaTime", Time.deltaTime);
		compute.SetFloat("time", Time.time);

		compute.SetFloat("trailWeight", settings.trailWeight);
		compute.SetFloat("decayRate", settings.decayRate);
		compute.SetFloat("diffuseRate", settings.diffuseRate);


		ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
		ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: diffuseMapKernel);

		ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);

		if (showAgentsOnly)
		{
			ComputeHelper.ClearRenderTexture(displayTexture);

			drawAgentsCS.SetTexture(0, "TargetTexture", displayTexture);
			ComputeHelper.Dispatch(drawAgentsCS, settings.numAgents, 1, 1, 0);

		}
		else
		{
			ComputeHelper.CopyRenderTexture(trailMap, displayTexture);
		}

	}

	void OnDestroy()
	{

		ComputeHelper.Release(agentBuffer, settingsBuffer);
	}

	//creates the structure for the so called "agents"
	public struct Agent
	{
		public Vector2 position;
		public float angle;
		public Vector3Int speciesMask;
		int unusedSpeciesChannel;
		public int speciesIndex;
	}


}
