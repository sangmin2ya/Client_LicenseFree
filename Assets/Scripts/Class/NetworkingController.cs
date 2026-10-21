using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using WebSocketSharp;

public class NetworkingController
{

	private WebSocket websocket = null;
	private Dictionary<String, HashSet<EventHandler<MessageEventArgs>>> onMessageFunctions = new Dictionary<String, HashSet<EventHandler<MessageEventArgs>>>();
	private readonly ConcurrentQueue<PendingMessage> pendingMessages = new ConcurrentQueue<PendingMessage>();

	public NetworkingController()
	{
		Debug.Log("Networking class initiated!");
	}

	public int connect(string ip, string port, string user, string password)
	{
		Debug.Log("Arg ip: " + ip);

		if (websocket != null && websocket.IsAlive)
		{
			Debug.LogWarning("Websocket is already connected!");
			return 0;
		}

		Debug.Log("Creating websocket");
		string serverAddress = "ws://" + ip + ":" + port;
		websocket = new WebSocket(serverAddress + "/unitysocket");
#if UNITY_EDITOR
		websocket.Log.Level = LogLevel.Trace;
#endif

		websocket.SetCredentials(user, password, false);

		websocket.OnMessage += (sender, e) =>
		{
			var responseType = ServerResponse.CreateFromJSON(e.Data).type;
			string pattern = "\"errMessage\":\"(.*?)\"";
			Match match = Regex.Match(e.Data, pattern);

			if (match.Success)
				Debug.Log("ERROR : " + match.Groups[1].Value);
			else
				Debug.Log(responseType);

			// 소켓 스레드에서는 큐에 넣기만 하고, 핸들러는 메인 스레드에서 수신 순서대로 실행한다
			pendingMessages.Enqueue(new PendingMessage { type = responseType, sender = sender, args = e });
		};

		websocket.OnOpen += (sender, e) =>
		{
			Debug.Log("Socket Open");
		};

		websocket.OnError += (sender, e) =>
		{
			Debug.Log("Error " + e.Message);
			Debug.Log("Exception " + e.Exception);
		};

		websocket.OnClose += (sender, e) =>
		{
			Debug.Log("Close Reason: " + e.Reason);
			Debug.Log("Close Code: " + e.Code);
			Debug.Log("Close Clean? " + e.WasClean);
		};

		Debug.Log("Connecting...");
		websocket.Connect();

		if (!websocket.IsAlive) {
			return -1;
		}

		Debug.Log("Connection isAlive : " + websocket.IsAlive);
		Debug.Log("Connection Status : " + websocket.ReadyState);

		return 1;
	}


	public void startListenOnMessage(EventHandler<MessageEventArgs> funct, String responseType)
	{
		if (!onMessageFunctions.ContainsKey(responseType))
		{
			onMessageFunctions[responseType] = new HashSet<EventHandler<MessageEventArgs>>();
		}
		onMessageFunctions[responseType].Add(funct);
	}

	public void stopListenOnMessage(EventHandler<MessageEventArgs> funct, String responseType)
	{
		if (onMessageFunctions.ContainsKey(responseType))
		{
			onMessageFunctions[responseType].Remove(funct);
		}
	}

	public bool isConnected()
	{
		return websocket.IsAlive;
	}

	public void sendRequest(ClientRequest request)
	{
		String json = JsonConvert.SerializeObject(request);

		if (websocket != null)
		{
			Debug.Log("Sending request of type " + request.type + ": " + json);
			websocket.Send(json);
		}
	}

	// NetworkManager.Update에서 매 프레임 호출. 쌓인 메시지를 메인 스레드에서 동기 처리한다.
	public void DispatchPendingMessages()
	{
		while (pendingMessages.TryDequeue(out PendingMessage message))
		{
			if (message.type == null || !onMessageFunctions.TryGetValue(message.type, out var handlers))
			{
				continue;
			}
			// 핸들러 안에서 구독/해제해도 순회가 깨지지 않도록 스냅샷으로 순회
			foreach (EventHandler<MessageEventArgs> eventHandler in handlers.ToArray())
			{
				try
				{
					eventHandler(message.sender, message.args);
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}
		}
	}

	private struct PendingMessage
	{
		public string type;
		public object sender;
		public MessageEventArgs args;
	}

	public class ServerResponse
	{
		public string type;

		public static ServerResponse CreateFromJSON(String jsonString)
		{
			return JsonUtility.FromJson<ServerResponse>(jsonString);
		}
	}
}
