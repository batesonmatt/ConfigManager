namespace ConfigManager
{
    public enum ConfigStatus
    {
        Good,
        LocalModified,
        LiveModified,
        NotDeployed,
        ReleaseModified,
        DebugModified,
        BuildModified
    }
}
