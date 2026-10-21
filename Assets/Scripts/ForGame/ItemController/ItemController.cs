using System.Collections;
using System;
using UnityEngine;
using WebSocketSharp;
using Newtonsoft.Json;
using TMPro;
using Unity.VisualScripting;

public class ItemController : MonoBehaviour
{
    [SerializeField] GameObject _player;
    [SerializeField] GameObject _itemIcon;
    [SerializeField] GameObject _smoke;
    [SerializeField] AudioClip _smokeSound;
    private bool _itemUsed = false;
    private rVector3 _itemPos;
    private static readonly string _ResponseName = responseType.USE_ITEM.ToString();
    void Start()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.startListenOnMessage(ItemUpdated, _ResponseName);
    }
    void OnDestroy()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.stopListenOnMessage(ItemUpdated, _ResponseName);
    }
    // Update is called once per frame
    void Update()
    {
        //재접 후 아이템 재배치
        ReplaceSmoke();
        //연막탄 사용가능하거나 안죽었을 때
        if (!ModelManager._isDisconnected)
        {
            if (ModelManager._itemValid > 0 && !ModelManager._isDead)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    ModelManager._itemValid--;
                    SendRequest(new rVector3(_player.transform.position.x, _player.transform.position.y, _player.transform.position.z));
                    //스턴시 스턴해제
                    if (ModelManager._isStunned)
                    {
                        SendRequestStun(ModelManager._username, stunState.END.ToString());
                    }
                }
            }
            _itemIcon.SetActive(ModelManager._itemValid > 0);
            if (_itemUsed)
            {
                _itemUsed = false;
                StartCoroutine("Smoke");
            }
        }
    }
    IEnumerator Smoke()
    {
        float smokeTime = 25f;
        GameObject smoke = Instantiate(_smoke, new Vector3(_itemPos.x, _itemPos.y, _itemPos.z), Quaternion.identity);
        AudioSource.PlayClipAtPoint(_smokeSound, new Vector3(_itemPos.x, _itemPos.y, _itemPos.z));

        while (smokeTime >= 0)
        {
            smokeTime -= Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
        Destroy(smoke);
    }
    private void ReplaceSmoke()
    {
        if (ModelManager._isReconnected && ReconnectionController._recentSmoke != null)
        {
            foreach (rVector3 pos in ReconnectionController._recentSmoke)
            {
                Debug.Log("연막 발생");
                _itemPos = pos;
                StartCoroutine("Smoke");
            }
            ReconnectionController._recentSmoke = null;
        }
    }
    private void ItemUpdated(object sender, MessageEventArgs e)
    {
        Debug.Log(e.Data);
        UseItemResponse itemInfo = UseItemResponse.CreateFromJSON(e.Data);
        _itemUsed = true;
        _itemPos = itemInfo.itemPos;
    }
    public void SendRequest(rVector3 itemPos)
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        RequestUseItem(networking, itemPos);
    }
    private void RequestUseItem(NetworkingController networking, rVector3 itemPos)
    {
        var request = new UseItemRequest(itemPos);
        networking.sendRequest(request);
    }
    public void SendRequestStun(string username, string stunState)
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        RequestStun(username, stunState, networking);
    }
    private void RequestStun(string username, string stunState, NetworkingController networking)
    {
        //검문시 타겟 유저네임이랑 시간 보내줌
        var request = new StunRequest(username, PingManager.GameTime.ToString(), stunState);
        networking.sendRequest(request);
    }
    public class UseItemRequest : ClientRequest
    {
        public int roomId;
        public rVector3 itemPos;
        public string username;
        public UseItemRequest(rVector3 itemPos)
        {
            type = requestType.USE_ITEM.ToString();
            this.roomId = ModelManager._roomId;
            this.itemPos = itemPos;
            this.username = ModelManager._username;
        }
    }
    public class StunRequest : ClientRequest
    {
        public string targetUsername;
        public string targetingAt;
        public string stunState;
        public int roomId;
        public StunRequest(string target, string targetAt, string stunState)
        {
            type = requestType.STUN.ToString();
            this.targetUsername = target;
            this.targetingAt = targetAt;
            this.stunState = stunState;
            roomId = ModelManager._roomId;
        }
    }
    public class UseItemResponse
    {
        public rVector3 itemPos;
        public static UseItemResponse CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<UseItemResponse>(jsonString);
        }
    }
}
