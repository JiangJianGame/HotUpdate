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

		//����ģʽ
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

		//���ڴ���Զ��ab����Ϣ�ֵ䣬֮��ͱ��ؽ��жԱȽ�����ɸ��¡�����
		private Dictionary<string, AbInfo> dic_RemoteAbinfo = new Dictionary<string, AbInfo>();

		//���ڴ���Զ��ab����Ϣ�ֵ䣬֮��ͱ��ؽ��жԱȽ�����ɸ��¡�����
		private Dictionary<string, AbInfo> dic_LocalAbinfo = new Dictionary<string, AbInfo>();


		//�����ص�ab���б��ļ����洢����ab��������
		private List<string> downLoadList = new List<string>();

		/// <summary>
		/// �ȸ������
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
					updateInfo?.Invoke("������Դ�Ա��ļ���ɡ�");

					GetLocalAbCompareFileInfo((isOver) =>
					{
						procressCallBack?.Invoke(2, 5);
						updateInfo?.Invoke("��ȡ������Դ�ļ���");
						//������ȡ����Ҫ���µ���Դ
						foreach (var item in dic_RemoteAbinfo.Keys)
						{
							if(dic_LocalAbinfo.ContainsKey(item))
							{
								//��Ҫ���µ�ab��
								if(dic_LocalAbinfo[item].abMd5!=dic_RemoteAbinfo[item].abMd5)
								{
									downLoadList.Add(item);
								}

								//�Ƴ������õ�ab����ʣ�µľ���Ҫû�õģ���Ҫɾ���ģ�
								dic_LocalAbinfo.Remove(item);
							}
							//������ab��
							else
							{
								downLoadList.Add(item);
							}
						}

						updateInfo?.Invoke("ɾ��û�õ���Դ�ļ���");
						//ɾ��û�õ�
						foreach (var item in dic_LocalAbinfo)
						{
							if (File.Exists(Application.persistentDataPath + "/" + item))
							{
								File.Delete(Application.persistentDataPath + "/" + item);
							}
						}

						procressCallBack?.Invoke(3, 5);
						updateInfo?.Invoke("�������µ���Դ�Ա��ļ���");
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
					updateInfo?.Invoke("������Դ�Ա��ļ�ʧ�ܡ�");
					overCallBack?.Invoke(false);
				}
			});
		}


		/// <summary>
		/// ����AB�Ա��ļ�����ʱ�ļ���
		/// </summary>
		public async void GetRemoteAbCompareFileInfo(Action<bool> overCallBack)
		{
			Debug.Log("��ʼ����AB�ļ���");

			string fileName = "ABCompareInfo_TMP.txt";
			string localPath = Application.persistentDataPath + "/" + fileName;

			bool isOver = false;
			int reDownloadMaxNum = 5;

			while (!isOver && reDownloadMaxNum > 0)
			{
				//����Զ����Դ�Ա��ļ�
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

			//���ص����ص�λ��
			string localPath = Application.persistentDataPath + "/";
			//������ɱ�־
			bool isOver = false;
			//ʧ���������ش���
			int reDownloadMaxNum = 5;
			//���سɹ����������ذ���
			int downLoadOverNum = 0;
			int downLoadMaxNum = downLoadList.Count;
			//���سɹ����б�
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
		/// �����ļ������浽����
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
				Debug.LogError($"������Դ�Ա��ļ�������{e}");
				return false;
			}
		}


		/// <summary>
		/// ��ȡ������Դ�Ա��ļ�
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
				#if UNITY_ANDROID//�ڰ�׿ƽ̨Ĭ�ϻ���ǰ׺�ģ�����Ҫ��ǰ׺
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
		/// ��ȡ������Դ�Ա��ļ���ϢЯ��
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
				Debug.Log($"��ȡ������Դ�Ա��ļ�ʧ��:{request.error}");
				overCallBack?.Invoke(false);
			}
		}

		/// <summary>
		/// ���ı�������������Ϣ��䵽�ֵ���
		/// </summary>
		/// <param name="textInfo"></param>
		/// <param name="dic_ABInfo"></param>
		private void HandleABCompareFileInfo(string textInfo, Dictionary<string, AbInfo> dic_ABInfo)
		{
			//������Դ�Ա�
			string[] fileInfo = textInfo.Split('|');
			string[] infos;
			foreach (var item in fileInfo)
			{
				infos = item.Split(' ');
				dic_ABInfo.Add(infos[0], new AbInfo(infos[0], long.Parse(infos[1]), infos[2]));
			}
		}

		/// <summary>
		/// ab��Ϣ
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
