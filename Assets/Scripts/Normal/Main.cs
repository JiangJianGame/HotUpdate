using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JiangJian
{
    public class Main : MonoBehaviour
    {
		private void Start()
		{
			ABMgr.GetInstance().LoadResAsync<GameObject>("ui", "LoadingPanel", (panelObject) =>
			  {
				  GameObject canvasObj = GameObject.Find("Canvas");
				  panelObject.transform.SetParent(canvasObj.transform,false);

				  ABMgr.GetInstance().ClearAB();

				  var panel = panelObject.GetComponent<LoadingPanel>();
				  panel.BeginUpdateFile();
			  });
		}
	}
}