using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using PlaylistVoting.core;
using ZeepkistClient;

namespace PlaylistVoting.api;

public class RestController
{
    private static readonly HttpClient HttpClient = new HttpClient();

    private static string Token => VotingConfig.Instance.WebToken;
    private static string BaseUrl => VotingConfig.Instance.WebApiUrl;

    public async Task<string> GetCurrentLevelNameAsync() => await GetAsync($"{BaseUrl}/currentLevel/name");

    public async Task<string> GetCurrentAuthorAsync() => await GetAsync($"{BaseUrl}/currentLevel/author");

    public async Task<VoteResult> FetchVoteTotalsAsync()
    {
        string content = await GetAsync($"{BaseUrl}/result?result=TOTAL");
        return ParseVoteResult(content);
    }

    public async Task<VoteResult> SubmitVoteAsync(ulong playerId, VotingType votingType)
    {
        string username = TryGetPlayerName(playerId);
        string vote = GetVotingTypeAsString(votingType);

        string content = await GetAsync($"{BaseUrl}/" +
                                        $"vote?" +
                                        $"platformUserId={playerId}&" +
                                        $"username={username}&" +
                                        $"platform=STEAM&" +
                                        $"vote={vote}");
        return ParseVoteResult(content);
    }


    public async Task<bool> SetCurrentLevelAsync(string uid, string levelName, string author, string workshopID)
    {
        string url = $"{BaseUrl}/currentLevel";
        FormUrlEncodedContent content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("uid", uid),
            new KeyValuePair<string, string>("name", levelName),
            new KeyValuePair<string, string>("author", author),
            new KeyValuePair<string, string>("workshopID", workshopID)
        ]);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        HttpResponseMessage response = await HttpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private string TryGetPlayerName(ulong playerId) => ZeepkistNetwork.TryGetPlayer(playerId, out ZeepkistNetworkPlayer player) ? player.Username : playerId.ToString();

    private static VoteResult ParseVoteResult(string content) =>
        // if (string.IsNullOrWhiteSpace(content))
        // {
        //     return null;
        // }
        //
        // Match match = Regex.Match(content, VoteResultPattern);
        // if (!match.Success)
        // {
        //     return null;
        // }
        //
        // return new VoteResult(
        //     int.Parse(match.Groups[1].Value),
        //     int.Parse(match.Groups[2].Value),
        //     int.Parse(match.Groups[3].Value)
        // );
        new VoteResult(0, 0, 0);

    public async Task<string> GetAsync(string url)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        HttpResponseMessage response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ResetVotesAsync()
    {
        string url = $"{BaseUrl}/reset";
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        HttpResponseMessage response = await HttpClient.SendAsync(request);
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