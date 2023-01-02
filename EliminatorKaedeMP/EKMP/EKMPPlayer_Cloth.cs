using K_PlayerControl;
using UnityEngine;
using UnityEngine.UI;
using static EliminatorKaedeMP.EKMP_UI_cloth_Purchase;

namespace EliminatorKaedeMP
{
	public partial class EKMPPlayer
	{
		/// Replacement/extension methods for UI_ClothSystem and UI_cloth_Purchase

		private void ClothSystem_Initialize(UI_ClothSystem cs)
		{
			UI_ClothSystem ogCs = PlayerPref.instance.GetComponent<UI_ClothSystem>();

			cs.DefaultColor = ogCs.DefaultColor;
			cs.MaterialList = ogCs.MaterialList;
			cs.ShaderParamName = ogCs.ShaderParamName;
			cs.HIYAKE_pat = ogCs.HIYAKE_pat;
			cs.UnderHairMat = ClothSystem_GetMaterial("geomGrp/common/underHair");
			cs.SkinMat = ClothSystem_GetMaterial("geomGrp/common/Game_body_P1");

			cs.hairStyle = new GameObject[ogCs.hairStyle.Length];
			for (int i = 0; i < ogCs.hairStyle.Length; i++)
				cs.hairStyle[i] = FindGameObjectFromLocal(ogCs.hairStyle[i]);

			cs.ini = true;
			ClothSystem_LoadData(cs);
		}

		private void ClothSystem_LoadData(UI_ClothSystem cs)
		{
			cs.clothID = Info.ClothID;
			cs.S_underHair = Info.S_underHair;
			cs.S_underHair_alpha = Info.S_underHair_alpha;
			cs.S_underHair_density = Info.S_underHair_density;
			cs.S_HairStyle = Info.S_HairStyle;
			cs.S_HIYAKE_kosa = Info.S_HIYAKE_kosa;
			cs.S_HIYAKE_patan = Info.S_HIYAKE_patan;

			for (int i = 0; i < 10; i++)
			{
				string[] paramNames = cs.ShaderParamName[i].Split(',');
				for (int j = 0; j < paramNames.Length; j++)
					cs.MaterialList[i].SetColor(paramNames[j], Info.S_MatColor[i]);
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
			{
				PlayerPrefs.SetInt(cs.KEY_HairStyle, input);
				// SendHairstyleChangeData(input); // might need to prevent packet send during init
			}
		}

		private Material ClothSystem_GetMaterial(string path)
		{
			return PlayerCtrl.transform.Find(path).GetComponent<SkinnedMeshRenderer>().material;
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

			ClothPurchase_SelectCloth(cp, Info.ClothID);
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
			Info.ClothID = (byte)inputID;

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
				// SendClothChangeData(inputID); // might need to prevent packet send during init
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
	}
}
