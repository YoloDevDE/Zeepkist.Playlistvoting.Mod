using System;
using System.Collections.Generic;
using PlaylistVoting.Data.Models;
using PlaylistVoting.Networking.Zeepkist;
using ZeepkistClient;

namespace PlaylistVoting.Display;

public class OverlayService
{
    public static OverlayService Instance { get; private set; }

    public static void Init(string title)
    {
        Instance ??= new OverlayService(title);
    }

    private const float SendInterval = 0.1f;
    private readonly Queue<string> _messageQueue = new Queue<string>();
    private readonly string _title;
    private DateTime _lastSendTime;

    public OverlayService(string title)
    {
        _title = title;
    }

    public void UpdateVotingDisplay(string sessionName, LevelMetadata level, VoteResult result, bool isConnected)
    {
        string message = VotingDisplayManager.BuildVoteDisplayMessage(_title, sessionName, level, result, isConnected);
        _messageQueue.Enqueue(message);
        Refresh();
    }

    public void Refresh()
    {
        if (ZeepkistNetwork.CurrentLobby == null || ZeepkistNetwork.CurrentLobby.GameState != 0)
        {
            return;
        }

        if (_messageQueue.Count == 0)
        {
            return;
        }

        if ((DateTime.UtcNow - _lastSendTime).TotalSeconds < SendInterval)
        {
            return;
        }

        string message = _messageQueue.Dequeue();
        VotingDisplayManager.SendVotingUpdate(message);
        _lastSendTime = DateTime.UtcNow;
    }

    public void Clear()
    {
        _messageQueue.Clear();
        VotingDisplayManager.ClearDisplay();
    }

    public void SendLocalMessage(string message)
    {
        ZeepkistNetworkHelper.SendLocalPrivateMessage(message);
    }
}