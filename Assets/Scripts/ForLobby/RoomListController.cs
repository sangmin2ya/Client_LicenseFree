using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using TMPro;
using WebSocketSharp;

public class RoomListController : MonoBehaviour
{
    public GameObject _roomPrefab;
    public TextMeshProUGUI _warnText;
    public Transform _roomsContainer;

    private bool _flag = true;
    private bool _updateRoomListFlag = false;
    private bool _warn = false;

    private RoomListResponse _cachedRoomListResponse;
    private CheckUserController _check;
    private static readonly String _responseName = responseType.ROOM_LIST.ToString();

    // Start is called before the first frame update
    void OnDestroy()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.stopListenOnMessage(RoomListUpdated, _responseName);
    }
    void Start()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
		networking.startListenOnMessage(RoomListUpdated, _responseName);
        _check = FindObjectOfType<CheckUserController>();
        _warnText.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(_updateRoomListFlag)
        {
            if(_cachedRoomListResponse != null)
            {
                foreach (Transform child in _roomsContainer)
                {
                    Destroy(child.gameObject);
                }

                float yOffset = -90f;
                float totalHeight = Mathf.Abs(yOffset) * _cachedRoomListResponse.rooms.Length;
                _roomsContainer.GetComponent<RectTransform>().sizeDelta = 
                    new Vector2(_roomsContainer.GetComponent<RectTransform>().sizeDelta.x, totalHeight);

                Debug.Log(_cachedRoomListResponse.rooms.Length);
                int endRoomCount = 0;

                for (int i = 0; i < _cachedRoomListResponse.rooms.Length; i++)
                {
                    if (_cachedRoomListResponse.rooms[i].roomState == "END")
                    {
                        endRoomCount++;
                        continue;
                    }

                    GameObject roomObj = Instantiate(_roomPrefab, _roomsContainer);

                    RoomData roomData = roomObj.AddComponent<RoomData>();
                    roomData.roomId = _cachedRoomListResponse.rooms[i].roomId;
                    roomData.roomState = _cachedRoomListResponse.rooms[i].roomState;
                    roomData.emptySpace = _cachedRoomListResponse.rooms[i].maxUserCount - _cachedRoomListResponse.rooms[i].userCount;

                    Vector3 position = roomObj.transform.localPosition;
                    position.y = (i - endRoomCount) * yOffset - 50f;
                    roomObj.transform.localPosition = position;

                    string _roomId = "Room " + _cachedRoomListResponse.rooms[i].roomId;
                    string _hostNickname = _cachedRoomListResponse.rooms[i].hostNickname;
                    string _usersCurMax = _cachedRoomListResponse.rooms[i].userCount + " / " + _cachedRoomListResponse.rooms[i].maxUserCount;
                    
                    TextMeshProUGUI roomStateText = roomObj.transform.GetChild(3).GetComponent<TextMeshProUGUI>();
                    string _roomState = "";
                    if (_cachedRoomListResponse.rooms[i].roomState != "NONE")
                    {
                        roomStateText.color = new Color(1.0f, 0f, 0f, 1.0f);
                        _roomState = "게임 중";
                    }
                    else
                    {
                        _roomState = "대기";
                    }

                    roomObj.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = _roomId;
                    roomObj.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = _hostNickname;
                    roomObj.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = _usersCurMax;
                    roomObj.transform.GetChild(3).GetComponent<TextMeshProUGUI>().text = _roomState;

                    Button joinButton = roomObj.transform.GetChild(4).GetComponent<Button>();
                    joinButton.onClick.AddListener(() => OnJoinButtonClicked(roomData.roomId, roomData.roomState, roomData.emptySpace));
                }

                _cachedRoomListResponse = null;
            }
            _updateRoomListFlag = false;
        }
        if(_warn)
        {
            _warnText.gameObject.SetActive(true);
            _warn = false;
        }
    }

    public void Request()
    {
        if (_flag)
		{
			NetworkingController networking = NetworkManager.getNetworkingController();
			requestRoomList(networking);
			_flag = false;
		}
    }

    public void OnReloadButtonClicked()
    {
        _flag = true;
        Request();
    }

    public void OnJoinButtonClicked(int roomId, string roomState, Int32 emptySpace)
    {
        if(roomState == "NONE" && emptySpace != 0)
        {
            _check.StartLogin(roomId, roomState, true);
        }
        else if(roomState == "PLAY")
        {
            _check.StartLogin(roomId, roomState, true);
        }
        else
        {
            _warn = true;
        }
    }

    private void requestRoomList(NetworkingController networking)
    {
        var request = new RoomListRequest();
        networking.sendRequest(request);
    }

    private void RoomListUpdated(object sender, MessageEventArgs e)
    {
        Debug.Log("RoomList Response");
        _cachedRoomListResponse = RoomListResponse.CreateFromJSON(e.Data);
        _updateRoomListFlag = true;
    }

    public class RoomListRequest : ClientRequest
    {
        public RoomListRequest()
        {
            type = requestType.ROOM_LIST.ToString();
        }
    }

    public class RoomListResponse
    {
        public RoomDto[] rooms;
        public static RoomListResponse CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<RoomListResponse>(jsonString);
        }
    }
}
