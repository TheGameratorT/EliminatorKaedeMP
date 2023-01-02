using K_PlayerControl;
using UnityEngine;

namespace EliminatorKaedeMP
{
	public class EKMP_UI_cloth_Purchase : UI_cloth_Purchase
	{
		public class ClothDataType
		{
			public GameObject[] ALL_ClothElements;
			public Texture ForceClip;
		}

		// For custom player instances we use this instead of the base.ClothDatas because
		// that way we can avoid redundant game object instantiations for each cloth data
		public new ClothDataType[] ClothDatas;
	}
}
