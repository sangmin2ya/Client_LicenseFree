using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using WebSocketSharp;

public class WorldSoundController : MonoBehaviour
{
    private bool _didMission = false;
    private string _missionState;
    private rVector3 _pos;
    private static readonly string _missionResponseName = responseType.PLAY_MISSION.ToString();
    // Start is called before the first frame update
    void Start()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.startListenOnMessage(MissionUpdated, _missionResponseName);
    }
    void OnDestroy()
    {
        NetworkingController networking = NetworkManager.getNetworkingController();
        networking.stopListenOnMessage(MissionUpdated, _missionResponseName);
    }

    // Update is called once per frame
    void Update()
    {
        if (_didMission && ModelManager._userType == roomUserType.COP.ToString() && _missionState != missionState.START.ToString())
        {
            GetComponent<AudioSource>().Play();
            _didMission = false;
        }
    }
    private void MissionUpdated(object sender, MessageEventArgs e)
    {
        Debug.Log(e.Data);
        PlayMissionResponse MissionInfo = PlayMissionResponse.CreateFromJSON(e.Data);
        _didMission = true;
        _missionState = MissionInfo.missionState;
    }
    public class AssassinKillResponse
    {
        public string targetUsername;
        public static AssassinKillResponse CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<AssassinKillResponse>(jsonString);
        }
    }
    public class PlayMissionResponse
    {
        public int roomId;
        public int missionPhase;
        public rVector3 missionPos;
        public string crimeType;
        public string username;
        public string missionState;
        public static PlayMissionResponse CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<PlayMissionResponse>(jsonString);
        }
    }
}