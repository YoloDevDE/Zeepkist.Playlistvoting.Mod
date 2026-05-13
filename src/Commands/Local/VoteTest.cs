using System;
using System.Threading.Tasks;
using PlaylistVoting.Core.Controllers;
using PlaylistVoting.Core.Models;
using ZeepSDK.ChatCommands;

namespace PlaylistVoting.Commands.Local;

public class VoteTest : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "votetest";
    public string Description => "[Playlist Voting] Simulates multiple random votes for testing.";

    public void Handle(string arguments)
    {
        int count = 10;

        if (!string.IsNullOrEmpty(arguments) && int.TryParse(arguments, out int parsed))
        {
            count = parsed;
        }

        Task.Run(async () =>
        {
            Random random = new Random();
            string[] platforms = { "STEAM", "TWITCH", "YOUTUBE", "DASHBOARD" };

            for (int i = 0; i < count; i++)
            {
                string platform = platforms[random.Next(platforms.Length)];
                ulong fakeId = (ulong)random.Next(1000000, 9999999);
                string fakeUsername = "Tester_" + fakeId;
                VotingType type = random.Next(2) == 0 ? VotingType.Yes : VotingType.No;

                await VotingController.Instance.BackendService.SubmitVoteAsync(fakeId, type, platform);

                await Task.Delay(100);
            }
        });
    }
}