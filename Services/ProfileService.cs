using Newtonsoft.Json;
using System.IO;
using UdpSimulator.Models;
using UdpSimulator.Service;

namespace UdpSimulator.Services
{
    public class ProfileService : IProfileService
    {
        private readonly string _sendDir;
        private readonly string _receiveDir;

        public ProfileService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _sendDir = Path.Combine(baseDir, "Profiles", "Send");
            _receiveDir = Path.Combine(baseDir, "Profiles", "Receive");

            Directory.CreateDirectory(_sendDir);
            Directory.CreateDirectory(_receiveDir);
        }

        public void SaveSendProfile(SendProfile profile)
        {
            var path = Path.Combine(_sendDir, $"{profile.Name}.json");
            var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public SendProfile? LoadSendProfile(string Name)
        {
            var path = Path.Combine(_sendDir, $"{Name}.json");
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<SendProfile>(File.ReadAllText(path));
        }

        public List<SendProfile> LoadAllSendProfiles()
        {
            var result = new List<SendProfile>();

            foreach (var file in Directory.GetFiles(_sendDir, "*.json"))
            {
                var profile = JsonConvert.DeserializeObject<SendProfile>(File.ReadAllText(file));
                if (profile != null) result.Add(profile);
            }
            return result;
        }

        public void DeleteSendProfile(string Name)
        {
            var path = Path.Combine(_sendDir, $"{Name}.json");
            if (File.Exists(path)) File.Delete(path);
        }

        public List<string> GetSendProfileNames()
        {
            return Directory.GetFiles(_sendDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null)
                .Cast<string>()
                .ToList();
        }
        public void SaveReceiveProfile(ReceiveProfile profile)
        {
            var path = Path.Combine(_receiveDir, $"{profile.Name}.json");
            var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public ReceiveProfile? LoadReceiveProfile(string name)
        {
            var path = Path.Combine(_receiveDir, $"{name}.json");
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<ReceiveProfile>(
                File.ReadAllText(path));
        }

        public List<ReceiveProfile> LoadAllReceiveProfiles()
        {
            var result = new List<ReceiveProfile>();
            foreach (var file in Directory.GetFiles(_receiveDir, "*.json"))
            {
                var profile = JsonConvert.DeserializeObject<ReceiveProfile>(
                    File.ReadAllText(file));
                if (profile != null) result.Add(profile);
            }
            return result;
        }

        public void DeleteReceiveProfile(string name)
        {
            var path = Path.Combine(_receiveDir, $"{name}.json");
            if (File.Exists(path)) File.Delete(path);
        }

        public List<string> GetReceiveProfileNames()
        {
            return Directory.GetFiles(_receiveDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null)
                .Cast<string>()
                .ToList();
        }


    }
}
