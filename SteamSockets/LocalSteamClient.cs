using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace Mirror.SteamSocketsTransport
{
  public class LocalSteamClient
  {
    private SteamSockets transport;
    public LocalSteamClient(SteamSockets _transport, uint SteamAppID)
    {
      try
      {
        SteamClient.Init(SteamAppID, false);
      }
      catch (Exception e)
      { 
        Debug.LogError($"Steam could not initiialize: {e.Message}");
      }
      transport = _transport;
      if (SteamClient.IsValid)
      {
        Debug.Log("SteamClient initialized");
      }
    }

    public void Tick()
    {
      SteamClient.RunCallbacks();
    }

    public void Shutdown()
    {
      SteamClient.Shutdown();
    }
  }
}