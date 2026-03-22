using UdpSimulator.Models;

namespace UdpSimulator.Service
{
    public interface IProfileService
    {
        void SaveSendProfile(SendProfile profile);
        SendProfile? LoadSendProfile(string name);
        List<SendProfile> LoadAllSendProfiles();
        void DeleteSendProfile(string name);
        List<string> GetSendProfileNames();


        void SaveReceiveProfile(ReceiveProfile profile);
        ReceiveProfile? LoadReceiveProfile(string name);
        List<ReceiveProfile> LoadAllReceiveProfiles();
        void DeleteReceiveProfile(string name);
        List<string> GetReceiveProfileNames();
    }
}
