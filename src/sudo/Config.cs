using System;
using static System.Environment;
using System.IO;
using System.Linq;
using sudo;

class Config
{
    public bool multi_run = false;
    public int idle_timeout = 5;
    public int IdleTimeoutInMilliseconds => idle_timeout * 60 * 1000;

    public string Serialize(bool userFriendly = false)
        => userFriendly ?
            $"run: {(multi_run ? "multi" : "single")}{NewLine}idle-timeout: {idle_timeout} minute(s)" :
            $"multi-run: {multi_run}{NewLine}idle-timeout: {idle_timeout}";

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile));
        File.WriteAllText(ConfigFile, Serialize());
    }

    public static string ConfigFile => Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "win-sudo", "app.settings");

    public static Config Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile));
        var result = new Config();
        try
        {
            var delimiter = ":".ToCharArray();

            var values = File.ReadAllLines(ConfigFile)
                             .ToDictionary(x => x.Split(delimiter, 2).FirstOrDefault()?.Trim(),
                                           x => x.Split(delimiter, 2).LastOrDefault()?.Trim());

            result.multi_run = values["multi-run"].Equals("true", StringComparison.OrdinalIgnoreCase);
            int.TryParse(values["idle-timeout"], out result.idle_timeout);
        }
        catch
        {
            try { result.Save(); }
            catch (Exception e) { e.LogError(); }
        }
        return result;
    }
}