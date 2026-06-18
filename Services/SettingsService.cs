using Newtonsoft.Json;
using UdpSimulator.Models;

namespace UdpSimulator.Services
{
    public class AppSettings
    {
        public string   LocalIp        { get; set; } = "0.0.0.0";
        public int      LocalPort      { get; set; } = 12345;
        public string   RemoteIp       { get; set; } = "127.0.0.1";
        public int      RemotePort     { get; set; } = 12346;
        public UdpMode  Mode           { get; set; } = UdpMode.Bidirectional;
        public int      BurstCount     { get; set; } = 1;
        public int      AutoIntervalMs { get; set; } = 1000;
        public string   AutoPayloadHex { get; set; } = "55 44 50 53 49 4D 00 01";
        public string   SendHex        { get; set; } = "DE AD BE EF";
    }

    public class SettingsService
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UdpSimulator", "settings.json");

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonConvert.DeserializeObject<AppSettings>(
                        File.ReadAllText(_path)) ?? new();
            }
            catch { }
            return new();
        }

        public void Save(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path,
                    JsonConvert.SerializeObject(s, Formatting.Indented));
            }
            catch { }
        }
    }
}
