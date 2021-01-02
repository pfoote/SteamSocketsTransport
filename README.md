# SteamSocketsTransport

This is a **[Steam Sockets](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets)** Transport for **[Mirror](https://mirror-networking.com/)**, using **[Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks)**, written to allow use of steam dedicated servers. If you don't need dedicated servers, check out **[FizzySteamworks](https://github.com/Chykary/FizzySteamworks)** or **[FizzyFacepunch](https://github.com/Chykary/FizzyFacepunch)** instead.

| | UDP| P2P | SDR | 
| -- | -- | -- | -- | 
| **Client** | ✔️ | ✔️ | ❌ |
| **Listen Server** | ✔️| ✔️ | ❌ | 
| **Dedicated Server**| ✔️ | ✔️*  | ❌ |
Notes / Cop-outs / Musings: 
* P2P dedicated servers will need to share their SteamID with clients externally for the time being. P2P Listen servers can use [SteamMatchmaking.CreateLobbyAsync](https://wiki.facepunch.com/steamworks/SteamMatchmaking.CreateLobbyAsync )/ [Lobby.SetLobbyGameServer](https://wiki.facepunch.com/steamworks/Data.Lobby.SetGameServer).
This is because [ISteamGameServer](https://partner.steamgames.com/doc/api/ISteamGameServer) doesn't support adding SteamID's to be picked up by [RequestInternetServerList](https://partner.steamgames.com/doc/api/ISteamMatchmakingServers#RequestInternetServerList), and [CreateLobby](https://partner.steamgames.com/doc/api/ISteamMatchmaking#CreateLobby) / [SetLobbyGameServer](https://partner.steamgames.com/doc/api/ISteamMatchmaking#SetLobbyGameServer) can't be called from a dedicated server as [ISteamMatchmaking](https://partner.steamgames.com/doc/api/ISteamMatchmaking) needs a running steam client.
* **[SDR](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay)** will be supported once it is available in **[Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks)**. It will also need an external mechanism for sharing dedicated server SteamID's (in the form of a Game Coordinator), however this is more formalized compared to P2P and integrates PKI certificate authentication.
* Message sending / receiving uses the `unsafe` and `fixed` keywords to use pointers for data access, to avoid excessive garbage collection as per [Facepunch.Steamworks' suggestion](https://wiki.facepunch.com/steamworks/Data.Connection.SendMessage). Unsure how well this works on message receipt, due to needing to supply mirror with an array segment for ingestion.
  
## Dependencies

1. **[Unity](https://unity.com/)**

2.  **[Mirror](https://github.com/vis2k/Mirror)**

3. **[Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks)**
A copy of precompiled binaries is included in the [unitypackage](https://github.com/pfoote/SteamSocketsTransport/releases), but if you are installing [SteamSocketsTransport]() from source, you will need to build Facepunch.Steamworks from master. Instructions can be found [here](https://wiki.facepunch.com/steamworks/#howtobuildfrommasterbranch).

## Setting Up

1. Install Mirror from the  **[Unity asset store](https://assetstore.unity.com/packages/tools/network/mirror-129321)**.

2. Install the SteamSocketsTransport **[unitypackage](https://github.com/pfoote/SteamSocketsTransport/releases)** from the latest release.

3. In your **NetworkManager** object, replace **KCP** script with **SteamSocketsTransport** script.

4. Enter your `Steam App ID`, `Socket Type` & `Game Name` in the **SteamSocketsTransport** script.

## Command Line Arguments
| Argument | Use |
|--|--|
| `+debug` | Makes the transport output verbose on the command line / Unity Editor. Only use this for testing/debugging, it **will** impact performance. |
| `+server.hostname "SERVER HOSTNAME"`| Overrides the `serverName` set in the transport |
| `+server.gameport 27015`| Overrides the `gamePort` set in the transport |
|`+server.queryport 27016` | Overrides the `queryPort` set in the transport |
| `+server.steamport 27017` | Overrides the `steamPort` set in the transport |
| `+server.socketmode UDP` | Overrides the `socketMode` in the transport to use UDP
| `+server.socketmode P2P` | Overrides the `socketMode` in the transport to use P2P |

## Host

* Import the `Mirror` and `Steamworks` namespaces:
```
using Mirror;
using Mirror.SteamSocketsTransport;
using Steamworks;
using Steamworks.Data;
```

* Create a reference to your `NetworkManager` components: 
```
NetworkManager networkManager  =  GameObject.Find("NetworkManager")
	.gameObject.GetComponent<NetworkManager>();
Transport steamSocketsTransport = GameObject.Find("NetworkManager")
	.gameObject.GetComponent<Transport>();
```

### Create Listen Server
* Set up a Lobby Created callback to start the server when the lobby is created:
```
public void OnLobbyCreatedCallback(Result result, Lobby lobby)
{
	if (result == Result.OK) 
	{
		if(steamSocketsTransport.socketMode == SocketMode.P2P)
		{
			lobby.SetGameServer(Steamworks.SteamServer.SteamId);
		}
		if(steamSocketsTransport.socketMode == SocketMode.UDP)
		{
			lobby.SetGameServer(
				SteamServer.PublicIp.ToString(),
				steamSocketsTransport.gamePort
			);
		}
		networkManager.StartHost();
	}
}
```
* Register the callback & create the lobby:
```
SteamMatchmaking.OnLobbyCreated += OnLobbyCreatedCallback;
SteamMatchmaking.CreateLobbyAsync(networkManager.maxConnections);
```

### Creaete Dedicated Server
* Make sure [autoStartServerBuild](https://mirror-networking.com/docs/api/Mirror.NetworkManager.html#Mirror_NetworkManager_autoStartServerBuild) is `true` in your `NetworkManager`.
* Create a build in Unity with `Server Build` checked, or use the `BuildOptions.EnableHeadlessMode` flag in `BuildOptions` if you are using `BuildPipeline.BuildPlayer`.
* Run the built executable (use the **Command Line Arguments** supplied above to override what's defined in your `NetworkManager`

**UDP**
* Nothing additional is needed, the server will automatically be advertised with the master server

**P2P**
* You will need to externally publish the game server's SteamID. You can use a callback to pick this up when the game server logs into steam:
```
public void OnSteamServersConnectedCallback
{
	if (SteamServer.LoggedOn)
	{
		// Publish SteamServer.SteamId.ToString()
	}
}

SteamServer.OnSteamServersConnected += OnSteamServersConnectedCallback;
```

## Client

* Import the `Mirror` and `Steamworks` namespaces:
```
using Mirror;
using Mirror.SteamSocketsTransport;
using Steamworks;
using Steamworks.Data;
```

* Create a reference to your `NetworkManager` components: 
```
NetworkManager networkManager  =  GameObject.Find("NetworkManager")
	.gameObject.GetComponent<NetworkManager>();
Transport steamSocketsTransport = GameObject.Find("NetworkManager")
	.gameObject.GetComponent<Transport>();
```

**Connect to Listen Server**

* Create a find lobbies function:
```
void  FindLobbies()
{
	Lobby[] lobbies =  await  SteamMatchmaking.LobbyList.RequestAsync();
	if (lobbies  !=  null)
	{
		for(int i; i  <  lobbies.Length; i++)
		{
			AddLobbyToList(lobby);

		}
	}
}
```
* Create a display lobby list function:
```
void  AddLobbyToList(Lobby  lobby)
{
	// https://wiki.facepunch.com/steamworks/Data.Lobby
	// Use this to display properties / method results from the above link
	// And then finally set a button onClick action to join the lobby, eg:
	// GetComponent<Button>().onClick.AddListener(() => this.JoinLobby(lobby));
}
```
* Create a join lobby function:
```
void  JoinLobby(Lobby  lobby)
{
	RoomEnter lobbyJoinStatus = await lobby.Join();
}
```

* Create Lobby Entered callback:
```
void  OnLobbyEnteredCallback(Lobby  lobby)
{
	ushort ip;
	ushort port;
	SteamId serverId;

	bool hasGameServerInfo = lobby.GetGameServer(ref  ip, ref  port, ref  serverId);

	if (hasGameServerInfo)
	{
		switch (networkManager.socketMode)
		{
			case SocketMode.UDP:
				networkManager.networkAddress  =  $"{ip}:{port}";
				networkManager.StartClient();
				break;
			case  SocketMode.P2P:
				networkManager.networkAddress  =  serverId.ToString();
				networkManager.StartClient();
				break;
		}
	}
}
```
* Register the callback somewhere in your constructor:
```
SteamMatchmaking.OnLobbyEntered  +=  OnLobbyEnteredCallback;
```

### Connect to Dedicated Server

**UDP**

* Create a reference to your `NetworkManager` components: 
```
NetworkManager networkManager  =  GameObject.Find("NetworkManager")
	.gameObject.GetComponent<NetworkManager>();
Transport steamSocketsTransport = GameObject.Find("NetworkManager")
	.gameObject.GetComponent<Transport>();
```

* Create Server List callback:
```
void  OnServerListCallback()
{
	if (Request.Responsive.Count  ==  0) { return; }
	foreach (ServerInfo server in  Request.Responsive )
	{
		AddServerToList(server);
	}
	Request.Responsive.Clear();
}
```
* Create Add to Server List function:
```
void  AddServerToList(ServerInfo  server)
{
	// https://wiki.facepunch.com/steamworks/Data.ServerInfo
	// Use this to display properties / method results from the above link
	// And then finally set a button onClick action to connect to the server, eg:
	// GetComponent<Button>().onClick.AddListener(() => this.JoinServer(server));
}
```
* Create Join Server function:
```
void  JoinServer(ServerInfo  server)
{
	networkManager.networkAddress  =  $"{server.Address}:{server.ConnectionPort}";
	networkManager.StartClient();
}
```

* Create List Servers function:
```
void ListServers()
{
	Request = new Steamworks.ServerList.Internet();
	Request.OnChanges += OnServerListCallback;
	Request.RunQueryAsync(30);
}
```

  **P2P**
  * You will need to write a function to get SteamID's of your dedicated servers externally
  * To Join the server once you have it's SteamID:
```
void JoinServerP2P(string steamIdString)
{
	networkManager.networkAddress = steamIdString;
	networkManager.StartClient();
}
```

## Help
Feel free to submit pull requests, bug reports.
Mirror **[Docs](https://mirror-networking.com/docs/Transports)**,  **[Discord](https://discord.gg/N9QVxbM)**.

## License

[MIT](https://github.com/pfoote/SteamSocketsTransport/LICENSE) - go nuts  🥜 and/or bananas  🍌
