using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PlaylistVoting.core;
using ZeepkistClient;

namespace PlaylistVoting.api;

public class RestController
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private static string _sessionToken;

    private static string Token => !string.IsNullOrEmpty(_sessionToken) ? _sessionToken : VotingConfig.Instance.WebToken;
    private static string BaseUrl => VotingConfig.Instance.WebApiUrl;

    public string GetSessionToken() => _sessionToken;

    public async Task<bool> LoginWithSteamTicketAsync(string ticketHex, CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/login/steam";
        FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("ticket", ticketHex)
        });

        try
        {
            HttpResponseMessage response = await HttpClient.PostAsync(url, content, ct);
            if (response.IsSuccessStatusCode)
            {
                _sessionToken = await response.Content.ReadAsStringAsync();
                return true;
            }
        }
        catch (Exception)
        {
            // Log error if needed
        }

        return false;
    }

    public async Task<string> GetCurrentLevelNameAsync(CancellationToken ct = default) => await GetAsync($"{BaseUrl}/currentLevel/name", ct);

    public async Task<string> GetCurrentAuthorAsync(CancellationToken ct = default) => await GetAsync($"{BaseUrl}/currentLevel/author", ct);

    public async Task<VoteResult> FetchVoteTotalsAsync(CancellationToken ct = default)
    {
        string content = await GetAsync($"{BaseUrl}/result?result=TOTAL", ct);
        return ParseVoteResult(content);
    }

    public async Task<VoteResult> SubmitVoteAsync(ulong playerId, VotingType votingType, CancellationToken ct = default)
    {
        string username = TryGetPlayerName(playerId);
        string vote = GetVotingTypeAsString(votingType);

        string content = await GetAsync($"{BaseUrl}/" +
                                        $"vote?" +
                                        $"platformUserId={playerId}&" +
                                        $"username={username}&" +
                                        $"platform=STEAM&" +
                                        $"vote={vote}", ct);
        return ParseVoteResult(content);
    }

    public async Task<bool> SetCurrentLevelAsync(string uid, string levelName, string author, string workshopID, CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/currentLevel";
        FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("uid", uid),
            new KeyValuePair<string, string>("name", levelName),
            new KeyValuePair<string, string>("author", author),
            new KeyValuePair<string, string>("workshopID", workshopID)
        });

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        HttpResponseMessage response = await HttpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private string TryGetPlayerName(ulong playerId) => ZeepkistNetwork.TryGetPlayer(playerId, out ZeepkistNetworkPlayer player) ? player.Username : playerId.ToString();

    private static VoteResult ParseVoteResult(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new VoteResult(0, 0, 0);
        }

        try
        {
            // If it's JSON: {"YesVotes": 10, "NoVotes": 5, "AbstainVotes": 2}
            return JsonConvert.DeserializeObject<VoteResult>(content) ?? new VoteResult(0, 0, 0);
        }
        catch (Exception)
        {
            // Fallback for non-JSON or other formats if needed
            return new VoteResult(0, 0, 0);
        }
    }

    public async Task<string> GetAsync(string url, CancellationToken ct = default)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        HttpResponseMessage response = await HttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ResetVotesAsync(CancellationToken ct = default)
    {
        string url = $"{BaseUrl}/reset";
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        HttpResponseMessage response = await HttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string GetVotingTypeAsString(VotingType votingType)
    {
        return votingType switch
        {
            VotingType.Yes => "YES",
            VotingType.No => "NO",
            VotingType.Remove => "REMOVE",
            VotingType.Abstain => "ABSTAIN",
            _ => throw new ArgumentException("Invalid voting type", nameof(votingType))
        };
    }
}