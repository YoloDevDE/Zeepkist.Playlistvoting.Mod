using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZeepkistClient;

namespace PlaylistVoting.api;

public interface IVotingApiClient
{
    Task<string> GetCurrentLevelNameAsync();
    Task<string> GetCurrentAuthorAsync();
    Task<VoteResult?> FetchVoteTotalsAsync();
    Task<VoteResult?> SubmitVoteYesAsync(ulong playerId);
    Task<VoteResult?> SubmitVoteNoAsync(ulong playerId);
    Task<VoteResult?> SubmitVoteRemoveAsync(ulong playerId);
    Task<string> ResetVotesAsync();
    Task<bool> SetMapAsync(string uid, string mapName, string author, string workshopID);
}

/// <summary>
///     Kapselt alle HTTP-Kommunikation mit der Yololurk Voting API.
/// </summary>
public class VotingApiClient : IVotingApiClient
{
    private const string VoteResultPattern = @"(\d+)/(\d+)/(\d+) \(y/n/a\)";
    private static readonly HttpClient HttpClient = new HttpClient();
    private readonly Func<string> _tokenGetter;
    private readonly Func<string> _baseUrlGetter;

    public VotingApiClient(Func<string> tokenGetter, Func<string> baseUrlGetter)
    {
        _tokenGetter = tokenGetter;
        _baseUrlGetter = baseUrlGetter;
    }

    private string Token => _tokenGetter();
    private string BaseUrl => _baseUrlGetter().TrimEnd('/');

    public async Task<string> GetCurrentLevelNameAsync() => await GetAsync($"{BaseUrl}/currentLevel/name?token={Token}");

    public async Task<string> GetCurrentAuthorAsync() => await GetAsync($"{BaseUrl}/currentLevel/author?token={Token}");

    public async Task<VoteResult?> FetchVoteTotalsAsync()
    {
        string content = await GetAsync($"{BaseUrl}/result?token={Token}&result=TOTAL");
        return ParseVoteResult(content);
    }

    private string TryGetPlayerName(ulong playerId)
    {
        return ZeepkistNetwork.TryGetPlayer(playerId, out ZeepkistNetworkPlayer player) ? player.Username : playerId.ToString();
    }

    public async Task<VoteResult?> SubmitVoteYesAsync(ulong playerId)
    {
        string content = await GetAsync($"{BaseUrl}/vote?token={Token}&platformUserId={playerId}&username={TryGetPlayerName(playerId)}&platform=STEAM&vote=YES");
        return ParseVoteResult(content);
    }

    public async Task<VoteResult?> SubmitVoteNoAsync(ulong playerId)
    {
        string content = await GetAsync($"{BaseUrl}/vote?token={Token}&platformUserId={playerId}&username={TryGetPlayerName(playerId)}&platform=STEAM&vote=NO");
        return ParseVoteResult(content);
    }

    public async Task<VoteResult?> SubmitVoteRemoveAsync(ulong playerId)
    {
        string content = await GetAsync($"{BaseUrl}/vote?token={Token}&platformUserId={playerId}&username={TryGetPlayerName(playerId)}&platform=STEAM&vote=REMOVE");
        return ParseVoteResult(content);
    }

    public async Task<string> ResetVotesAsync() => await GetAsync($"{BaseUrl}/reset?token={Token}");

    public async Task<bool> SetMapAsync(string uid, string mapName, string author, string workshopID)
    {
        string url = $"{BaseUrl}/currentLevel";
        FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", Token),
            new KeyValuePair<string, string>("uid", uid),
            new KeyValuePair<string, string>("name", mapName),
            new KeyValuePair<string, string>("author", author),
            new KeyValuePair<string, string>("workshopID", workshopID)
        });
        HttpResponseMessage response = await HttpClient.PostAsync(url, content);
        return response.IsSuccessStatusCode;
    }

    private static VoteResult? ParseVoteResult(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        Match match = Regex.Match(content, VoteResultPattern);
        if (!match.Success)
        {
            return null;
        }

        return new VoteResult(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value)
        );
    }

    private async Task<string> GetAsync(string url)
    {
        HttpResponseMessage response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}

/// <summary>
///     Ergebnis einer Voting-API-Abfrage.
/// </summary>
public record VoteResult(int YesVotes, int NoVotes, int AbstainVotes)
{
    public bool IsYesWinning => YesVotes > NoVotes;
    public bool IsNoWinning => NoVotes > YesVotes;
    public bool IsTie => YesVotes == NoVotes;
    public int YesVotes { get; } = YesVotes;
    public int NoVotes { get; } = NoVotes;
    public int AbstainVotes { get; } = AbstainVotes;
}