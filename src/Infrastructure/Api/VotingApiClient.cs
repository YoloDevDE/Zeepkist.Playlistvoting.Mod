using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Models;
using ZeepkistClient;

namespace PlaylistVoting.Infrastructure.Api;

public class VotingApiClient
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private static string _sessionToken;
    private static string _userId;

    private static string Token => !string.IsNullOrEmpty(_sessionToken) ? _sessionToken : VotingConfig.Instance.WebToken;
    private static string BaseUrl => VotingConfig.Instance.WebApiUrl;

    public string GetSessionToken() => _sessionToken;
    public string GetUserId() => _userId;

    public async Task<bool> LoginWithSteamTicketAsync(string ticketHex, CancellationToken ct = default)
    {
        string authBaseUrl = BaseUrl;
        if (authBaseUrl.EndsWith("/playlistvoting"))
        {
            authBaseUrl = authBaseUrl.Substring(0, authBaseUrl.Length - "/playlistvoting".Length);
        }

        string url = $"{authBaseUrl}/auth/steam/ticket";

        StringContent content = new StringContent(ticketHex, Encoding.UTF8, "text/plain");

        try
        {
            HttpResponseMessage response = await HttpClient.PostAsync(url, content, ct);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                SteamLoginResponse loginResponse = JsonConvert.DeserializeObject<SteamLoginResponse>(json);
                if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                {
                    _sessionToken = loginResponse.Token;
                    // Prefer steamId if available, fallback to internal id
                    _userId = !string.IsNullOrEmpty(loginResponse.SteamId) ? loginResponse.SteamId : loginResponse.Id;
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Log error if needed
        }

        return false;
    }

    public async Task<VotingResultResponse> FetchVoteTotalsAsync(CancellationToken ct = default)
    {
        string content = await GetAsync($"{BaseUrl}/result", ct);
        return ParseVotingResult(content);
    }

    public async Task<VotingResultResponse> SubmitVoteAsync(ulong playerId, VotingType votingType, CancellationToken ct = default)
    {
        string username = TryGetPlayerName(playerId);
        string vote = GetVotingTypeAsString(votingType);

        string content = await GetAsync($"{BaseUrl}/" +
                                        $"vote?" +
                                        $"platformUserId={playerId}&" +
                                        $"username={username}&" +
                                        $"platform=STEAM&" +
                                        $"vote={vote}", ct);

        // The /vote endpoint in the new API returns a string message, not the full result.
        // We might need to fetch the result separately if we want updated totals immediately,
        // but with WebSockets, we will get it anyway.
        // For now, let's just return null or fetch it.
        // The original code expected a VoteResult here.

        return await FetchVoteTotalsAsync(ct);
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

    private static VotingResultResponse ParseVotingResult(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<VotingResultResponse>(content);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static VoteResult ParseVoteResult(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new VoteResult(0, 0, 0);
        }

        try
        {
            return JsonConvert.DeserializeObject<VoteResult>(content) ?? new VoteResult(0, 0, 0);
        }
        catch (Exception)
        {
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

    private class SteamLoginResponse
    {
        [JsonProperty("token")] public string Token { get; set; }
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("steamId")] public string SteamId { get; set; }
    }
}