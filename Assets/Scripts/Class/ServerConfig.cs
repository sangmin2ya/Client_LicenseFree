using System;
using System.IO;
using UnityEngine;

// 서버 접속 정보. 코드에 하드코딩하지 않고 StreamingAssets/server_config.json에서 읽는다.
// 실제 파일은 git에 올리지 않고, server_config.example.json을 복사해 값을 채워 사용한다.
[Serializable]
public class ServerConfig
{
	public string ip;
	public string port;
	public string user;
	public string password;

	private const string FileName = "server_config.json";
	private static ServerConfig _current;

	public static ServerConfig Current
	{
		get
		{
			if (_current == null)
			{
				_current = Load();
			}
			return _current;
		}
	}

	public bool IsValid
	{
		get { return !string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(port); }
	}

	private static ServerConfig Load()
	{
		string path = Path.Combine(Application.streamingAssetsPath, FileName);
		if (!File.Exists(path))
		{
			Debug.LogError("서버 설정 파일이 없습니다: " + path + " (server_config.example.json 참고)");
			return new ServerConfig();
		}
		try
		{
			return JsonUtility.FromJson<ServerConfig>(File.ReadAllText(path)) ?? new ServerConfig();
		}
		catch (Exception ex)
		{
			Debug.LogError("서버 설정 파일을 읽지 못했습니다: " + path + "\n" + ex);
			return new ServerConfig();
		}
	}
}
