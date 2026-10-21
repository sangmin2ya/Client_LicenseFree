using UnityEngine;

public class NetworkManager : MonoBehaviour
{
	private static NetworkManager _instance;
	private NetworkingController _networking;

	// Create our singleton
	void Awake()
	{
		if (_instance != null)
		{
			Destroy(gameObject);
		}
		else
		{
			_instance = this;
			DontDestroyOnLoad(this.gameObject);
		}
	}
	//네트워크 컨트롤러 생성
	public static NetworkingController getNetworkingController()
	{
		if (_instance._networking == null)
		{
			_instance._networking = new NetworkingController();
		}
		return _instance._networking;
	}

	public static int StartConnectionWithIP(string ip)
    {
        ServerConfig config = ServerConfig.Current;
        if (!config.IsValid)
        {
            return -1;
        }
        return _instance._networking.connect(ip, config.port, config.user, config.password);
    }
}