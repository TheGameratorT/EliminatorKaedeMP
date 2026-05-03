using K_PlayerControl;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Tango;
using static EliminatorKaedeMP.EKMP_UI_cloth_Purchase;

namespace EliminatorKaedeMP
{
	public partial class EKMPPlayer
	{
		/// Replacement/extension methods for UI_ClothSystem and UI_cloth_Purchase

		private void ClothSystem_Initialize(UI_ClothSystem cs)
		{
			UI_ClothSystem ogCs = PlayerPref.instance.GetComponent<UI_ClothSystem>();

			// New instances of the materials must be created to avoid shared colors
			Material skinMat = Object.Instantiate(ogCs.MaterialList[0]);
			Material eyesMat = Object.Instantiate(ogCs.MaterialList[4]);
			Material underHairMat = Object.Instantiate(ogCs.MaterialList[5]);
			Material hairMat = Object.Instantiate(ogCs.MaterialList[6]);
			Material lambertMat = ogCs.MaterialList[7];
			Material eyeWhiteMat = Object.Instantiate(ogCs.MaterialList[9]);

			// Create a new MaterialList
			cs.MaterialList = new Material[10];
			cs.MaterialList[0] = skinMat;
			cs.MaterialList[1] = skinMat;
			cs.MaterialList[2] = skinMat;
			cs.MaterialList[3] = skinMat;
			cs.MaterialList[4] = eyesMat;
			cs.MaterialList[5] = underHairMat;
			cs.MaterialList[6] = hairMat;
			cs.MaterialList[7] = lambertMat;
			cs.MaterialList[8] = lambertMat;
			cs.MaterialList[9] = eyeWhiteMat;

			cs.ShaderParamName = ogCs.ShaderParamName;
			cs.HIYAKE_pat = ogCs.HIYAKE_pat;
			cs.UnderHairMat = underHairMat;
			cs.SkinMat = skinMat;

			ClothSystem_SetMaterial("geomGrp/common/tooth_above", skinMat);
			ClothSystem_SetMaterial("geomGrp/common/tonge", skinMat);
			ClothSystem_SetMaterial("geomGrp/common/tooth_under", skinMat);
			ClothSystem_SetMaterial("geomGrp/common/Game_body_P1", skinMat);
			ClothSystem_SetMaterial("geomGrp/common/eye", eyesMat);
			ClothSystem_SetMaterial("geomGrp/common/underHair", underHairMat);

			// Setting hair material is a bit complicated
			SkinnedMeshRenderer faceSmr = PlayerCtrl.transform.Find("geomGrp/GamePart/Game_Face").GetComponent<SkinnedMeshRenderer>();
			Material[] faceMaterials = new Material[faceSmr.materials.Length];
			faceMaterials[0] = skinMat;
			faceMaterials[1] = faceSmr.materials[1];
			faceMaterials[2] = eyeWhiteMat;
			faceSmr.materials = faceMaterials;

			// Setting hair material is a bit even more complicated
			cs.hairStyle = new GameObject[ogCs.hairStyle.Length];
			for (int i = 0; i < ogCs.hairStyle.Length; i++)
			{
				cs.hairStyle[i] = FindGameObjectFromLocal(ogCs.hairStyle[i]);
				SkinnedMeshRenderer renderer = cs.hairStyle[i].GetComponentInChildren<SkinnedMeshRenderer>();
				Material[] materials = new Material[renderer.materials.Length];
				for (int j = 0; j < renderer.materials.Length; j++)
				{ 
					Material ogMat = renderer.materials[j];
					materials[j] = ogMat.name.StartsWith("hair_mat") ? hairMat : ogMat;
				}
				renderer.materials = materials;
			}

			cs.ini = true;
			ClothSystem_LoadData(cs);
		}

		private void ClothSystem_LoadData(UI_ClothSystem cs)
		{
			EKMPPlayerClothInfo clothInfo = Info.Cloth;

			cs.clothID = clothInfo.ClothID;
			cs.S_underHair = clothInfo.S_underHair;
			cs.S_underHair_alpha = clothInfo.S_underHair_alpha;
			cs.S_underHair_density = clothInfo.S_underHair_density;
			cs.S_HairStyle = clothInfo.S_HairStyle;
			cs.S_HIYAKE_kosa = clothInfo.S_HIYAKE_kosa;
			cs.S_HIYAKE_patan = clothInfo.S_HIYAKE_patan;

			for (int i = 0; i < 10; i++)
			{
				string[] paramNames = cs.ShaderParamName[i].Split(',');
				for (int j = 0; j < paramNames.Length; j++)
					cs.MaterialList[i].SetColor(paramNames[j], clothInfo.S_MatColor[i]);
			}

			cs.UnderHairMat.SetFloat("_Alpha", cs.S_underHair_alpha);
			cs.UnderHairMat.SetFloat("_density", cs.S_underHair_density);
			cs.SkinMat.SetTexture("_skin_sunburn", cs.HIYAKE_pat[cs.S_HIYAKE_patan]);
			ClothSystem_ChangeHairStyle(cs, cs.S_HairStyle);
		}

		public void ClothSystem_ChangeHairStyle(UI_ClothSystem cs, int input)
		{
			bool isLocalPlayer = PlayerCtrl == GameNet.GetLocalPlayer();

			for (int i = 0; i < cs.hairStyle.Length; i++)
				cs.hairStyle[i].SetActive(i == input);

			if (isLocalPlayer)
				PlayerPrefs.SetInt(cs.KEY_HairStyle, input);
		}

		private void ClothSystem_SetMaterial(string path, Material material)
		{
			PlayerCtrl.transform.Find(path).GetComponent<SkinnedMeshRenderer>().material = material;
		}

		public static PlayerControl ClothSystem_GetPlayer(UI_ClothSystem cs)
		{
			return cs.GetComponent<PlayerPref>().PlayerIncetance?.GetComponent<PlayerControl>();
		}

		// Server + Client
		private void ClothPurchase_Initialize(EKMP_UI_cloth_Purchase cp, int characterID)
		{
			UI_cloth_Purchase ogCp = PlayerPref.instance.PlayerData[characterID].GetComponent<UI_cloth_Purchase>();

			cp.ClothSimElement = null;
			cp.SkinMaterial = ogCp.SkinMaterial;

			// Copy cloth datas from local player
			int clothDataCount = ogCp.ClothDatas.Length;
			cp.ClothDatas = new ClothDataType[clothDataCount];
			for (int i = 0; i < clothDataCount; i++)
			{
				ClothDataType clothData = new ClothDataType();
				cp.ClothDatas[i] = clothData;
				UI_ClothDatas ogClothData = ogCp.ClothDatas[i];

				clothData.ForceClip = ogClothData.ForceClip;
				GameObject[] ogElems = ogClothData.ALL_ClothElements;
				int elemCount = ogElems.Length;
				GameObject[] elems = new GameObject[elemCount];
				clothData.ALL_ClothElements = elems;
				for (int j = 0; j < elemCount; j++)
				{
					GameObject ogElem = ogElems[j];
					if (ogElem != null)
						elems[j] = FindGameObjectFromLocal(ogElem);
				}
			}

			ClothPurchase_SelectCloth(cp, Info.Cloth.ClothID);
		}

		// Server + Client - Replacement for UI_cloth_Purchase.OnClothSelect
		public void ClothPurchase_SelectCloth(UI_cloth_Purchase cp, int inputID)
		{
			bool isLocalPlayer = PlayerCtrl == GameNet.GetLocalPlayer();

			UI_ClothSystem cs = PlayerCtrl.Perf.GetComponent<UI_ClothSystem>();

			if (isLocalPlayer && !PlayerPref.Instance.R18)
			{
				PlayerPref.Instance.WarningMessage();
				return;
			}

			if (isLocalPlayer)
			{
				cp.hideAllCloths();
				cp.ShowCloth(inputID);
				cp.deleteOptionUI();
				cp.CreateClothOptionPanel(inputID);
			}
			else
			{
				ClothPurchase_HideAllCloths((EKMP_UI_cloth_Purchase)cp);
				ClothPurchase_ShowCloth((EKMP_UI_cloth_Purchase)cp, inputID);
			}

			cs.clothID = inputID;
			Info.Cloth.ClothID = (byte)inputID;

			if (isLocalPlayer)
			{
				for (int i = 0; i < cp.ClothDatas[inputID].OptionsElemnts.Length; i++)
				{
					if (cp.ClothDatas[inputID].UI_Elements[i] == UI_ClothDatas.Element.Toggle ||
						cp.ClothDatas[inputID].UI_Elements[i] == UI_ClothDatas.Element.Texture)
					{
						cp.OnOptionChange(inputID, i, SaveData.GetInt(cp.KEY_ClothOption_ID[inputID, i], 0));
					}
					else if (cp.ClothDatas[inputID].UI_Elements[i] == UI_ClothDatas.Element.Slider)
					{
						float[] array = cp.ConvertSaveData_to_Slider(SaveData.GetInt(cp.KEY_ClothOption_ID[inputID, i], 0));
						cp.OnOptionSliderChange(inputID, i, Mathf.FloorToInt(array[0]), cp.UI_Elements[i].GetComponent<Slider>());
					}
				}

				SaveData.SetInt(cp.KEY_Cloth_ID, inputID);
				SaveData.Save();
			}
		}

		private void ClothPurchase_HideAllCloths(EKMP_UI_cloth_Purchase cp)
		{
			if (cp.ClothSimElement != null)
				Object.Destroy(cp.ClothSimElement);

			for (int i = 0; i < cp.ClothDatas.Length; i++)
			{
				ClothDataType clothData = cp.ClothDatas[i];
				if (clothData == null)
					continue;

				for (int j = 0; j < clothData.ALL_ClothElements.Length; j++)
				{
					GameObject clothElement = clothData.ALL_ClothElements[j];
					clothElement?.SetActive(false);
				}
			}
		}

		private void ClothPurchase_ShowCloth(EKMP_UI_cloth_Purchase cp, int InputID)
		{
			ClothDataType clothData = cp.ClothDatas[InputID];
			for (int i = 0; i < clothData.ALL_ClothElements.Length; i++)
			{
				GameObject clothElement = clothData.ALL_ClothElements[i];
				if (clothElement != null)
				{
					if (clothElement.GetComponent<Cloth>() != null)
					{
						cp.ClothSimElement = Object.Instantiate(clothElement);
						cp.ClothSimElement.name = "ClothSimGeom";
						cp.ClothSimElement.transform.SetParent(clothElement.transform.parent.transform);
						cp.ClothSimElement.SetActive(true);
						clothElement.SetActive(false);
					}
					else
					{
						clothElement.SetActive(true);
					}
				}
			}

			if (clothData.ForceClip != null)
				cp.SkinMaterial.SetTexture("_Cutout", clothData.ForceClip);
		}

		public static PlayerControl ClothPurchase_GetPlayer(UI_cloth_Purchase cp)
		{
			return cp.gameObject.transform.parent.GetComponent<PlayerPref>().PlayerIncetance?.GetComponent<PlayerControl>();
		}

		// Server + Client - Runs when cloth data is received
		public void OnClothInfoData(EKMPPlayerClothInfo clothInfo)
		{
			bool changedClothID = Info.Cloth.ClothID != clothInfo.ClothID;
			Info.Cloth = clothInfo;

			ClothSystem_LoadData(PlayerCtrl.Perf.GetComponent<UI_ClothSystem>());
			if (changedClothID)
			{
				UI_cloth_Purchase cp = PlayerCtrl.Perf.PlayerData[Info.CharacterID].GetComponent<UI_cloth_Purchase>();
				ClothPurchase_SelectCloth(cp, clothInfo.ClothID);
			}
		}

		// Server - Sends the cloth data of a player to the other clients
		private void BroadcastClothInfoData(EKMPPlayerClothInfo clothInfo)
		{
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((int)S2CPacketID.PlayerClothInfo);
					writer.Write(Info.ID);
					clothInfo.Write(writer);
				}
				bytes = stream.ToArray();
			}
			BroadcastPacket(bytes);
		}

		// Server + Client - Sends the cloth data to the server (only the local player should run this)
		public void SendClothInfoData()
		{
			if (GameNet.IsServer)
			{
				// If we are a server, there is not point in sending the data to ourselves, just send it to the other clients
				BroadcastClothInfoData(Info.Cloth);
			}
			else
			{
				// But if we are a client, we must send it to the server so that it will broadcast it to other clients
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = new BinaryWriter(stream))
					{
						writer.Write((int)C2SPacketID.PlayerClothInfo);
						Info.Cloth.Write(writer);
					}
					bytes = stream.ToArray();
				}
				Client.SendPacket(bytes);
			}
		}
	}
}
