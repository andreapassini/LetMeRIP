using System;
using System.Collections.Generic;
using System.Reflection;
using Photon.Bolt.Collections;
using Photon.Bolt.Internal;
using Photon.Bolt.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Photon.Bolt
{
	[Preserve]
	public static class BoltDynamicData
	{
		public static void Setup()
		{
			BoltNetworkInternal.DebugDrawer = new UnityDebugDrawer();

#if UNITY_PRO_LICENSE
			BoltNetworkInternal.UsingUnityPro = true;
#else
			BoltNetworkInternal.UsingUnityPro = false;
#endif

			BoltNetworkInternal.GetActiveSceneIndex = GetActiveSceneIndex;
			BoltNetworkInternal.GetSceneName = GetSceneName;
			BoltNetworkInternal.GetSceneIndex = GetSceneIndex;
			BoltNetworkInternal.GetGlobalBehaviourTypes = GetGlobalBehaviourTypes;
			BoltNetworkInternal.EnvironmentSetup = BoltNetworkInternal_User.EnvironmentSetup;
			BoltNetworkInternal.EnvironmentReset = BoltNetworkInternal_User.EnvironmentReset;

			// Setup Unity Config

#if ENABLE_IL2CPP
			UnitySettings.IsBuildIL2CPP = true;
#elif ENABLE_MONO
			UnitySettings.IsBuildMono = true;
#elif ENABLE_DOTNET
			UnitySettings.IsBuildDotNet = true;
#endif

			UnitySettings.CurrentPlatform = Application.platform;
		}

		private static int GetActiveSceneIndex()
		{
			return GetSceneIndex(SceneManager.GetActiveScene().name);
		}

		private static int GetSceneIndex(string name)
		{
			try
			{
				return BoltScenes_Internal.GetSceneIndex(name);
			}
			catch
			{
				return -1;
			}
		}

		private static string GetSceneName(int index)
		{
			try
			{
				return BoltScenes_Internal.GetSceneName(index);
			}
			catch
			{
				return null;
			}
		}

		private static List<STuple<BoltGlobalBehaviourAttribute, Type>> GetGlobalBehaviourTypes()
		{
			var globalBehaviours = new List<STuple<BoltGlobalBehaviourAttribute, Type>>();
			var asmIter = BoltAssemblies.AllAssemblies;
			var assemblyList = AppDomain.CurrentDomain.GetAssemblies();

			while (asmIter.MoveNext())
			{
				try
				{
					// Load Assembly
					var asm = Array.Find(assemblyList, (assembly) => assembly.GetName().Name.Equals(asmIter.Current));

					// Skip of not found
					if (asm == null) { continue; }

					foreach (Type type in asm.GetTypes())
					{
						try
						{
							if (typeof(MonoBehaviour).IsAssignableFrom(type))
							{
								var globalAttr = type.GetCustomAttribute<BoltGlobalBehaviourAttribute>(false);

								if (globalAttr != null)
								{
									globalBehaviours.Add(new STuple<BoltGlobalBehaviourAttribute, Type>(globalAttr, type));
								}
							}
						}
						catch (Exception e2)
						{
							BoltLog.Warn(e2);
						}
					}
				}
				catch (Exception e)
				{
					BoltLog.Warn(e);
				}
			}

			return globalBehaviours;
		}
	}
}
