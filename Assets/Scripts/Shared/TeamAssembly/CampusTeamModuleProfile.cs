using UnityEngine;

// One asset belongs to one member. No member edits another module's profile.
public sealed class CampusTeamModuleProfile : ScriptableObject
{
    public string module;
    public bool integrated;
}
