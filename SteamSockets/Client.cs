using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

namespace Mirror.SteamSocketsTransport
{
  public class Client
  {
    public bool Connected { get; private set; }
    private FPConnectionManager connectionManager;

    public Client(SteamSockets transport, string address, SteamSockets.SocketMode socketMode)
    {
      if (transport.debug) { Debug.Log($"SteamSockets.Client: Starting"); }
      if (transport.debug) { Debug.Log($"Creating connection manager ({socketMode})"); }
      switch(socketMode)
      {
        case SteamSockets.SocketMode.P2P:
          if (transport.debug) { Debug.Log($"Connecting to P2P server steamid {address}"); } 
          SteamId connectSteamId = new SteamId();
          connectSteamId.Value = Convert.ToUInt64(address);
          connectionManager = SteamNetworkingSockets.ConnectRelay<FPConnectionManager>(connectSteamId);
          break;
        case SteamSockets.SocketMode.UDP:
          string[] ipPort = address.Split(':');
          NetAddress netAddress = NetAddress.From(ipPort[0], Convert.ToUInt16(ipPort[1]));
          if (transport.debug) { Debug.Log($"Connecting to UDP server {netAddress}"); } 
          connectionManager = SteamNetworkingSockets.ConnectNormal<FPConnectionManager>(netAddress);
          break;
      }
      connectionManager.transport = transport;
      if (transport.debug) 
      {
         Debug.Log($"Connecting: {connectionManager.Connecting} Connected: {connectionManager.Connected}");
      }
    }

    public class FPConnectionManager : ConnectionManager
    {
      public SteamSockets transport;

      public override void OnConnected( ConnectionInfo info )
      {
        base.OnConnected(info);
        if (transport.debug) { Debug.Log($"Connected to {info.Address}"); }
        transport.OnClientConnected.Invoke();
      }

      public override void OnConnecting( ConnectionInfo info )
      {
        base.OnConnecting(info);
        if (transport.debug) { Debug.Log($"Connecting to {info.Address}"); }
      }

      public override void OnConnectionChanged( ConnectionInfo info )
      {
        base.OnConnectionChanged(info);
        if (transport.debug) { Debug.Log($"Connection changed: {info.State}"); }
      }

      public override void OnDisconnected( ConnectionInfo info )
      {
        base.OnDisconnected(info);
        if (transport.debug) { Debug.Log($"Disconnecting from {info.Address}"); }
        transport.OnClientDisconnected.Invoke();
      }

      public override void OnMessage( System.IntPtr data, int size, Int64 messageNum, Int64 recvTime, int channel )
      {
        base.OnMessage(data, size, messageNum, recvTime, channel);
        if (transport.debug) { Debug.Log("Recieved message from server"); }
        byte[] mIn = new byte[size];
        Marshal.Copy(data, mIn, 0, size);
        ArraySegment<byte> mOut = new ArraySegment<byte>(mIn);
        transport.OnClientDataReceived.Invoke(mOut, channel);
      }
    }

    public void Disconnect()
    {
      connectionManager.Connection.Close();
      connectionManager.Close();
      connectionManager = null;
    }

    public void Tick()
    {
      connectionManager?.Receive();
    }

    public unsafe void Send(int channelId, ref ArraySegment<byte> segment)
    {
        if (connectionManager.transport.debug) { Debug.Log($"Sending message to server"); }
        fixed(byte * aPtr = segment.Array)
        {
          IntPtr ptr = (IntPtr)aPtr + segment.Offset;
          Result res = connectionManager.Connection.SendMessage(
            ptr,
            segment.Count,
            connectionManager.transport.Channels[channelId]
          );
          if (connectionManager.transport.debug) {
            if (res != Result.OK) 
            { 
              Debug.Log($"Message Send Failed: {res}"); 
            }
          }
        }
     }
  }
}