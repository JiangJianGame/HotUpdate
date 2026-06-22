using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JiangJian
{
	public class LoadingPanel : MonoBehaviour
	{
		[Header("进度条")] [SerializeField] private Slider slider_Process;
		[Header("进度信息")] [SerializeField] private Text text_ProcessInfo;
		//[Header("打印信息")] [SerializeField] private Text text_Info;
		private void Start()
		{
			slider_Process.value = 0;
			text_ProcessInfo.text = "资源加载中...";
		}

		/// <summary>
		/// 开始更新资源包
		/// </summary>
		public void BeginUpdateFile()
		{
			ABUpdateMgr.Instance.CheckUpdate(ABUpdateOverDoSomething, (info) =>
			 {
				 text_ProcessInfo.text = info;

			 }, (currentValue, maxValue) =>
			  {
				  slider_Process.value = currentValue / maxValue;

			  });
		}

		private void ABUpdateOverDoSomething(bool isOver)
		{
			if (!isOver)
			{
				text_ProcessInfo.text = "资源下载失败，请检查网络连接或联系服务商！";
				return;
			}

			text_ProcessInfo.text = "资源加载结束。";

			//IlRunTime初始化
			IlRunTimeMgr.Instance().StartILRunTime(() =>
			{
				text_ProcessInfo.text = "游戏初始化完毕。";

				IlRunTimeMgr.Instance().appDomain.Invoke("HotFix_Project.ILRunTimeMain", "StartILRunTime", null, null);
			});
		}
	}
}