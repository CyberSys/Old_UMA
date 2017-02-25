using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace UMA
{
	/// <summary>
	/// Default mesh combiner for UMA UMAMeshdata from slots.
	/// </summary>
    public class UMADefaultMeshCombiner : UMAMeshCombiner
    {
        protected List<SkinnedMeshCombiner.CombineInstance> combinedMeshList;
        protected List<Material> combinedMaterialList;

        UMAData umaData;
        int atlasResolution;
		private UMAClothProperties clothProperties;
		int currentRendererIndex;
		SkinnedMeshRenderer[] renderers;

		protected void EnsureUMADataSetup(UMAData umaData)
		{
			if (umaData.umaRoot == null)
			{
				GameObject newRoot = new GameObject("Root");
				//make root of the UMAAvatar respect the layer setting of the UMAAvatar so cameras can just target this layer
				newRoot.layer = umaData.gameObject.layer;
				newRoot.transform.parent = umaData.transform;
				newRoot.transform.localPosition = Vector3.zero;
				newRoot.transform.localRotation = Quaternion.Euler(270f, 0, 0f);
				newRoot.transform.localScale = Vector3.one;
				umaData.umaRoot = newRoot;

				GameObject newGlobal = new GameObject("Global");
				newGlobal.transform.parent = newRoot.transform;
				newGlobal.transform.localPosition = Vector3.zero;
				newGlobal.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);

				umaData.skeleton = new UMASkeleton(newGlobal.transform);

				renderers = new SkinnedMeshRenderer[umaData.generatedMaterials.rendererCount];

				for (int i = 0; i < umaData.generatedMaterials.rendererCount; i++)
				{
					renderers[i] = MakeRenderer(i, newGlobal.transform);
				}
				umaData.SetRenderers(renderers);
			}
			else
			{
				umaData.CleanMesh(false);
				if (umaData.rendererCount != umaData.generatedMaterials.rendererCount)
				{
					var oldRenderers = umaData.GetRenderers();
					var globalTransform = umaData.GetGlobalTransform();

					renderers = new SkinnedMeshRenderer[umaData.generatedMaterials.rendererCount];

					for (int i = 0; i < umaData.generatedMaterials.rendererCount; i++)
					{
						if (oldRenderers != null && oldRenderers.Length > i)
						{
							renderers[i] = oldRenderers[i];
							continue;
						}
						renderers[i] = MakeRenderer(i, globalTransform);
					}

					if (oldRenderers != null)
					{
						for (int i = umaData.generatedMaterials.rendererCount; i < oldRenderers.Length; i++)
						{
							Destroy(oldRenderers[i]);
						}
					}
					umaData.SetRenderers(renderers);
				}
			}
		}

		private SkinnedMeshRenderer MakeRenderer(int i, Transform rootBone)
		{
			GameObject newSMRGO = new GameObject(i == 0 ? "UMARenderer" : ("UMARenderer " + i));
			newSMRGO.transform.parent = umaData.transform;
			newSMRGO.transform.localPosition = Vector3.zero;
			newSMRGO.transform.localRotation = Quaternion.Euler(0, 0, 0f);
			newSMRGO.transform.localScale = Vector3.one;

			var newRenderer = newSMRGO.AddComponent<SkinnedMeshRenderer>();
			newRenderer.enabled = false;
			newRenderer.sharedMesh = new Mesh();
			newRenderer.rootBone = rootBone;
			newRenderer.quality = SkinQuality.Bone4;
			newRenderer.sharedMesh.name = i == 0 ? "UMAMesh" : ("UMAMesh " + i);
			return newRenderer;
		}

		/// <summary>
		/// Updates the UMA mesh and skeleton to match current slots.
		/// </summary>
		/// <param name="updatedAtlas">If set to <c>true</c> atlas has changed.</param>
		/// <param name="umaData">UMA data.</param>
		/// <param name="atlasResolution">Atlas resolution.</param>
		public override void UpdateUMAMesh(bool updatedAtlas, UMAData umaData, int atlasResolution)
        {
            this.umaData = umaData;
            this.atlasResolution = atlasResolution;

            combinedMeshList = new List<SkinnedMeshCombiner.CombineInstance>(umaData.umaRecipe.slotDataList.Length);
            combinedMaterialList = new List<Material>();

			EnsureUMADataSetup(umaData);
			umaData.skeleton.BeginSkeletonUpdate();

			//UMAMeshData umaMesh = new UMAMeshData();
			//umaMesh.ClaimSharedBuffers();

			for (currentRendererIndex = 0; currentRendererIndex < umaData.generatedMaterials.rendererCount; currentRendererIndex++)
			{
				//Move umaMesh creation to with in the renderer loops, or make sure to set all it's buffers to null, see below.
				UMAMeshData umaMesh = new UMAMeshData();
				umaMesh.ClaimSharedBuffers();

				umaMesh.subMeshCount = 0;
				umaMesh.vertexCount = 0;

				/*umaMesh.vertices = null;//added
				umaMesh.boneWeights = null;
				umaMesh.unityBoneWeights = null;
				umaMesh.normals = null;
				umaMesh.tangents = null;
				umaMesh.uv = null;
				umaMesh.uv2 = null;
				umaMesh.uv3 = null;
				umaMesh.uv4 = null;
				umaMesh.colors32 = null;
				umaMesh.clothSkinning = null;*/

				combinedMeshList.Clear();
				combinedMaterialList.Clear();
				clothProperties = null;

				BuildCombineInstances();

				if (combinedMeshList.Count == 1)
				{
					// fast track
					var tempMesh = SkinnedMeshCombiner.ShallowInstanceMesh(combinedMeshList[0].meshData);
					tempMesh.ApplyDataToUnityMesh(renderers[currentRendererIndex], umaData.skeleton);
				}
				else
				{
					SkinnedMeshCombiner.CombineMeshes(umaMesh, combinedMeshList.ToArray());

					if (updatedAtlas)
					{
						RecalculateUV(umaMesh);
					}

					umaMesh.ApplyDataToUnityMesh(renderers[currentRendererIndex], umaData.skeleton);
				}
				var cloth = renderers[currentRendererIndex].GetComponent<Cloth>();
				if (clothProperties != null)
				{
					if (cloth != null)
					{
						clothProperties.ApplyValues(cloth);
					}
				}
				else
				{
					Destroy(cloth);
				}

				var materials = combinedMaterialList.ToArray();
				renderers[currentRendererIndex].sharedMaterials = materials;

				umaMesh.ReleaseSharedBuffers();
			}
			//umaMesh.ReleaseSharedBuffers();

            umaData.umaRecipe.ClearDNAConverters();
            for (int i = 0; i < umaData.umaRecipe.slotDataList.Length; i++)
            {
                SlotData slotData = umaData.umaRecipe.slotDataList[i];
                if (slotData != null)
                {
					umaData.umaRecipe.AddDNAUpdater(slotData.asset.slotDNA);
                }
            }

            umaData.firstBake = false;
        }

        protected void BuildCombineInstances()
        {
            SkinnedMeshCombiner.CombineInstance combineInstance;

            //Since BuildCombineInstances is called within a renderer loop, use a variable to keep track of the materialIndex per renderer
            int rendererMaterialIndex = 0;

            for (int materialIndex = 0; materialIndex < umaData.generatedMaterials.materials.Count; materialIndex++)
            {
				var generatedMaterial = umaData.generatedMaterials.materials[materialIndex];
				if (generatedMaterial.renderer != currentRendererIndex)
					continue;

				combinedMaterialList.Add(generatedMaterial.material);

				for (int materialDefinitionIndex = 0; materialDefinitionIndex < generatedMaterial.materialFragments.Count; materialDefinitionIndex++)
                {
					var materialDefinition = generatedMaterial.materialFragments[materialDefinitionIndex];
					var slotData = materialDefinition.slotData;
                    combineInstance = new SkinnedMeshCombiner.CombineInstance();
					combineInstance.meshData = slotData.asset.meshData;
					combineInstance.targetSubmeshIndices = new int[combineInstance.meshData.subMeshCount];
					for (int i = 0; i < combineInstance.meshData.subMeshCount; i++)
					{
						combineInstance.targetSubmeshIndices[i] = -1;
					}
					combineInstance.targetSubmeshIndices[slotData.asset.subMeshIndex] = rendererMaterialIndex;//materialIndex;
                    combinedMeshList.Add(combineInstance);

					if (slotData.asset.SlotAtlassed != null)
					{
						slotData.asset.SlotAtlassed.Invoke(umaData, slotData, generatedMaterial.material, materialDefinition.atlasRegion);
					}

					if (slotData.asset.material.clothProperties != null)
					{
						clothProperties = slotData.asset.material.clothProperties;
					}
				}

                                rendererMaterialIndex++;
            }
        }

		protected void RecalculateUV(UMAMeshData umaMesh)
        {
            int idx = 0;
            //Handle Atlassed Verts
            for (int materialIndex = 0; materialIndex < umaData.generatedMaterials.materials.Count; materialIndex++)
            {
				var generatedMaterial = umaData.generatedMaterials.materials[materialIndex];
				if (generatedMaterial.renderer != currentRendererIndex)
					continue;

				if (generatedMaterial.umaMaterial.materialType != UMAMaterial.MaterialType.Atlas) continue;

				for (int materialDefinitionIndex = 0; materialDefinitionIndex < generatedMaterial.materialFragments.Count; materialDefinitionIndex++)
                {
					var fragment = generatedMaterial.materialFragments[materialDefinitionIndex];
					var tempAtlasRect = fragment.atlasRegion;
					int vertexCount = fragment.slotData.asset.meshData.vertices.Length;
					float atlasXMin = tempAtlasRect.xMin / atlasResolution;
					float atlasXMax = tempAtlasRect.xMax / atlasResolution;
					float atlasXRange = atlasXMax - atlasXMin;
					float atlasYMin = tempAtlasRect.yMin / atlasResolution;
					float atlasYMax = tempAtlasRect.yMax / atlasResolution;
					float atlasYRange = atlasYMax - atlasYMin;
					while (vertexCount-- > 0)
                    {
						umaMesh.uv[idx].x = atlasXMin + atlasXRange * umaMesh.uv[idx].x;
						umaMesh.uv[idx].y = atlasYMin + atlasYRange * umaMesh.uv[idx].y;
						idx++;
                    }

                }
            }
        }
	}
}