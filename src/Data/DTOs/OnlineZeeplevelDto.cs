using System;

namespace PlaylistVoting.Data.DTOs;

[Serializable]
public class OnlineZeeplevelDto
{
    public string UID;
    public ulong WorkshopID;
    public string Name;
    public string Collaborators;
    public string OverrideAuthorName;
    public string Author;
    public bool played;
}