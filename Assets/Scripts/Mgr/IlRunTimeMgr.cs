using ILRuntime.Mono.Cecil.Pdb;
using ILRuntime.Runtime;
using ILRuntime.Runtime.Enviorment;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace JiangJian
{
	//主要用来加载dll文件和pdb文件
	public class IlRunTimeMgr:SingletonAutoMono<IlRunTimeMgr>
	{
		public ILRuntime.Runtime.Enviorment.AppDomain appDomain;

		//dll和pdb文件的内存流
		private MemoryStream dllStream;
		private MemoryStream pdbStream;

		private bool isStarted = false;
		private bool isDebug = false;
		#region 外部访问方法

		/// <summary>
		/// 启动IlRunTime，加载dll和pdb文件
		/// </summary>
		public void StartILRunTime(Action callBack)
		{
			//如果已近开启过了,就直接返回吧，如果没开启就尝试开启
			if(isStarted)
			{
				Debug.LogError("ILRunTime无需重复开启！");
				return;
			}
			else
			{
				isStarted = true;
			}

			//初始化appDoMain
			appDomain = new ILRuntime.Runtime.Enviorment.AppDomain(ILRuntimeJITFlags.JITOnDemand);

			//加载对应的dll和pdb文件
			AssetBundleMgr.Instance.LoadResAsync<TextAsset>("dll_res", "HotFix_Project.dll.txt", (dll) =>
			  {
				  AssetBundleMgr.Instance.LoadResAsync<TextAsset>("dll_res", "HotFix_Project.pdb.txt", (pdb) =>
				  {
					  //初始化pdb和dll文件流对象
					  dllStream = new MemoryStream(dll.bytes);
					  pdbStream = new MemoryStream(pdb.bytes);

					  //将pdb和dll文件流绑定到appDoMain
					  appDomain.LoadAssembly(dllStream, pdbStream, new PdbReaderProvider());

					  //其它初始化操作
					  InitILRunTime();

					  if(isDebug)
					  {
						  StartCoroutine(WaitDebug(callBack));
					  }
					  else
					  {
						  callBack?.Invoke();
					  }
				  });
			  });
		}


		/// <summary>
		/// 关闭ILRunTime，卸载对应文件
		/// </summary>
		public void StopILRunTime()
		{
			if(pdbStream!=null)
			{
				pdbStream.Close();
			}
			if(dllStream!=null)
			{
				dllStream.Close();
			}

			dllStream = null;
			pdbStream = null;
			appDomain = null;
			isStarted = false;
		}
		#endregion

		#region 私有方法
		/// <summary>
		/// 初始化IlRunTime相关信息
		/// </summary>
		private void InitILRunTime()
		{
			if(isDebug)
			{
				//如果想使用Unity自带的新能调试窗口，就需要设置主线程id
				//appDomain.UnityMainThreadID = Thread.CurrentThread.ManagedThreadId;
			}

		}

		/// <summary>
		/// 等待调试程序接入
		/// </summary>
		/// <param name="callBack"></param>
		/// <returns></returns>
		IEnumerator WaitDebug(Action callBack)
		{
			//等待调试程序接入
			while(!appDomain.DebugService.IsDebuggerAttached)
			{
				yield return null;
			}

			//等待一秒后开始执行后面的逻辑，防止错过调试信息
			yield return new WaitForSeconds(1);

			callBack?.Invoke();
		}

		#endregion
	}
}
