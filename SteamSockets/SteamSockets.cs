using System;
using System.Net;
using UnityEngine;
using UnityEngine.Events;
using Steamworks;
using Steamworks.Data;

namespace Mirror.SteamSocketsTransport
{
    [HelpURL("https://github.com/pfoote/SteamSocketsTransport")]
    [DisallowMultipleComponent]
    public class SteamSockets : Transport
    {
        public bool debug = false;
        
        [SerializeField]
        public SocketMode socketMode = SocketMode.UDP; 
        
        public uint SteamAppID = 480;
        public string gameName = "Default Game";
        public string modDir = "Game";
        public string serverName = "Default Server";

        [SerializeField]
        public ushort gamePort = 27015;

        [SerializeField]
        public ushort queryPort = 27016;

        [SerializeField]
        public ushort steamPort = 27017;
        
        [SerializeField]
        public SendType[] Channels = new SendType[4] 
        { 
          SendType.Unreliable,
          SendType.NoNagle,
          SendType.NoDelay,
          SendType.Reliable
        };

        private Server server;
        private Client client;
        public LocalSteamClient localSteamClient { get; private set; }

        [HideInInspector]
        public string gsToken { get; private set; }

        private void ProcessArguments()
        {
          string[] args = System.Environment.GetCommandLineArgs();
          for (int i = 0; i < args.Length; i++) 
          {
            switch (args[i].ToLower())
            {
              case "+debug":
                debug = true;
                break;
              case "+server.hostname":
                serverName = args[i + 1];
                break;
              case "+server.gameport":
                gamePort = Convert.ToUInt16(args[i + 1]);
                break;
              case "+server.queryport":
                queryPort = Convert.ToUInt16(args[i + 1]);
                break;
              case "+server.steamport":
                steamPort = Convert.ToUInt16(args [i + 1]);
                break;
              case "+server.socketmode":
                switch(args[i + 1].ToUpper())
                {
                  case "UDP":
                    socketMode = SocketMode.UDP;
                    break;
                  case "P2P":
                    socketMode = SocketMode.P2P;
                    break;
                  // SDR - Will be added when Facepunch.Steamworks has support for it   
                  /*  
                  case "SDR":
                    socketMode = SocketMode.SDR;
                    break;
                  */
                }
                break;
            }
          }
        }

        public enum SocketMode {
          UDP,
          P2P
          //SDR - Will be added when Facepunch.Steamworks has support for it
        }

        public static bool IsHeadless {
          get { 
            return UnityEngine.SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null; 
          }
        }

        public override bool Available()
        {
          if (IsHeadless) { return true; }
          try
          {
              return SteamClient.IsValid;
          }
          catch
          {
              return false;
          }
        }

        public override bool ClientConnected() => ClientActive() && client.Connected;

        public override void ClientConnect(string address)
        {
          client = new Client(this, address, socketMode);
        }
 
        public override void ClientConnect(Uri uri)
        {
          string address;
          switch (uri.Scheme)
          {
            case "ip":
              address = $"{uri.Host}:{uri.Port}";
              break;
            case "steamid":
              address = uri.Host;
              break;
            default:
              address = uri.Host;
              break;
          }
          ClientConnect(address);
        }

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
          client.Send(channelId, ref segment);
        }

        public override void ClientDisconnect()
        {
          client.Disconnect();
          client = null;
        }

        public override Uri ServerUri()
        {
            switch (socketMode)
            {
              case SocketMode.UDP:
                Uri udpUri = new Uri($"ip://{IPAddress.Loopback}:{gamePort}");
                return udpUri;
              case SocketMode.P2P:
                Uri steamIdUri = new Uri($"steamid://{SteamServer.SteamId.ToString()}");
                return steamIdUri;
             } 
             return new Uri($"ip://{IPAddress.Loopback}:{gamePort}");
        }

        public override bool ServerActive() => server != null;

        public bool ClientActive() => client != null;

        public void Awake()
        {
          if (!IsHeadless)
          {
            localSteamClient = new LocalSteamClient(this, SteamAppID);
          }
        }

        public override void ServerStart()
        {
          if (!ServerActive())
          {
            ProcessArguments();
            server = new Server(
              this,
              NetworkManager.singleton.maxConnections,
              SteamAppID,
              new SteamServerInit(modDir, gameName)
              {
                DedicatedServer = IsHeadless,
                GamePort = gamePort,
                IpAddress =  System.Net.IPAddress.Any,
                QueryPort = queryPort,
                Secure = true,
                SteamPort = steamPort,
                VersionString = Application.version
              },
              socketMode,
              serverName
            );
          }
        }

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
          server.Send(connectionId, channelId, ref segment);
        }

        public override bool ServerDisconnect(int connectionId)
        {
          return server.Disconnect(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
          return server.GetClientAddress(connectionId);
        }

        public override void ServerStop()
        {
          if (ServerActive())
          {
            server.Shutdown();
            server = null;
          }
        }

        public override int GetMaxPacketSize(int channelId)
        {
          switch (Channels[channelId])
          {
              case SendType.Unreliable:
                  return 1200; 
              case SendType.NoNagle:
                  return 1200;
              case SendType.NoDelay:
                  return 1200;
              case SendType.Reliable:
                  return 1048576;
              default:
                  throw new NotSupportedException();
          }
        }

        public override void Shutdown()
        {
          server?.Shutdown();
          client?.Disconnect();
          localSteamClient?.Shutdown();
          server = null;
          client = null;
          localSteamClient = null;
          Debug.Log("Transport shut down");
        }

        public void LateUpdate()
        {
          if (!IsHeadless) { localSteamClient?.Tick(); }
          server?.Tick();
          client?.Tick();
        }

        public override void OnApplicationQuit()
        {
          Shutdown();
        }
    }
}
