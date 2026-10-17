namespace TsScheduler;
internal static class RecommendationSwitch
{
    internal static void Save(string home,bool enabled)
    {
        Directory.CreateDirectory(home);
        var flag=Path.Combine(home,"recommend-disabled.txt");
        if(enabled)File.Delete(flag);else File.WriteAllText(flag,"off");
    }
}
