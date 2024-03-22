namespace ConfigManager
{
    public enum ConfigStatus
    {
        Good,
        LocalModified,
        LiveModified,
        NotDeployed,
        LocalBuildModified,
        BuildModified,
        BuildNotDeployed
    }
}
