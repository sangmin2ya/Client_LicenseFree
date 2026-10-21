using System;
using UnityEngine;
using WebSocketSharp;
using Newtonsoft.Json;

public class PingManager : MonoBehaviour
{
    //평균 RTT (SRTT)
    private static long _avgRtt;
    public static long AvgRtt { get { return _avgRtt; } }
    //게임시간
    private static long _gameTime;
    public static long GameTime { get { return _gameTime; } }
    private long _sentTime;
    private long _currentTime;
    private long _serverTime;
    //서버 시계 오프셋 (서버 시각 - 클라이언트 시각)
    private long _avgDelay;
    private float _reqSentPeriod = 0.0f;
    //마지막 PING 응답을 받은 시각 (클라이언트 시계)
    private long _lastReplyAt;
    //PING은 한 번에 하나만 보내 요청과 응답을 짝짓는다
    private bool _awaitingPing = false;
    private const long PingTimeoutMs = 3000;
    //PING이 타임아웃되면 이 시각까지 새 PING을 보내지 않는다. 늦게 도착한 응답이 새 요청과 잘못 짝지어지지 않도록 하는 대기 구간
    private long _quietUntil = 0;
    //RTT, 시계 오프셋 추정 (이상치 제외)
    private readonly RttEstimator _estimator = new RttEstimator();
    public static bool _pingUpdated = false;
    private static readonly String _responseName = responseType.PING.ToString();
    private static PingManager _instance;
    // Start is called before the first frame update
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
    void Start()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.startListenOnMessage(PingUpdated, _responseName);

        _avgRtt = 0;
        _avgDelay = 0;
    }
    void OnDestroy()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.stopListenOnMessage(PingUpdated, _responseName);
    }
    // Update is called once per frame
    void Update()
    {
        if (ModelManager._whileConnected)
        {
            //시간 동기화
            DateTimeOffset now = DateTimeOffset.UtcNow;
            _currentTime = now.ToUnixTimeMilliseconds();
            _gameTime = _currentTime + _avgDelay;

            //1초마다 pingReq보냄
            _reqSentPeriod += Time.deltaTime;
            if (_reqSentPeriod >= 1.0f)
            {
                _reqSentPeriod = 0.0f;
                if (_awaitingPing && _currentTime - _sentTime > PingTimeoutMs)
                {
                    _awaitingPing = false;
                    _quietUntil = _currentTime + PingTimeoutMs;
                }
                if (!_awaitingPing && _currentTime >= _quietUntil)
                {
                    Request();
                }
            }
            if (_pingUpdated)
            {
                CheckConnect();
            }
            //Debug.Log("시간차이" + (_currentTime - _serverTime).ToString());
        }
    }
    private void CheckConnect()
    {
        //마지막 PING 응답 이후 6초 이상 응답이 없을 경우 (같은 클라이언트 시계로 비교)
        if ((_currentTime - _lastReplyAt) > 6000 && ModelManager._checkOnce)
        {
            ModelManager._whileConnected = false;
            Debug.Log("연결끊김");
            _pingUpdated = false;
            //재접속 후 바로 PING을 다시 보낼 수 있도록 초기화
            _awaitingPing = false;
            _quietUntil = 0;
            if (ModelManager._whileGame)
            {
                //Debug.Log("게임중 연결끊김");
                //sendDisconnectRequest();
                ModelManager._isDisconnectedInGame = true;
                ModelManager._checkOnce = false;
            }
            else
            {
                Debug.Log("로비에서 연결끊김");
                //sendDisconnectRequest();
                ModelManager._isDisconnectedInLobby = true;
                ModelManager._checkOnce = false;
            }
        }
    }
    private void PingUpdated(object sender, MessageEventArgs e)
    {
        Debug.Log(e.Data + " / 현재시간 " + _gameTime);
        //메인 스레드 처리 지연을 빼기 위해 소켓 스레드에서 받은 시각 사용
        long receivedAt = NetworkManager.getNetworkingController().CurrentMessageReceivedAt;
        PingResponse pingInfo = PingResponse.CreateFromJSON(e.Data);
        _serverTime = Convert.ToInt64(pingInfo.serverTime);
        _lastReplyAt = receivedAt;
        //타임아웃 후 대기 구간에 온 늦은 응답은 연결 확인에만 쓰고 샘플로는 쓰지 않는다
        if (_awaitingPing)
        {
            //이상치가 아닌 샘플만 RTT, 시계 오프셋에 반영
            if (_estimator.AddSample(_sentTime, receivedAt, _serverTime))
            {
                _avgRtt = (long)Math.Round(_estimator.SmoothedRtt);
                _avgDelay = _estimator.ClockOffset;
            }
            _awaitingPing = false;
        }
        _pingUpdated = true;
    }
    public void Request()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        //보낸시간 저장 (전송 직전)
        _sentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _awaitingPing = true;
        requestPing(networking);
    }
    private void requestPing(NetworkingController networking)
    {

        var request = new PingRequest();
        networking.sendRequest(request);
    }
    public class PingRequest : ClientRequest
    {
        public PingRequest()
        {
            type = requestType.PING.ToString();
        }
    }
    public void sendDisconnectRequest()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        requestTest(networking);
    }
    private void requestTest(NetworkingController networking)
    {
        var request = new TestRequest();
        networking.sendRequest(request);
    }
    public class TestRequest : ClientRequest
    {
        public TestRequest()
        {
            type = requestType.TEST.ToString();
        }
    }
    public class PingResponse
    {
        public string serverTime;
        public static PingResponse CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<PingResponse>(jsonString);
        }
    }
}
