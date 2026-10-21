using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using WebSocketSharp;

public class NetworkingController
{

	private WebSocket websocket = null;
	private Dictionary<String, HashSet<EventHandler<MessageEventArgs>>> onMessageFunctions = new Dictionary<String, HashSet<EventHandler<MessageEventArgs>>>();

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

			if (onMessageFunctions.ContainsKey(responseType))
			{
				foreach (EventHandler<MessageEventArgs> eventHandler in onMessageFunctions[responseType])
				{
					eventHandler.BeginInvoke(sender, e, EndAsyncEvent, null);
				}
			}
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

	// TODO: can this be handled better?
	private void EndAsyncEvent(IAsyncResult iar)
	{
		var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
		var invokedMethod = (System.EventHandler<WebSocketSharp.MessageEventArgs>)(ar.AsyncDelegate);

		try
		{
			invokedMethod.EndInvoke(iar);
		}
		catch
		{
			// Handle any exceptions that were thrown by the invoked method
			Debug.Log("An event listener went kaboom!");
		}
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
