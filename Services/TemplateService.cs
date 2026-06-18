using Newtonsoft.Json;

namespace UdpSimulator.Services
{
    public record PayloadTemplate(string Name, string HexPayload);

    public class TemplateService
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UdpSimulator", "templates.json");

        private List<PayloadTemplate> _templates = new();

        public IReadOnlyList<PayloadTemplate> Templates => _templates;

        public TemplateService() => Load();

        public void Save(string name, string hex)
        {
            _templates.RemoveAll(t => t.Name == name);
            _templates.Add(new PayloadTemplate(name, hex));
            Persist();
        }

        public void Delete(string name)
        {
            _templates.RemoveAll(t => t.Name == name);
            Persist();
        }

        public PayloadTemplate? Get(string name) =>
            _templates.FirstOrDefault(t => t.Name == name);

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                _templates = JsonConvert.DeserializeObject<List<PayloadTemplate>>(
                    File.ReadAllText(_path)) ?? new();
            }
            catch { }
        }

        private void Persist()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path,
                    JsonConvert.SerializeObject(_templates, Formatting.Indented));
            }
            catch { }
        }
    }
}
