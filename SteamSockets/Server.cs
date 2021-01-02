using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

namespace Mirror.SteamSocketsTransport
{
  public class Server
  {
    public ushort networkPort { get; private set; }
    private FPSocketManager socketManager;

    public Server(
      SteamSockets transport, 
      int maxConnections, 
      uint steamAppID,
      SteamServerInit init,
      SteamSockets.SocketMode socketMode,
      string serverName
    )
    {
      if (transport.debug) { Debug.Log($"SteamSockets.Server: Starting"); }
      networkPort = init.GamePort;
      SteamServer.Init(steamAppID, init, false);
      SteamServer.ServerName = serverName;
      if (String.IsNullOrEmpty(transport.gsToken)) 
      { 
        Debug.Log($"No gsToken provided, logging on anonymously");
        SteamServer.LogOnAnonymous(); 
      }
      else
      {
        Debug.Log($"Logging on with gstoken");
        // Todo: Add SteamServer.LogOn(gsToken); when Facepunch.Steamworks supports it
      }
      if (transport.debug) { Debug.Log($"Creating socket manager ({socketMode})"); }
      switch (socketMode)
      {
        case SteamSockets.SocketMode.P2P:
          socketManager = SteamNetworkingSockets.CreateRelaySocket<FPSocketManager>();
          break;
        case SteamSockets.SocketMode.UDP:
          socketManager = SteamNetworkingSockets.CreateNormalSocket<FPSocketManager>(NetAddress.AnyIp(networkPort));
          break;
      }
      socketManager.transport = transport;
    }

    public void Shutdown()
    {
      socketManager.Close();
      socketManager = null;
    }

    public unsafe void Send(int connectionId, int channelId, ref ArraySegment<byte> segment)
    {
      if (socketManager.transport.debug) 
      { 
        Debug.Log($"Sending message to client {connectionId}"); 
      }
      if (socketManager.steamToMirrorIds.TryGetValue(connectionId, out uint steamConnectionId))
      { 
        fixed(byte * aPtr = segment.Array)
        {
          IntPtr ptr = (IntPtr) aPtr + segment.Offset;
          Result res = socketManager.Connected
            .First( x => x.Id == steamConnectionId)
            .SendMessage(
              ptr,
              segment.Count,
              socketManager.transport.Channels[channelId]
            );
          if (socketManager.transport.debug && res != Result.OK) 
          { 
            Debug.Log($"Message Send Failed: {res}"); 
          }
        }
      }
      else
      {
        Debug.Log($"Server.Send Unable to get steam connection.Id from mirror id mapping: connectionId: {connectionId}");
      }
    }

    public void Tick()
    {
      SteamServer.RunCallbacks();
      socketManager?.Receive();
    }

    public bool Disconnect(int connectionId)
    {
      try
      {
        if (socketManager.steamToMirrorIds.TryGetValue(connectionId, out uint steamConnectionId))
        {
          socketManager.steamToMirrorIds.Remove(connectionId);
          return socketManager.Connected
            .First( x => x.Id == steamConnectionId)
            .Close(false, 0, "Closing Connection");
        }
        else
        {
          Debug.Log($"Server.Disconnect Unable to get steam connection.Id from mirror id mapping: connectionId: {connectionId}");
          return false;
        }
      }
      catch
      {
        return false;
      }
    }

    public string GetClientAddress(int connectionId)
    {
      try
      {
        if (socketManager.steamToMirrorIds.TryGetValue(connectionId, out uint steamConnectionId))
        {
          return socketManager.Connected
            .First( x => x.Id == steamConnectionId )
            .ConnectionName;
        }
        else
        {
          Debug.Log($"Server.GetClientAddress Unable to get steam connection.Id from mirror id mapping: connectionId: {connectionId}");
          return "Unknown";
        }
      }
      catch
      {
        return "Unknown";
      }
    }

    public class FPSocketManager : SocketManager
    {
      public SteamSockets transport;
      public bool debug => transport.debug;
      private int nextConnectionID = 1;
      public BidirectionalDictionary<uint, int> steamToMirrorIds = new BidirectionalDictionary<uint, int>();
      
      public override void OnConnecting( Connection connection, ConnectionInfo data )
      {
        base.OnConnecting(connection, data);
        if (transport.debug) { Debug.Log( $"{data.Identity} is connecting" ); }
      }

      public override void OnConnected( Connection connection, ConnectionInfo data )
      {
        base.OnConnected(connection, data);
        if (transport.debug) {  Debug.Log( $"{data.Identity} has joined the game" ); }
        int connectionId = nextConnectionID;
        steamToMirrorIds.Add(connection.Id, connectionId);
        nextConnectionID++;
        connection.ConnectionName = data.Identity.ToString();
        transport.OnServerConnected.Invoke(connectionId);
      }

      public override void OnConnectionChanged(Connection connection, ConnectionInfo info )
      {
        base.OnConnectionChanged(connection, info);
      }

      public override void OnDisconnected( Connection connection, ConnectionInfo data )
      {
        base.OnDisconnected(connection, data);
        if (steamToMirrorIds.TryGetValue(connection.Id, out int connectionId))
        { 
          transport.OnServerDisconnected.Invoke(connectionId);
          steamToMirrorIds.Remove(connection.Id);
          nextConnectionID++;
          if (transport.debug) { Debug.Log( $"{data.Identity} has disconnected" ); }
        }
        else
        {
          Debug.Log($"Server.OnDisconnected: Unable to get steam connection.Id to mirror id mapping: connection.Id: {connection.Id}");
        }

      }

      public override void OnMessage( Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel )
      {

        if (steamToMirrorIds.TryGetValue(connection.Id, out int connectionId))
        { 
          if (transport.debug) 
          { 
            Debug.Log( $"We got a message from {identity} / connectionId: {connectionId}" );
          }
          base.OnMessage(connection, identity, data, size, messageNum, recvTime, channel);
          byte[] mIn = new byte[size];
          Marshal.Copy(data, mIn, 0, size);
          ArraySegment<byte> mOut = new ArraySegment<byte>(mIn);
          transport.OnServerDataReceived.Invoke(connectionId, mOut, channel);
        }
        else
        {
          Debug.Log($"Server.OnMessage: Unable to get steam connection.Id to mirror id mapping: connection.Id: {connection.Id}");
        }
      }
    }
  }
}