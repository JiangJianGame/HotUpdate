using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace JiangJian
{
	public class ABTool : EditorWindow
	{
		//当前选择的平台索引
		private int nowSelIndex = 0;
		private string[] targetString = new string[] { "PC", "IOS", "Android" };

		private string userName = "JiangJian";
		private string passWoard = "000000";
		private string serverIP = "ftp://127.0.0.1";
		private string remoteFilePath = "AB/";

		public string GetLocalFilePath => $"{Application.dataPath}/ArtRes/AB/{targetString[nowSelIndex]}/";

		private string GetRemoteFullUrl => $"{serverIP}/{remoteFilePath}/{targetString[nowSelIndex]}/";

		[MenuItem("AB包工具/打开工具窗口")]
		public static void OpenWindow()
		{
			//获取一个ABTool编辑器对象并显示
			ABTool window=GetWindowWithRect(typeof(ABTool), new Rect(0, 0, 310, 240)) as ABTool;
			window.Show();
		}


		private void OnGUI()
		{
			//页签的方式显示平台选择
			GUI.Label(new Rect(10, 10, 150, 15), "平台选择");
			nowSelIndex=GUI.Toolbar(new Rect(10, 30, 270, 20), nowSelIndex, targetString);

			//设置IP地址
			//设置IP地址
			GUI.Label(new Rect(10, 65, 150, 15), "用户名");
			userName = GUI.TextField(new Rect(10, 85, 140, 20), userName);
			GUI.Label(new Rect(160, 65, 140, 15), "密码");
			passWoard= GUI.TextField(new Rect(160, 85, 140, 20), passWoard);
			GUI.Label(new Rect(10, 110, 150, 15), "资源服务器地址");
			serverIP=GUI.TextField(new Rect(10, 130, 140, 20), serverIP);
			GUI.Label(new Rect(160, 110, 140, 15), "远端文件层级(忽略平台)");
			remoteFilePath = GUI.TextField(new Rect(160, 130, 140, 20), remoteFilePath);

			//创建对比文件按钮
			if (GUI.Button(new Rect(10, 165, 140, 30), "创建对比文件"))
			{
				CreateABCompareFile();
			}

			//将选择的文件保存到StreamingAssets
			if (GUI.Button(new Rect(160, 165, 140, 30), "保存选择文件到本地"))
			{
				MoveSelectedFileToStreamingAssets();
			}

			//上传AB包到远端
			if (GUI.Button(new Rect(10, 200, 290, 30), "上传AB包到远端"))
			{
				UpLoadAllABFile();
			}
		}

		/// <summary>
		/// 上传所有Ab文件到远端
		/// </summary>
		private void UpLoadAllABFile()
		{
			//获取所有文件信息
			DirectoryInfo directoryInfo = Directory.CreateDirectory(GetLocalFilePath);
			FileInfo[] fileInfos = directoryInfo.GetFiles();

			//遍历所有文件
			foreach (var info in fileInfos)
			{
				if (info.Extension == ".txt" || info.Extension == "")//匹配对比文件和AB包
				{
					UpLoadFile(info.FullName, info.Name);
				}
			}
		}

		private async void UpLoadFile(string filePath, string fileName)
		{
			// 使用 Task.Run 将耗时操作放入子线程，避免编辑器卡死
			await Task.Run(() =>
			{
				try
				{
					FtpWebRequest ftpWebRequest = FtpWebRequest.Create(new Uri(GetRemoteFullUrl + fileName)) as FtpWebRequest;
					ftpWebRequest.Credentials = new NetworkCredential(userName, passWoard);
					ftpWebRequest.Proxy = null;
					ftpWebRequest.KeepAlive = false;
					ftpWebRequest.Method = WebRequestMethods.Ftp.UploadFile;
					ftpWebRequest.UseBinary = true;

					// 使用 using 确保 FTP 流在异常时也能被安全关闭
					using (Stream stream = ftpWebRequest.GetRequestStream())
					using (FileStream file = File.OpenRead(filePath))
					{
						byte[] bytes = new byte[2048]; // 适当增大缓冲区提高传输效率
						int length = 0;
						while ((length = file.Read(bytes, 0, bytes.Length)) > 0)
						{
							stream.Write(bytes, 0, length);
						}
					}
					Debug.Log($"{fileName} 上传成功。");
				}
				catch (Exception e)
				{
					Debug.LogError($"上传出错: {e.Message}");
				}
			});
		}


		/// <summary>
		/// 创建AB对比文件
		/// </summary>
		private void CreateABCompareFile()
		{
			//获取所有文件信息
			DirectoryInfo directoryInfo = Directory.CreateDirectory(GetLocalFilePath);
			FileInfo[] fileInfos = directoryInfo.GetFiles();

			//要存储的对比文件信息
			string abCompareInfo = "";

			//遍历所有文件
			foreach (var info in fileInfos)
			{
				if (info.Extension == "")//匹配所有没后缀的文件
				{
					abCompareInfo +=$"{info.Name}\" \"{info.Length}\" \"{GetMD5(info.FullName)}\"|";
				}
			}

			if(string.IsNullOrEmpty(abCompareInfo))
			{
				Debug.Log("AB包文件为空！");
				return;
			}
			//移除掉最后面那个下滑杠
			abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);

			//写入文件
			File.WriteAllText(GetLocalFilePath + "ABCompareInfo.txt", abCompareInfo);

			Debug.Log("AB包对比文件生成成功。");

			AssetDatabase.Refresh();
		}

		public string GetMD5(string filePath)
		{
			using (FileStream stream = new FileStream(filePath, FileMode.Open))
			{
				MD5 mD5 = new MD5CryptoServiceProvider();
				byte[] bytes = mD5.ComputeHash(stream);
				stream.Close();

				StringBuilder stringBuilder = new StringBuilder();
				foreach (var item in bytes)
				{
					stringBuilder.Append(item.ToString("x2"));
				}
				return stringBuilder.ToString();
			}
		}

		/// <summary>
		/// 拷贝选择的ab文件到本地
		/// </summary>
		private void MoveSelectedFileToStreamingAssets()
		{
			UnityEngine.Object[] selectedAssets = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.DeepAssets);

			if (selectedAssets.Length <= 0)
				return;

			string abCompareInfo = "";
			foreach (var item in selectedAssets)
			{
				string assetsPath = AssetDatabase.GetAssetPath(item);
				string fileName = assetsPath.Substring(assetsPath.LastIndexOf('/'));

				if (fileName.IndexOf('.') != -1)
					continue;

				AssetDatabase.CopyAsset(assetsPath, $"Assets/StreamingAssets{fileName}");

				FileInfo fileInfo = new FileInfo(Application.streamingAssetsPath + fileName);

				abCompareInfo +=$"{fileInfo.Name}\" \"{fileInfo.Length}\" \"{GetMD5(fileInfo.FullName)}\"|";
			}

			if (string.IsNullOrEmpty(abCompareInfo))
			{
				Debug.Log("选择的AB包文件为空！");
				return;
			}
			abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);

			File.WriteAllText(Application.streamingAssetsPath + "/ABCompareInfo.txt", abCompareInfo);

			AssetDatabase.Refresh();
		}
	}
}
