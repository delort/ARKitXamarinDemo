﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Foundation;
using UIKit;
using Urho;
using Urho.Navigation;
using Urho.Shapes;
using Urho.SharpReality;

namespace ARKitXamarinDemo
{
	public class CrowdDemo : ArkitApp
	{
		[Preserve]
		public CrowdDemo(ApplicationOptions opts) : base(opts)
		{
		}

		Node environmentNode;
		Material spatialMaterial;
		Node armyNode;
		CrowdManager crowdManager;
		bool debug = true;
		bool surfaceIsValid;
		bool positionIsSelected;

		readonly Color validPositionColor = Color.Green;
		readonly Color invalidPositionColor = Color.Red;

		const string WalkingAnimation = @"Animations/Mutant_Run.ani";
		const string IdleAnimation = @"Animations/Mutant_Idle0.ani";
		const string DeathAnimation = @"Animations/Mutant_Death.ani";

		protected override async void Start()
		{
			base.Start();
			CreateArScene();
			ResourceCache.GetMaterial("Materials/mutant_M.xml");
			Renderer.ReuseShadowMaps = false;
			environmentNode = Scene.CreateChild();
			//Scene.CreateComponent<SpatialCursor>();
			Input.TouchEnd += e => OnGestureTapped();
		}

		void SubscribeToEvents()
		{
			Engine.PostRenderUpdate += args => {
				if (debug)
				{
					Scene.GetComponent<NavigationMesh>().DrawDebugGeometry(true);
					crowdManager.DrawDebugGeometry(true);
				}
			};

			crowdManager.CrowdAgentReposition += (args) =>
			{
				Vector3 velocity = args.Velocity * -1;
				Node node = args.Node;
				AnimationController animCtrl = node.GetComponent<AnimationController>();
				if (animCtrl != null)
				{
					float speed = velocity.Length;
					if (animCtrl.IsPlaying(WalkingAnimation))
					{
						float speedRatio = speed / args.CrowdAgent.MaxSpeed;
						// Face the direction of its velocity but moderate the turning speed based on the speed ratio as we do not have timeStep here
						node.SetRotationSilent(Quaternion.FromRotationTo(Vector3.UnitZ, velocity));
						// Throttle the animation speed based on agent speed ratio (ratio = 1 is full throttle)
						animCtrl.SetSpeed(WalkingAnimation, speedRatio);
					}
					else
						animCtrl.Play(WalkingAnimation, 0, true, 0.1f);

					// If speed is too low then stopping the animation
					if (speed < args.CrowdAgent.Radius)
					{
						animCtrl.Stop(WalkingAnimation, 0.8f);
						animCtrl.Play(IdleAnimation, 0, true, 0.2f);
					}
				}
			};
		}

		void KillAll()
		{
			foreach (var node in armyNode.Children.ToArray())
			{
				var anim = node.GetComponent<AnimationController>();
				var agent = node.GetComponent<CrowdAgent>();
				agent?.Remove();
				anim.Play(DeathAnimation, 0, false, 0.4f);
			}
		}

		Vector3? Raycast()
		{
			Ray cameraRay = Camera.GetScreenRay(0.5f, 0.5f);
			var result = Scene.GetComponent<Octree>().RaycastSingle(cameraRay, RayQueryLevel.Triangle, 100, DrawableFlags.Geometry, 0x70000000);
			if (result != null)
			{
				return result.Value.Position;
			}
			return null;
		}

		void SpawnMutant(Vector3 pos, string name = "Mutant")
		{
			Node mutantNode = armyNode.CreateChild(name);
			mutantNode.Position = pos;
			mutantNode.SetScale(0.12f);
			var modelObject = mutantNode.CreateComponent<AnimatedModel>();
			modelObject.Model = ResourceCache.GetModel("Models/Mutant.mdl");
			modelObject.SetMaterial(ResourceCache.GetMaterial("Materials/mutant_M.xml"));
			modelObject.CastShadows = true;
			mutantNode.CreateComponent<AnimationController>().Play(IdleAnimation, 0, true, 0.2f);

			// Create the CrowdAgent
			var agent = mutantNode.CreateComponent<CrowdAgent>();
			agent.Height = 0.2f;
			agent.NavigationPushiness = NavigationPushiness.Medium;
			agent.MaxSpeed = 0.4f;
			agent.MaxAccel = 0.4f;
			agent.Radius = 0.03f;
			agent.NavigationQuality = NavigationQuality.High;
		}

		public async void OnGestureTapped()
		{
			NavigationMesh navMesh;
			Vector3 hitPos = new Vector3(0, -1, 2);

			System.Console.WriteLine("Tapped!" + positionIsSelected);
			if (!positionIsSelected)
			{
				positionIsSelected = true;
				navMesh = Scene.CreateComponent<NavigationMesh>();

				//this plane is a workaround 
				//TODO: build a navmesh using spatial data
				var planeNode = Scene.CreateChild();
				var plane = planeNode.CreateComponent<StaticModel>();
				plane.Model = CoreAssets.Models.Plane;
				//plane.SetMaterial(Material.FromColor(Color.Transparent));
				plane.SetMaterial(ResourceCache.GetMaterial("tttt.xml"));
				planeNode.Scale = new Vector3(20, 0.1f, 20);
				planeNode.Position = hitPos;

				Scene.CreateComponent<Navigable>();

				navMesh.CellSize = 0.2f;
				navMesh.CellHeight = 0.02f;
				navMesh.DrawOffMeshConnections = true;
				navMesh.DrawNavAreas = true;
				navMesh.TileSize = 2;
				navMesh.AgentRadius = 0.05f;

				navMesh.Build();

				crowdManager = Scene.CreateComponent<CrowdManager>();
				var parameters = crowdManager.GetObstacleAvoidanceParams(0);
				parameters.VelBias = 0.5f;
				parameters.AdaptiveDivs = 7;
				parameters.AdaptiveRings = 3;
				parameters.AdaptiveDepth = 3;
				crowdManager.SetObstacleAvoidanceParams(0, parameters);
				armyNode = Scene.CreateChild();

				SubscribeToEvents();

				int mutantIndex = 1;
				for (int i = 0; i < 4; i++)
				for (int j = 0; j < 4; j++)
					SpawnMutant(new Vector3(hitPos.X + 0.15f * i, hitPos.Y - 0.1f, hitPos.Z + 0.13f * j), "Mutant " + mutantIndex++);

				return;
			}

			if (positionIsSelected)
			{
				var newhitPos = Raycast();
				if (newhitPos == null)
					return;
				await Delay(1);
				//Scene.GetComponent<SpatialCursor>().ClickAnimation();
				navMesh = Scene.GetComponent<NavigationMesh>();
				Vector3 pathPos = navMesh.FindNearestPoint(newhitPos.Value, new Vector3(0.1f, 0.1f, 0.1f));
				Scene.GetComponent<CrowdManager>().SetCrowdTarget(pathPos, Scene);
			}
		}
	}
}