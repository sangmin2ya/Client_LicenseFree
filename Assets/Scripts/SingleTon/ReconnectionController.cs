using System;
using System.Collections;
using System.Collections.Generic;
using WebSocketSharp;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public class ReconnectionController : MonoBehaviour
{
    [SerializeField] private GameObject _reconnecting;
    private static ReconnectionController _instance;
    public static rVector3[] _recentSmoke;
    public static ReportInfo[] _recentReportForCop;
    public static ReportInfo[] _recentReportForCriminal;
    private GameObject _reconnectingCanvas;
    private bool _connectOnce = true;
    private bool _endOnce = true;
    private bool _needToLoadScene = false;
    private static readonly string _reconnectResponseName = responseType.RECONNECT.ToString();
    private const float MaxRetryDelay = 8f;
    private int _retryCount = 0;
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
        networking.startListenOnMessage(ReconnectUpdated, _reconnectResponseName);
    }
    void OnDestroy()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.stopListenOnMessage(ReconnectUpdated, _reconnectResponseName);
    }
    // Update is called once per frame
    void Update()
    {
        Disconnect();
        Reconnect();
    }
    //Test용 소켓연결해제 요청
    public void Disconnect()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SendDisconnectRequest();
        }
    }
    public void Reconnect()
    {
        //게임 내부에서 끊길경우
        if (ModelManager._isDisconnectedInGame)
        {
            Debug.Log("게임중 연결끊김");
            //모든 작동 정지
            ModelManager._isDisconnected = true;
            //재접속중 화면 on
            ShowReconnectingCanvas();
            //소켓에는 한번만 연결
            if (_connectOnce)
            {
                if (!TryConnectOrRetry(() => ModelManager._isDisconnectedInGame = true))
                {
                    ModelManager._isDisconnectedInGame = false;
                    return;
                }
                _connectOnce = false;
            }
            SendRequest();
            ModelManager._isDisconnectedInGame = false;

        }
        //게임이 아예 종료될경우
        if (ModelManager._isDisconnectedCloseGame)
        {
            ModelManager._isDisconnected = true;
            ShowReconnectingCanvas();
            SendRequest();
            ModelManager._isDisconnectedCloseGame = false;
            _needToLoadScene = true;
        }
        //로비에서 연결이 끊길경우
        if (ModelManager._isDisconnectedInLobby)
        {
            //방입장 안했을때
            if (ModelManager._roomId == 0)
            {
                //모든 작동 정지
                ModelManager._isDisconnected = true;
                //재접속중 화면 on
                ShowReconnectingCanvas();
                //소켓에는 한번만 연결
                if (_connectOnce)
                {
                    if (!TryConnectOrRetry(() => ModelManager._isDisconnectedInLobby = true))
                    {
                        ModelManager._isDisconnectedInLobby = false;
                        return;
                    }
                    _connectOnce = false;
                }
                ModelManager._isDisconnectedInLobby = false;
                StartCoroutine("EndRecconect");
            }
            //방입장 했을때
            else
            {
                ModelManager._isDisconnected = true;
                ShowReconnectingCanvas();
                if (_connectOnce)
                {
                    if (!TryConnectOrRetry(() => ModelManager._isDisconnectedInLobby = true))
                    {
                        ModelManager._isDisconnectedInLobby = false;
                        return;
                    }
                    SendRequest();
                    _connectOnce = false;
                }
                ModelManager._isDisconnectedInLobby = false;
                StartCoroutine("EndRecconect");
            }
        }
        //재접속 패킷 데이터 도착후 재접속 시작
        if (ModelManager._isReconnected && _endOnce)
        {
            //리커넥션 완료후 과정 종료 (약 1~2초)
            StartCoroutine("EndRecconect");
            _endOnce = false;
            //종료된 경우에만 로드씬
            if (_needToLoadScene)
            {
                SceneManager.LoadScene("Game");
                _needToLoadScene = false;
            }
            ModelManager._whileGame = true;
        }
    }
    //소켓 재연결
    //이미 연결된 소켓이면 0을 반환하므로 성공으로 본다
    public bool ConnectServer()
    {
        return NetworkManager.StartConnectionWithIP(ModelManager._ip) >= 0;
    }
    //연결에 실패하면 재접속 화면을 유지한 채 1, 2, 4, 8초 간격으로 같은 끊김 처리를 다시 시도
    private bool TryConnectOrRetry(Action raiseDisconnectAgain)
    {
        if (ConnectServer())
        {
            _retryCount = 0;
            return true;
        }
        float delay = Mathf.Min(MaxRetryDelay, Mathf.Pow(2f, _retryCount));
        _retryCount++;
        Debug.LogWarning("재접속 실패, " + delay + "초 후 다시 시도");
        StartCoroutine(RetryAfter(delay, raiseDisconnectAgain));
        return false;
    }
    //재접속중 화면 on (재시도 대기 중에도 유지)
    private void ShowReconnectingCanvas()
    {
        if (_reconnectingCanvas == null)
        {
            _reconnectingCanvas = Instantiate(_reconnecting);
        }
    }
    IEnumerator RetryAfter(float delay, Action raiseDisconnectAgain)
    {
        yield return new WaitForSeconds(delay);
        raiseDisconnectAgain();
    }
    public void ReplaceUser()
    {

    }
    IEnumerator EndRecconect()
    {
        yield return new WaitForSeconds(1.0f);
        ModelManager._isReconnected = false;
        ModelManager._isDisconnected = false;
        //재접속중 화면 off
        Destroy(_reconnectingCanvas);
        ModelManager._checkOnce = true;
        ModelManager._whileConnected = true;
        _connectOnce = true;
        _endOnce = true;
    }
    public void SendDisconnectRequest()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        RequestDisconnect(networking);
    }
    private void RequestDisconnect(NetworkingController networking)
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
    private void ReconnectUpdated(object sender, MessageEventArgs e)
    {
        Debug.Log(e.Data);
        ReconnectResponse reconnectInfo = ReconnectResponse.CreateFromJSON(e.Data);
        if (reconnectInfo.reconnectInfo.playRoomDto != null)
        {
            //ModelMaganer 값 다시 저장
            ModelManager._endTime = Convert.ToInt64(reconnectInfo.reconnectInfo.playRoomDto.roomEndAt);
            ModelManager._leftCriminal = reconnectInfo.reconnectInfo.playRoomDto.remainCrimeCount;
            ModelManager._missionPhase = reconnectInfo.reconnectInfo.currentMissionPhase;
            ModelManager._missionLocation = reconnectInfo.reconnectInfo.missionInfo;
            ModelManager._npcCount = 0;
            if (reconnectInfo.reconnectInfo.targetInfo != null)
                ModelManager._targetID = reconnectInfo.reconnectInfo.targetInfo.ToList();
            //한번 초기화해주기
            ModelManager._users.Clear();
            ModelManager._npcPosition.Clear();
            ModelManager._npcRotation.Clear();
            ModelManager._npcColor.Clear();

            foreach (RoomUser roomUser in reconnectInfo.reconnectInfo.playRoomDto.roomUsers)
            {
                ModelManager._users.Add(roomUser.username, roomUser);
                if (roomUser.roomUserType == roomUserType.NPC.ToString())
                {
                    ModelManager._npcPosition.Add(roomUser.pos);
                    ModelManager._npcRotation.Add(roomUser.rot);
                    ModelManager._npcColor.Add(roomUser.color);
                    ModelManager._npcCount++;
                    Debug.Log("NPC : " + ModelManager._npcCount);
                }
                else if (roomUser.roomUserType == roomUserType.COP.ToString())
                {
                    ModelManager._copPosition = roomUser.pos;
                    ModelManager._copRotation = roomUser.rot;
                    ModelManager._copNickname = roomUser.nickname;
                }
                else if (roomUser.roomUserType == roomUserType.USER.ToString())
                {
                    if (roomUser.crimeType == crimeType.SPY.ToString())
                    {
                        ModelManager._spyPosition = roomUser.pos;
                        ModelManager._spyRotation = roomUser.rot;
                        ModelManager._spyColor = roomUser.color;
                        ModelManager._spyNickname = roomUser.nickname;
                    }
                    if (roomUser.crimeType == crimeType.BOOMER.ToString())
                    {
                        ModelManager._boomerPosition = roomUser.pos;
                        ModelManager._boomerRotation = roomUser.rot;
                        ModelManager._boomerColor = roomUser.color;
                        ModelManager._boomerNickname = roomUser.nickname;
                    }
                    if (roomUser.crimeType == crimeType.ASSASSIN.ToString())
                    {
                        ModelManager._assassinPosition = roomUser.pos;
                        ModelManager._assassinRotation = roomUser.rot;
                        ModelManager._assassinColor = roomUser.color;
                        ModelManager._assasssinNickname = roomUser.nickname;
                    }
                }
                //암살타겟 죽었으면 삭제
                if (ModelManager._targetID.Contains(roomUser.username))
                {
                    ModelManager._users[roomUser.username].nickname = "암살타겟";
                    if (roomUser.userState == userState.DIE.ToString())
                    {
                        ModelManager._targetID.Remove(roomUser.username);
                    }
                }
            }
            ModelManager._nickname = reconnectInfo.reconnectUser.nickname;
            ModelManager._userColor = reconnectInfo.reconnectUser.color;
            ModelManager._userType = reconnectInfo.reconnectUser.roomUserType;
            ModelManager._crimeType = reconnectInfo.reconnectUser.crimeType;


            //연막 사용위치 설정
            _recentSmoke = reconnectInfo.reconnectInfo.playRoomDto.recentItemUseInfo;
            //최근 신고정보 저장
            _recentReportForCop = reconnectInfo.reconnectInfo.playRoomDto.recentReportInfo;
            _recentReportForCriminal = reconnectInfo.reconnectInfo.playRoomDto.recentReportInfo;
            //경찰 범죄자 구분
            if (reconnectInfo.reconnectUser.roomUserType == roomUserType.USER.ToString())
            {
                ModelManager._itemValid = reconnectInfo.reconnectInfo.remainItemCount;
                //플레이어 상태 업데이트
                if (reconnectInfo.reconnectUser.userState == userState.STUN.ToString())
                {
                    ModelManager._isStunned = true;
                    ModelManager._hasJustStunned = true;
                }
                if (reconnectInfo.reconnectUser.userState == userState.NORMAL.ToString())
                {
                    ModelManager._isStunned = false;
                }
                if (reconnectInfo.reconnectUser.userState == userState.DIE.ToString())
                {
                    ModelManager._isDead = true;
                }
            }
            if (reconnectInfo.reconnectUser.roomUserType == roomUserType.COP.ToString())
            {
                //검문은 바로 해제
                ModelManager._whileStun = false;
            }
            //이후 각 컨트롤러들이 받아온 데이터를 이용해 오브젝트 재설정
            ModelManager._isReconnected = true;
        }

        else if (reconnectInfo.reconnectInfo.LobbyRoomDto != null)
        {
            ModelManager._roomId = reconnectInfo.reconnectInfo.LobbyRoomDto.roomId;

            ModelManager._npcCount = reconnectInfo.reconnectInfo.LobbyRoomDto.roomNpcs.Length;
            //방유저 목록 업데이트 
            ModelManager._roomUserList = reconnectInfo.reconnectInfo.LobbyRoomDto.roomUsers;
            ModelManager._updateUserList = true;
        }
    }
    public class ReconnectResponse
    {
        public RoomUser reconnectUser;
        public ReconnectInfo reconnectInfo;
        public static ReconnectResponse CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<ReconnectResponse>(jsonString);
        }
    }
    public void SendRequest()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        RequestReconnect(networking);
    }
    private void RequestReconnect(NetworkingController networking)
    {
        var request = new ReconnectRequest();
        networking.sendRequest(request);
    }
    public class ReconnectRequest : ClientRequest
    {
        public string username;
        public int roomId;
        public ReconnectRequest()
        {
            type = requestType.RECONNECT.ToString();
            username = ModelManager._username;
            roomId = ModelManager._roomId;
        }
    }
    public class AddUserRequest : ClientRequest
    {
        public string username;
        public string nickname;
        public int roomId;
        public AddUserRequest()
        {
            type = requestType.ADD_USER.ToString();
            this.username = ModelManager._username;
            this.nickname = ModelManager._nickname;
            this.roomId = ModelManager._roomId;
        }
    }
}