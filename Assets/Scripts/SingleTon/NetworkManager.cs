using UnityEngine;

// 수신 메시지 핸들러가 다른 컴포넌트의 Update보다 먼저 실행되도록 앞 순서로 실행
[DefaultExecutionOrder(-100)]
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
	// 소켓 스레드에서 받은 메시지를 메인 스레드에서 처리
	void Update()
	{
		if (_instance != this || _networking == null)
		{
			return;
		}
		_networking.DispatchPendingMessages();
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