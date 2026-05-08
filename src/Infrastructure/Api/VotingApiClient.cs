using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Lombok.NET;
using Newtonsoft.Json;
using PlaylistVoting.Core.Config;
using PlaylistVoting.Core.Models;
using ZeepkistClient;

namespace PlaylistVoting.Infrastructure.Api;

public class VotingApiClient
{
    private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    private static string BaseUrl => VotingConfig.Instance.WebApiUrl;

    public string GetSessionToken() => VotingConfig.Instance.AuthToken;

    public string GetUserId() => VotingConfig.Instance.AuthUserId;

    public async Task<bool> LoginWithSteamTicketAsync(string ticketHex)
    {
        string authBaseUrl = BaseUrl;
        if (authBaseUrl.EndsWith("/playlistvoting"))
        {
            authBaseUrl = authBaseUrl.Substring(0, authBaseUrl.Length - "/playlistvoting".Length);
        }

        try
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{authBaseUrl}/auth/steam/ticket");
            request.Content = new StringContent(ticketHex, Encoding.UTF8, "text/plain");

            HttpResponseMessage response = await HttpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                SteamLoginResponse loginResponse = JsonConvert.DeserializeObject<SteamLoginResponse>(content);
                if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                {
                    VotingConfig.Instance.AuthToken = loginResponse.Token;
                    VotingConfig.Instance.AuthUserId = !string.IsNullOrEmpty(loginResponse.SteamId)
                        ? loginResponse.SteamId
                        : loginResponse.Id;
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // ignore
        }

        return false;
    }

    public async Task<PlaylistSessionInfo> GetActiveSessionAsync()
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/sessions/active?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<PlaylistSessionInfo>(content);
    }

    public async Task<PlaylistSessionInfo> GetLatestActiveSessionAsync()
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/sessions/latest-active?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<PlaylistSessionInfo>(content);
    }

    public async Task<bool> SetPlaylistModeAsync(bool enabled)
    {
        HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{BaseUrl}/sessions/active/playlist-mode?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        request.Content = new StringContent(JsonConvert.SerializeObject(new
        {
            enabled
        }), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<LevelMetadata>> GetToBeVotedPlaylistAsync()
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/sessions/active/playlist/to-be-voted?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<List<LevelMetadata>>(content);
    }

    public async Task<List<LevelMetadata>> GetFinalPlaylistAsync()
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/sessions/active/playlist/final?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<List<LevelMetadata>>(content);
    }

    public async Task<bool> FinalizeLevelAsync(string levelUid)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/sessions/active/levels/{Uri.EscapeDataString(levelUid)}/finalize?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResetVotesForLevelAsync(string levelUid)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/sessions/active/levels/{Uri.EscapeDataString(levelUid)}/votes?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<VotingResultResponse> GetLevelResultAsync(string levelUid)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/sessions/active/levels/{Uri.EscapeDataString(levelUid)}/result?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return ParseVotingResult(content);
    }

    public async Task<VotingResultResponse> FetchVoteTotalsAsync()
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/sessions/active/result?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return ParseVotingResult(content);
    }

    public async Task<VotingResultResponse> SubmitVoteAsync(ulong playerId, VotingType votingType, string platform = "STEAM")
    {
        string username = FindUsernameBySteamId(playerId);
        string vote = GetVotingTypeAsString(votingType);

        string url =
            $"{BaseUrl}/votes?platformUserId={playerId}&username={Uri.EscapeDataString(username)}&platform={platform}&vote={vote}&token={Uri.EscapeDataString(GetSessionToken())}";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuth(request);

        await HttpClient.SendAsync(request).ConfigureAwait(false);

        return await FetchVoteTotalsAsync();
    }

    public async Task<bool> UpdatePlaylistAsync(List<LevelMetadata> levels)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/sessions/active/playlist?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        request.Content = new StringContent(JsonConvert.SerializeObject(levels), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetCurrentLevelAsync(LevelMetadata level, bool includeAbstain)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/sessions/active/level?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        string json = JsonConvert.SerializeObject(new
        {
            uid = level.Uid,
            name = level.Name,
            author = level.Author,
            workshopID = level.WorkshopId,
            includeAbstain
        });
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<string> ResetVotesAsync()
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/sessions/active/votes?token={Uri.EscapeDataString(GetSessionToken())}");
        AddAuth(request);

        HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static void AddAuth(HttpRequestMessage request)
    {
        string token = VotingConfig.Instance.AuthToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static string FindUsernameBySteamId(ulong playerId) => ZeepkistNetwork.TryGetPlayer(playerId, out ZeepkistNetworkPlayer player) ? player.Username : playerId.ToString();

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

    private static string GetVotingTypeAsString(VotingType votingType)
    {
        return votingType switch
        {
            VotingType.Yes => "YES",
            VotingType.No => "NO",
            VotingType.Remove => "REMOVE",
            VotingType.Abstain => "ABSTAIN",
            VotingType.Idk => "IDK",
            _ => throw new ArgumentException("Invalid voting type", nameof(votingType))
        };
    }
}

[NoArgsConstructor]
[AllArgsConstructor]
public partial class SteamLoginResponse
{
    [JsonProperty("token")] public string Token { get; set; }
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("steamId")] public string SteamId { get; set; }
}