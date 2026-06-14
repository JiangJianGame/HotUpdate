using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace JiangJian
{

	public class ABUpdateMgr : MonoBehaviour
	{
		public string baseUrl = "ftp://192.168.100.4/";
		public string endUrl = "AB/";
		public string platform =
		#if UNITY_IOS
			"IOS/";
		#elif UNITY_ANDROID
			"Android/";
		#else
			"PC/";
		#endif
		private string GetFullUrl => baseUrl + endUrl+platform;

		//单例模式
		private static ABUpdateMgr instance;
		public static ABUpdateMgr Instance
		{
			get
			{
				if (instance == null)
				{
					GameObject obj = new GameObject("ABUpdateMgr");
					instance = obj.AddComponent<ABUpdateMgr>();
				}
				return instance;
			}
		}
		private void OnDestroy()
		{
			instance = null;
		}

		//用于储存远端ab包信息字典，之后和本地进行对比进而完成更新、下载
		private Dictionary<string, AbInfo> dic_RemoteAbinfo = new Dictionary<string, AbInfo>();

		//用于储存远端ab包信息字典，之后和本地进行对比进而完成更新、下载
		private Dictionary<string, AbInfo> dic_LocalAbinfo = new Dictionary<string, AbInfo>();


		//待下载的ab包列表文件，存储的是ab包的名字
		private List<string> downLoadList = new List<string>();

		/// <summary>
		/// 热更主入口
		/// </summary>
		/// <param name="overCallBack"></param>
		/// <param name="updateInfo"></param>
		public void CheckUpdate(Action<bool> overCallBack, Action<string> updateInfo,Action<float,float>procressCallBack)
		{
			dic_LocalAbinfo.Clear();
			dic_RemoteAbinfo.Clear();
			downLoadList.Clear();

			GetRemoteAbCompareFileInfo((isOver) =>
			{
				if (isOver)
				{
					procressCallBack?.Invoke(1, 5);
					updateInfo?.Invoke("下载资源对比文件完成。");

					GetLocalAbCompareFileInfo((isOver) =>
					{
						procressCallBack?.Invoke(2, 5);
						updateInfo?.Invoke("获取更新资源文件。");
						//遍历获取所有要更新的资源
						foreach (var item in dic_RemoteAbinfo.Keys)
						{
							if(dic_LocalAbinfo.ContainsKey(item))
							{
								//需要更新的ab包
								if(dic_LocalAbinfo[item].abMd5!=dic_RemoteAbinfo[item].abMd5)
								{
									downLoadList.Add(item);
								}

								//移除掉有用的ab包，剩下的就是要没用的（需要删除的）
								dic_LocalAbinfo.Remove(item);
							}
							//新增的ab包
							else
							{
								downLoadList.Add(item);
							}
						}

						updateInfo?.Invoke("删除没用的资源文件。");
						//删除没用的
						foreach (var item in dic_LocalAbinfo)
						{
							if (File.Exists(Application.persistentDataPath + "/" + item))
							{
								File.Delete(Application.persistentDataPath + "/" + item);
							}
						}

						procressCallBack?.Invoke(3, 5);
						updateInfo?.Invoke("保存最新的资源对比文件。");
						UpdateABFile((isOver) =>
						{
							if (isOver)
							{
								procressCallBack?.Invoke(4, 5);
								DownLoadFile("ABCompareInfo.txt", Application.persistentDataPath + "ABCompareInfo.txt");
								procressCallBack?.Invoke(5, 5);
							}
							overCallBack?.Invoke(isOver);
						}, updateInfo);
					});
				}
				else
				{
					updateInfo?.Invoke("下载资源对比文件失败。");
					overCallBack?.Invoke(false);
				}
			});
		}


		/// <summary>
		/// 更新AB对比文件到临时文件中
		/// </summary>
		public async void GetRemoteAbCompareFileInfo(Action<bool> overCallBack)
		{
			Debug.Log("开始更新AB文件。");

			string fileName = "ABCompareInfo_TMP.txt";
			string localPath = Application.persistentDataPath + "/" + fileName;

			bool isOver = false;
			int reDownloadMaxNum = 5;

			while (!isOver && reDownloadMaxNum > 0)
			{
				//下载远程资源对比文件
				await Task.Run(() => isOver = DownLoadFile(fileName, localPath));

				reDownloadMaxNum--;
			}

			if (isOver)
			{
				HandleABCompareFileInfo(File.ReadAllText(localPath), dic_RemoteAbinfo);
			}

			overCallBack?.Invoke(isOver);
		}


		public async void UpdateABFile(Action<bool> callBack, Action<string> downLoadProcress)
		{

			//下载到本地的位置
			string localPath = Application.persistentDataPath + "/";
			//下载完成标志
			bool isOver = false;
			//失败重新下载次数
			int reDownloadMaxNum = 5;
			//下载成功、所需下载包数
			int downLoadOverNum = 0;
			int downLoadMaxNum = downLoadList.Count;
			//下载成功的列表
			List<string> tempList = new List<string>();
			while (downLoadList.Count > 0 && reDownloadMaxNum > 0)
			{
				for (int i = 0; i < downLoadList.Count; i++)
				{
					isOver = false;
					await Task.Run(() =>
					{
						isOver = DownLoadFile(downLoadList[i], localPath + downLoadList[i]);
					});

					if (isOver)
					{
						tempList.Add(downLoadList[i]);
						downLoadProcress?.Invoke($"{++downLoadOverNum}/{downLoadMaxNum}");
					}
				}

				foreach (var item in tempList)
				{
					downLoadList.Remove(item);
				}

				tempList.Clear();
				reDownloadMaxNum--;
			}

			callBack?.Invoke(isOver);
		}

		/// <summary>
		/// 下载文件并保存到本地
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="loaclPath"></param>
		private bool DownLoadFile(string fileName, string loaclPath)
		{
			try
			{
				FtpWebRequest ftpWebRequest = FtpWebRequest.Create(new Uri(GetFullUrl + fileName)) as FtpWebRequest;

				ftpWebRequest.Credentials = new NetworkCredential("JiangJian", "000000");

				ftpWebRequest.Proxy = null;

				ftpWebRequest.KeepAlive = false;

				ftpWebRequest.Method = WebRequestMethods.Ftp.DownloadFile;

				ftpWebRequest.UseBinary = true;

				FtpWebResponse ftpWebResponse = ftpWebRequest.GetResponse() as FtpWebResponse;

				Stream downloadStream = ftpWebResponse.GetResponseStream();

				using (FileStream fileStream = File.Create(loaclPath))
				{
					byte[] bytes = new byte[1024];
					int contentLength = downloadStream.Read(bytes, 0, bytes.Length);
					while (contentLength != 0)
					{
						fileStream.Write(bytes, 0, contentLength);
						contentLength = downloadStream.Read(bytes, 0, bytes.Length);
					}

					fileStream.Close();
					downloadStream.Close();
				}

				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"下载资源对比文件报错：{e}");
				return false;
			}
		}


		/// <summary>
		/// 获取本地资源对比文件
		/// </summary>
		private void GetLocalAbCompareFileInfo(Action<bool> overCallBack)
		{
			string filePath = "";
			if (File.Exists(Application.persistentDataPath + "/ABCompareInfo.txt"))
			{
				filePath = "file:///"+ Application.persistentDataPath + "/ABCompareInfo.txt";
			}
			else if (File.Exists(Application.streamingAssetsPath + "/ABCompareInfo.txt"))
			{
				filePath =
				#if UNITY_ANDROID//在安卓平台默认会有前缀的，不需要加前缀
					""
				#else
					"file:///"
				#endif
					+ Application.streamingAssetsPath + "/ABCompareInfo.txt";
			}
			else
			{
				overCallBack?.Invoke(true);
			}

			if (!string.IsNullOrEmpty(filePath))
			{
				StartCoroutine(GetLocalAbCompareFileInfo(filePath, overCallBack));
			}
		}

		/// <summary>
		/// 获取本地资源对比文件信息携程
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		private IEnumerator GetLocalAbCompareFileInfo(string filePath, Action<bool> overCallBack)
		{
			UnityWebRequest request = UnityWebRequest.Get(filePath);

			yield return request.SendWebRequest();

			if (request.result == UnityWebRequest.Result.Success)
			{
				HandleABCompareFileInfo(request.downloadHandler.text, dic_LocalAbinfo);

				overCallBack?.Invoke(true);
			}
			else
			{
				Debug.Log($"获取本地资源对比文件失败:{request.error}");
				overCallBack?.Invoke(false);
			}
		}

		/// <summary>
		/// 将文本解析处理后将信息填充到字典中
		/// </summary>
		/// <param name="textInfo"></param>
		/// <param name="dic_ABInfo"></param>
		private void HandleABCompareFileInfo(string textInfo, Dictionary<string, AbInfo> dic_ABInfo)
		{
			//进行资源对比
			string[] fileInfo = textInfo.Split('|');
			string[] infos;
			foreach (var item in fileInfo)
			{
				infos = item.Split(' ');
				dic_ABInfo.Add(infos[0], new AbInfo(infos[0], long.Parse(infos[1]), infos[2]));
			}
		}

		/// <summary>
		/// ab信息
		/// </summary>
		private class AbInfo
		{
			public string abName;
			public long abSize;
			public string abMd5;

			public AbInfo(string abName, long abSize, string abMd5)
			{
				this.abName = abName;
				this.abSize = abSize;
				this.abMd5 = abMd5;
			}
			public AbInfo() { }
		}
	}
}
