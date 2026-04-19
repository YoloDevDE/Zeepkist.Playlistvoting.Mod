namespace PlaylistVoting.core;

public static class VotingEventBus
{
    // Global event hub reference for command -> state communication.
    // Set this in Plugin/VotingManager startup before registering commands.
    public static IVotingEventHub Hub { get; set; } = new VotingEventHub();
}