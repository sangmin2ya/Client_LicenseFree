using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using TMPro;
using WebSocketSharp;

public class EntryController : MonoBehaviour
{
    [Tooltip("Lobby Camera")]

    [SerializeField] private GameObject _lobbyCamera;
    [SerializeField] private GameObject _roomJoinCamera;
    [SerializeField] private GameObject _cop;


    public GameObject _ipCanvas;
    public GameObject _lobbyCanvas;
    public GameObject _userBlockBG;
    public GameObject _loginPopImage;
    public GameObject _roomListUI;
    public GameObject _chooseRoleUI;
    public GameObject _lobbyBG;

    public TextMeshProUGUI _isConnectedMessage;
    public Button _gameStartButton;

    public string _textMessage;
    public int _roomId;

    private CheckUserController _check;
    private RoomListController _roomList;

    private bool _tryEntry = false;
    private bool _isFailed = false;
    public static bool _chooseRole = false;

    private static readonly String _responseName = responseType.CREATE_ROOM.ToString();

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.startListenOnMessage(CreateRoomUpdated, _responseName);

        _check = FindObjectOfType<CheckUserController>();
        _roomList = FindObjectOfType<RoomListController>();

        _ipCanvas.gameObject.SetActive(true);
        _lobbyCanvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_chooseRole)
        {
            _lobbyCamera.SetActive(false);
            _roomJoinCamera.SetActive(false);
            _cop.GetComponent<Animator>().SetBool("PickPolice", false);
            _ipCanvas.gameObject.SetActive(false);
            _cop.GetComponent<Animator>().SetTrigger("SwitchLobby");
            _lobbyCanvas.gameObject.SetActive(true);
            _lobbyBG.gameObject.SetActive(true);
            _chooseRoleUI.gameObject.SetActive(true);
            _roomListUI.gameObject.SetActive(false);
            _userBlockBG.gameObject.SetActive(false);
            _loginPopImage.gameObject.SetActive(false);
            _chooseRole = false;
        }
        if (_tryEntry)
        {
            _lobbyBG.gameObject.SetActive(false);
            _chooseRoleUI.gameObject.SetActive(false);
            _roomListUI.gameObject.SetActive(true);
            _tryEntry = false;
        }
        if (_isFailed)
        {
            ModelManager._whileConnected = false;
            _isConnectedMessage.text = _textMessage;
            _isFailed = false;
        }
    }

    public void OnGameStartButtonClicked()
    {
        string _ip = ServerConfig.Current.ip;
        if (NetworkManager.StartConnectionWithIP(_ip) == 1)
        {
            _textMessage = "싱가포르 서버에 연결되었습니다";
            _chooseRole = true;
            ModelManager._whileConnected = true;
            ModelManager._ip = _ip;
        }
        else
        {
            _textMessage = "서버 연결에 실패하였습니다";
            _isFailed = true;
        }
    }

    public void OnBackButtonClicked()
    {
        _chooseRole = true;
    }

    public void OnCopButtonClicked()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        requestCreateRoom(networking);
    }

    public void OnCriminalButtonClicked()
    {
        _roomList.Request();
        _tryEntry = true;
    }

    private void requestCreateRoom(NetworkingController networking)
    {
        var request = new CreateRoomRequest();
        networking.sendRequest(request);
    }

    private void CreateRoomUpdated(object sender, MessageEventArgs e)
    {
        CreateRoomResponse room = CreateRoomResponse.CreateFromJSON(e.Data);
        Debug.Log(room.roomId);
        _roomId = room.roomId;
        _check.StartLogin(_roomId, "NONE", false);
    }

    public class CreateRoomRequest : ClientRequest
    {
        public CreateRoomRequest()
        {
            type = requestType.CREATE_ROOM.ToString();
        }
    }

    public class CreateRoomResponse
    {
        public int roomId;
        public static CreateRoomResponse CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<CreateRoomResponse>(jsonString);
        }
    }
}